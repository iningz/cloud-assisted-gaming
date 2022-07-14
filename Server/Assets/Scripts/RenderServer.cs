using System;
using System.Threading;
using System.Threading.Channels;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using Unity.Collections;
using Force.Crc32;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Grpc.Core;

public class ServerConfig
{
    public string ScheduleServerHost { get; set; }
    public int ScheduleListenPort { get; set; }

    public string EncodePreset { get; set; }
    public int FrameRate { get; set; }
    public int Crf { get; set; }
    public int Gop { get; set; }

    public int Mtu { get; set; }
    public int ClientPort { get; set; }
    public float TimeToEndSession { get; set; }

    public float LogPeriod { get; set; }
}

public class RenderServer : MonoBehaviour
{
    class Session
    {
        public bool Initialized;

        public Vector2Int FrameSize;
        public int Version;
        public FrameEncoder Encoder;
        public ServerScene Scene;

        public RenderTexture Target;
        public Texture2D[] TextureBuffer;
        public int CurrentTexture;

        public float TimeToEnd;
    }

    struct EncodeRequest
    {
        public IPEndPoint ClientEP;
        public Session Session;
        public int FrameIndex;
        public NativeArray<byte> TextureData;
    }

    const int CHANNEL_SIZE = 3;
    const int TEXTURE_BUFFER_SIZE = 3;

    [Header("References")]
    [SerializeField]
    ServerScene m_scenePrefab;

    [SerializeField]
    ObjectDatabase m_objectDatabase;

    [SerializeField]
    ServerObjectGenerator m_objectGenerator;

    [Header("Scheduling settings")]
    [SerializeField]
    string m_scheduleServerHost; // NOT USED YET

    [SerializeField]
    int m_scheduleListenPort;

    [Header("Encode settings")]
    [SerializeField]
    EncodePreset m_preset;

    [SerializeField]
    int m_frameRate;

    [SerializeField]
    int m_crf;

    [SerializeField]
    int m_gop;

    [Header("Client settings")]
    [SerializeField]
    int m_mtu;

    [SerializeField]
    int m_clientPort;

    [SerializeField]
    float m_timeToEndSession;

    [Header("Testing")]
    [SerializeField]
    float m_logPeriod;

    readonly Dictionary<int, Session> m_sessionTable = new Dictionary<int, Session>();

    int m_nextId = 0;

    CancellationTokenSource m_cts;
    CancellationToken m_token;

    Server m_grpcServer;
    NetworkingServer m_server;

    Channel<EncodeRequest> m_encodeChannel;

    System.Diagnostics.Stopwatch m_encodeWatch;
    System.Diagnostics.Stopwatch m_renderWatch;
    int m_encodeFrames;
    long m_encodeMs;
    int m_renderFrames;
    long m_renderMs;
    float m_timer;

    void Awake()
    {
        ReadConfig();

        m_encodeWatch = new System.Diagnostics.Stopwatch();
        m_renderWatch = new System.Diagnostics.Stopwatch();
        m_encodeFrames = 0;
        m_encodeMs = 0L;
        m_renderFrames = 0;
        m_renderMs = 0L;
        m_timer = m_logPeriod;
    }

    void ReadConfig()
    {
        string text;
        try
        {
            string path = "config.yml";
#if UNITY_EDITOR
            path = Path.Combine(Application.dataPath, path);
#endif
            text = File.ReadAllText(path);
        }
        catch
        {
            Debug.Log("No config file found!");
            return;
        }

        var input = new StringReader(text);
        var deserializer = new DeserializerBuilder()
               .WithNamingConvention(PascalCaseNamingConvention.Instance)
               .Build();
        var config = deserializer.Deserialize<ServerConfig>(input);

        m_scheduleServerHost = config.ScheduleServerHost;
        m_scheduleListenPort = config.ScheduleListenPort;

        m_preset = Enum.Parse<EncodePreset>(config.EncodePreset);
        m_frameRate = config.FrameRate;
        m_crf = config.Crf;
        m_gop = config.Gop;

        m_mtu = config.Mtu;
        m_clientPort = config.ClientPort;
        m_timeToEndSession = config.TimeToEndSession;

        m_logPeriod = config.LogPeriod;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (m_sessionTable != null)
        {
            foreach (Session session in m_sessionTable.Values)
            {
                session.Encoder.SetCrf(m_crf);
            }
        }
    }
#endif

    void OnEnable()
    {
        m_cts = new CancellationTokenSource();
        m_token = m_cts.Token;

        m_grpcServer = new Server();
        m_grpcServer.Ports.Add("0.0.0.0", m_scheduleListenPort, ServerCredentials.Insecure);
        m_grpcServer.Services.Add(OpenSession.OpenSession.BindService(new OpenSessionService(this)));
        m_grpcServer.Start();

        m_server = new NetworkingServer(m_mtu, m_clientPort);

        m_encodeChannel = System.Threading.Channels.Channel.CreateBounded<EncodeRequest>(CHANNEL_SIZE);

        new Thread(Consume).Start();

        //StartSession(new Vector2Int(Screen.width, Screen.height), 0, out _);
    }

    void OnDisable()
    {
        m_cts.Cancel();

        foreach (Session session in m_sessionTable.Values)
        {
            session.Encoder.Dispose();
            if (session.Scene != null)
            {
                Destroy(session.Scene.gameObject);
            }
        }
        m_sessionTable.Clear();

        _ = m_grpcServer.ShutdownAsync();
        m_server.Dispose();

        m_encodeChannel.Writer.Complete();
        m_cts.Dispose();

        m_encodeWatch.Stop();
        m_renderWatch.Stop();
    }

    void Update()
    {
        if (m_server.Reader.TryRead(out NetworkingServer.ClientRequest clientRequest))
        {
            if (clientRequest.Data.Length <= sizeof(int) * 2 + 4)
            {
                Debug.LogWarning("Packet too short");
                return;
            }

            if (!Crc32CAlgorithm.IsValidWithCrcAtEnd(clientRequest.Data))
            {
                Debug.LogWarning("Invalid packet received!");
                return;
            }

            int sessionId = BitConverter.ToInt32(clientRequest.Data, 0);
            if (!m_sessionTable.TryGetValue(sessionId, out Session session))
            {
                Debug.LogWarning("Session Not Found");
                return;
            }
            session.TimeToEnd = m_timeToEndSession;

            int frameIndex = BitConverter.ToInt32(clientRequest.Data, sizeof(int));

            m_renderWatch.Restart();
            //update scene and render
            if (!session.Initialized)
            {
                session.Initialized = true;

                session.Target = new RenderTexture(session.FrameSize.x, session.FrameSize.y, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, UnityEngine.Experimental.Rendering.GraphicsFormat.None, 0);
                session.TextureBuffer = new Texture2D[TEXTURE_BUFFER_SIZE];
                for (int i = 0; i < session.TextureBuffer.Length; i++)
                {
                    session.TextureBuffer[i] = new Texture2D(session.FrameSize.x, session.FrameSize.y, TextureFormat.RGBA32, false, true);
                }
                session.CurrentTexture = 0;

                ServerScene scene = Instantiate(m_scenePrefab);
                scene.Camera.targetTexture = session.Target;
                scene.Camera.enabled = false;
                session.Scene = scene;
            }
            foreach (Session s in m_sessionTable.Values)
            {
                if (s.Scene != null)
                {
                    if (s == session)
                    {
                        s.Scene.gameObject.SetActive(true);
                    }
                    else
                    {
                        s.Scene.gameObject.SetActive(false);
                    }
                }
            }
            using MemoryStream ms = new MemoryStream(clientRequest.Data, sizeof(int) * 2, clientRequest.Data.Length - sizeof(int) * 2 - 4);
            using BinaryReader reader = new BinaryReader(ms);
            session.Scene.DeserializeScene(reader, m_objectDatabase, m_objectGenerator);

            EncodeRequest encodeRequest;
            encodeRequest.ClientEP = clientRequest.RemoteEP;
            encodeRequest.Session = session;
            encodeRequest.FrameIndex = frameIndex;

            RenderTexture.active = session.Target;
            session.Scene.Camera.Render();
            Texture2D tex = session.TextureBuffer[session.CurrentTexture];
            session.CurrentTexture = (session.CurrentTexture + 1) % session.TextureBuffer.Length;
            tex.ReadPixels(new Rect(0f, 0f, session.FrameSize.x, session.FrameSize.y), 0, 0, false);
            encodeRequest.TextureData = tex.GetRawTextureData<byte>();
            m_renderWatch.Stop();
            m_renderFrames += 1;
            m_renderMs += m_renderWatch.ElapsedMilliseconds;

            if (!m_encodeChannel.Writer.TryWrite(encodeRequest))
            {
                Debug.LogWarning("EncodeChannel Full");
            }
        }

        List<int> toEnd = new List<int>();
        foreach (KeyValuePair<int, Session> kvp in m_sessionTable)
        {
            kvp.Value.TimeToEnd -= Time.deltaTime;
            if (kvp.Value.TimeToEnd <= 0f)
            {
                toEnd.Add(kvp.Key);
            }
        }
        toEnd.ForEach(id => StopSession(id));

        m_timer -= Time.deltaTime;
        if (m_timer <= 0f)
        {
            m_timer += m_logPeriod;

            if (m_sessionTable.Count > 0)
            {
                Debug.Log($"SessionCount {m_sessionTable.Count} | Render {m_renderFrames / m_logPeriod}FPS {(float)m_renderMs / m_renderFrames}ms | Encode {m_encodeFrames / m_logPeriod}FPS {(float)m_encodeMs / m_encodeFrames}ms");
            }
            m_encodeFrames = 0;
            m_encodeMs = 0L;
            m_renderFrames = 0;
            m_renderMs = 0L;
        }
    }

    public bool StartSession(Vector2Int frameSize, int version, out int sessionId)
    {
        sessionId = m_nextId;
        Session session = new Session();
        try
        {
            session.Initialized = false;

            session.FrameSize = frameSize;
            session.Version = version; //accept any version for now
            session.Encoder = new FrameEncoder(m_frameRate, new Vector2Int(frameSize.x, frameSize.y), m_preset, m_crf, m_gop);
            session.Scene = null;

            session.Target = null;
            session.TextureBuffer = null;
            session.CurrentTexture = 0;

            session.TimeToEnd = m_timeToEndSession;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Start session exception: {ex}");
            if (session.Encoder != null)
            {
                session.Encoder.Dispose();
            }
            return false;
        }

        m_nextId++;
        m_sessionTable.Add(sessionId, session);

        return true;
    }

    public bool StopSession(int sessionId)
    {
        if (m_sessionTable.TryGetValue(sessionId, out Session session))
        {
            session.Encoder.Dispose();
            if (session.Initialized)
            {
                if (session.Scene != null)
                {
                    Destroy(session.Scene.gameObject);
                }
                Destroy(session.Target);
                foreach (Texture2D tex in session.TextureBuffer)
                {
                    Destroy(tex);
                }
            }
            m_sessionTable.Remove(sessionId);
            return true;
        }
        else
        {
            return false;
        }
    }

    void Consume()
    {
        EncodeRequest encodeRequest;
        while (true)
        {
            if (m_token.IsCancellationRequested)
            {
                break;
            }

            if (!m_encodeChannel.Reader.TryRead(out encodeRequest))
            {
                continue;
            }

            m_encodeWatch.Restart();
            byte[] data = encodeRequest.Session.Encoder.Encode(encodeRequest.TextureData);


            byte[] response = new byte[data.Length + sizeof(int)];
            Array.Copy(BitConverter.GetBytes(encodeRequest.FrameIndex), 0, response, 0, sizeof(int));
            Array.Copy(data, 0, response, sizeof(int), data.Length);
            m_encodeWatch.Stop();
            m_encodeFrames += 1;
            m_encodeMs += m_encodeWatch.ElapsedMilliseconds;

            NetworkingServer.SendRequest sendRequest;
            sendRequest.ClientEP = encodeRequest.ClientEP;
            sendRequest.Data = response;

            if (!m_server.Writer.TryWrite(sendRequest))
            {
                Debug.LogWarning("SendChannel Full");
            }
        }
    }
}
