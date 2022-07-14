using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using UnityEngine;
using UnityEngine.UI;
using Force.Crc32;

public class ClientConfig
{
    public BasicSetting BasicSetting { get; set; }
    public DelayControl DelayControl { get; set; }
    public ServerFinding ServerFinding { get; set; }
    public Testing Testing { get; set; }
}

public class BasicSetting
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int GameVersion { get; set; }
    public int Mtu { get; set; }
    public int FrameRate { get; set; }
    public int BufferSize { get; set; }
}

public class DelayControl
{
    public int HistoryLength { get; set; }
    public int SessionHistoryLength { get; set; }
    public int DelayAdjustPeriod { get; set; }
    public float LengthenDelayThreshold { get; set; }
    public float ShortenDelayThreshold { get; set; }
    public int DelayIncrementMilliseconds { get; set; }
    public int SatisfyingDelayMilliseconds { get; set; }
}

public class ServerFinding
{
    public string ScheduleServerHost { get; set; }
    public float RequestPeriod { get; set; }
    public int RequestTimeoutMilliseconds { get; set; }
    public int TargetServerCount { get; set; }
    public float ServerOnTimeRateRequirement { get; set; }
    public float TimeToKillServer { get; set; }
}

public class Testing
{
    public float StatsPeriod { get; set; }
}

public class ClientSession
{
    public int Id;
    public RemoteRenderNetworkingClient Client;
    public FrameDecoder Decoder;

    //stats
    public long OnTimeHistory;
    public long SuccessHistory;
    public long DelayCounter;
    public int FrameCounter;

    public float TimeToKill;

    public float GetAndResetDelay()
    {
        if (FrameCounter == 0)
        {
            DelayCounter = 0;
            return float.NaN;
        }

        float delay = DelayCounter / FrameCounter;
        DelayCounter = 0;
        FrameCounter = 0;
        return delay;
    }
}

public enum FrameState
{
    Idle,
    Pending,
    Ready,
    Failed
}

public struct FrameBufferElement
{
    public long IssueTimestamp;
    public ClientSession Session;
    public byte[] TextureData;
    public FrameState State;
}

public class RemoteRenderClient : MonoBehaviour
{
    const int RECEIVE_CHANNEL_SIZE = 3;
    const int SCENE_DATA_BUFFER_SIZE = 5 * 1024 * 1024;

    [Header("Client references")]
    [SerializeField]
    RawImage m_rawImage;

    [SerializeField]
    ClientSceneSerializer m_sceneSerializer;

    [Header("Basic settings")]
    [SerializeField]
    Vector2Int m_frameSize;

    [SerializeField]
    int m_gameVersion;

    [SerializeField]
    int m_mtu;

    [SerializeField]
    int m_frameRate;

    [SerializeField]
    int m_bufferSize;

    [Header("Delay control")]
    [SerializeField, Range(1, 64)]
    int m_historyLength;

    [SerializeField, Range(1, 64)]
    int m_sessionHistoryLength;

    [SerializeField]
    int m_delayAdjustPeriod;

    [SerializeField, Range(0f, 1f)]
    float m_lengthenDelayThreshold;

    [SerializeField, Range(0f, 1f)]
    float m_shortenDelayThreshold;

    [SerializeField]
    int m_delayIncrementMilliseconds;

    [SerializeField]
    int m_satisfyingDelayMilliseconds;

    [Header("Server finding")]
    [SerializeField]
    string m_scheduleServerHost;

    [SerializeField]
    float m_requestPeriod;

    [SerializeField]
    int m_requestTimeoutMilliseconds;

    [SerializeField]
    int m_targetServerCount;

    [SerializeField, Range(0f, 1f)]
    float m_serverOnTimeRateRequirement;

    [SerializeField]
    float m_timeToKillServer;

    [Header("Testing")]
    [SerializeField]
    float m_statsPeriod;

    Texture2D m_texture;
    byte[] m_nextData;
    Vector2Int m_resolution;

    Stopwatch m_watch;

    List<ClientSession> m_sessions;

    CancellationTokenSource m_cts;
    CancellationToken m_token;

    ServerFinder m_serverFinder;

    Channel<byte[]> m_receiveChannel;

    int m_currentFrame;
    FrameBufferElement[] m_frameBuffer;

    int m_frameCounter;
    int m_delayMilliseconds;
    int m_maxDelayMilliseconds;
    long m_onTimeHistory;

    byte[] m_sceneDataBuffer;
    int m_sceneDataLength;

    //stats
    float m_statsTimer;
    int m_uploadBytes;
    int m_downloadBytes;
    int m_framesDecoded;
    long m_decodeDelayCounter;
    int m_framesUpdated;
    float m_onTimeRate;

    void Awake()
    {
        ReadConfig();

        m_statsTimer = m_statsPeriod;
        m_uploadBytes = 0;
        m_downloadBytes = 0;
        m_framesDecoded = 0;
        m_decodeDelayCounter = 0;
        m_framesUpdated = 0;
        m_onTimeRate = 1f;
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
            UnityEngine.Debug.Log("No config file found!");
            return;
        }

        var input = new StringReader(text);
        var deserializer = new DeserializerBuilder()
               .WithNamingConvention(PascalCaseNamingConvention.Instance)
               .Build();
        var config = deserializer.Deserialize<ClientConfig>(input);

        m_frameSize = new Vector2Int(config.BasicSetting.Width, config.BasicSetting.Height);
        m_gameVersion = config.BasicSetting.GameVersion;
        m_mtu = config.BasicSetting.Mtu;
        m_frameRate = config.BasicSetting.FrameRate;
        m_bufferSize = config.BasicSetting.BufferSize;

        m_historyLength = config.DelayControl.HistoryLength;
        m_sessionHistoryLength = config.DelayControl.SessionHistoryLength;
        m_delayAdjustPeriod = config.DelayControl.DelayAdjustPeriod;
        m_lengthenDelayThreshold = config.DelayControl.LengthenDelayThreshold;
        m_shortenDelayThreshold = config.DelayControl.ShortenDelayThreshold;
        m_delayIncrementMilliseconds = config.DelayControl.DelayIncrementMilliseconds;
        m_satisfyingDelayMilliseconds = config.DelayControl.SatisfyingDelayMilliseconds;

        m_scheduleServerHost = config.ServerFinding.ScheduleServerHost;
        m_requestPeriod = config.ServerFinding.RequestPeriod;
        m_requestTimeoutMilliseconds = config.ServerFinding.RequestTimeoutMilliseconds;
        m_targetServerCount = config.ServerFinding.TargetServerCount;
        m_serverOnTimeRateRequirement = config.ServerFinding.ServerOnTimeRateRequirement;
        m_timeToKillServer = config.ServerFinding.TimeToKillServer;

        m_statsPeriod = config.Testing.StatsPeriod;

        UnityEngine.Debug.Log("Config loaded!");
    }

    void OnEnable()
    {
        Application.targetFrameRate = m_frameRate * 2;
        m_resolution = (m_frameSize.x == 0 || m_frameSize.y == 0) ? new Vector2Int(Screen.width, Screen.height) : m_frameSize;
        m_texture = new Texture2D(m_resolution.x, m_resolution.y, TextureFormat.RGB24, false);
        m_nextData = null;
        m_rawImage.texture = m_texture;

        m_watch = new Stopwatch();
        m_watch.Start();

        m_sessions = new List<ClientSession>();

        m_cts = new CancellationTokenSource();
        m_token = m_cts.Token;

        m_serverFinder = new ServerFinder(m_scheduleServerHost);

        m_receiveChannel = Channel.CreateBounded<byte[]>(RECEIVE_CHANNEL_SIZE);

        m_currentFrame = 0;
        m_frameBuffer = new FrameBufferElement[m_bufferSize];
        for (int i = 0; i < m_frameBuffer.Length; i++)
        {
            m_frameBuffer[i].Session = null;
            m_frameBuffer[i].TextureData = new byte[m_resolution.x * m_resolution.y * 3];
            m_frameBuffer[i].State = FrameState.Idle;
        }

        m_frameCounter = m_delayAdjustPeriod;
        m_delayMilliseconds = 500 * m_frameBuffer.Length / m_frameRate;
        m_maxDelayMilliseconds = 1000 * (m_frameBuffer.Length - 1) / m_frameRate;
        m_onTimeHistory = ~0x0;

        m_sceneDataBuffer = new byte[SCENE_DATA_BUFFER_SIZE];
        m_sceneDataLength = 0;

        _ = ServerFindingLoop();
        new Thread(SendLoop).Start();
        new Thread(ReceiveLoop).Start();
    }

    void OnDisable()
    {
        m_cts.Cancel();

        foreach (ClientSession session in m_sessions)
        {
            session.Client.Dispose();
            session.Decoder.Dispose();
        }

        m_serverFinder.Dispose();

        m_receiveChannel.Writer.Complete();

        m_cts.Dispose();

        m_watch.Stop();

        if (m_rawImage != null)
        {
            m_rawImage.texture = null;
        }

        if (m_texture != null)
        {
            Destroy(m_texture);
        }
    }

    void Update()
    {
        if (m_nextData != null)
        {
            m_texture.LoadRawTextureData(m_nextData);
            m_texture.Apply();
            m_nextData = null;
        }

        for (int i = m_sessions.Count - 1; i >= 0; i--)
        {
            ClientSession session = m_sessions[i];
            if (CalculateRate(session.OnTimeHistory, m_sessionHistoryLength) >= m_serverOnTimeRateRequirement)
            {
                session.TimeToKill = m_timeToKillServer;
            }
            else
            {
                session.TimeToKill -= Time.deltaTime;
                if (session.TimeToKill <= 0f)
                {
                    EndSession(session);
                }
            }
        }

        m_statsTimer -= Time.deltaTime;
        if (m_statsTimer <= 0f)
        {
            m_statsTimer += m_statsPeriod;
            UnityEngine.Debug.Log("===============================================================");
            UnityEngine.Debug.Log($"FPS {m_framesUpdated / m_statsPeriod} | Delay {m_delayMilliseconds}ms | Upload {m_uploadBytes >> 10}KB/s | Download {m_downloadBytes >> 10}KB/s | OnTime {m_onTimeRate * 100f}% | Decode {(float)m_decodeDelayCounter / m_framesDecoded}ms");
            m_uploadBytes = 0;
            m_downloadBytes = 0;
            m_framesDecoded = 0;
            m_decodeDelayCounter = 0;
            m_framesUpdated = 0;

            if (m_sessions.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (ClientSession session in m_sessions)
                {
                    sb.Append($" <{session.Client.RemoteEP} | Delay {session.GetAndResetDelay()}ms | OnTime {CalculateRate(session.OnTimeHistory, m_sessionHistoryLength) * 100}% | Success {CalculateRate(session.SuccessHistory, m_sessionHistoryLength) * 100}%> ");
                }
                UnityEngine.Debug.Log(sb.ToString());
            }
        }
    }

    void LateUpdate()
    {
        using MemoryStream ms = new MemoryStream(m_sceneDataBuffer);
        using BinaryWriter writer = new BinaryWriter(ms);
        if (m_sceneSerializer.SerializeScene(writer))
        {
            m_sceneDataLength = (int)ms.Position;
        }
        else
        {
            m_sceneDataLength = 0;
        }
    }

    void AddSession(string ip, int port, int id)
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ip), port);
        m_sessions.Add(new ClientSession
        {
            Id = id,
            Client = new RemoteRenderNetworkingClient(remoteEP, m_mtu, m_receiveChannel.Writer),
            Decoder = new FrameDecoder(m_frameRate, m_resolution),

            OnTimeHistory = ~0x0,
            SuccessHistory = ~0x0,
            DelayCounter = 0,
            FrameCounter = 0,
            TimeToKill = m_timeToKillServer
        });
        UnityEngine.Debug.Log("New session added.");
        m_serverFinder.AddToBlackList(remoteEP);
    }

    void EndSession(ClientSession session)
    {
        if (m_sessions.Remove(session))
        {
            session.Client.Dispose();
            session.Decoder.Dispose();
            UnityEngine.Debug.Log("Session ended.");
        }
        else
        {
            UnityEngine.Debug.LogError("Cannot end session, session not found!");
        }
    }

    void Timeup(object state)
    {
        (state as AutoResetEvent).Set();
    }

    async Task ServerFindingLoop()
    {
        int interval = (int)(m_requestPeriod * 1000);

        while (true)
        {
            if (m_sessions.Count < m_targetServerCount)
            {
                (string host, int port, int sessionId) = await m_serverFinder.FindServer(m_gameVersion, m_resolution, m_requestTimeoutMilliseconds, m_token);
                if (m_token.IsCancellationRequested)
                {
                    break;
                }

                if (!string.IsNullOrEmpty(host))
                {
                    AddSession(host, port, sessionId);
                }
            }

            await Task.Delay(interval, m_token);
            if (m_token.IsCancellationRequested)
            {
                break;
            }
        }
    }

    void ReceiveLoop()
    {
        byte[] data;
        while (true)
        {
            if (m_token.IsCancellationRequested)
            {
                break;
            }

            if (!m_receiveChannel.Reader.TryRead(out data))
            {
                continue;
            }

            m_downloadBytes += data.Length;
            if (data.Length <= sizeof(int))
            {
                throw new Exception("Data size too small!");
            }

            int index = BitConverter.ToInt32(data, 0);
            int framesDelayed = m_currentFrame - index;
            if (framesDelayed < 0 || framesDelayed >= m_frameBuffer.Length)
            {
                UnityEngine.Debug.LogWarning("Received frame out of window! Dropping.");
                continue;
            }

            int bufferIndex = (index % m_frameBuffer.Length);
            ClientSession session = m_frameBuffer[bufferIndex].Session;
            session.SuccessHistory <<= 1;
            long latency = m_watch.ElapsedMilliseconds - m_frameBuffer[bufferIndex].IssueTimestamp;
            session.DelayCounter += latency;
            session.FrameCounter += 1;

            long start = m_watch.ElapsedTicks;
            //UnityEngine.Debug.Log(data.Length);
            bool decodeSuccess = session.Decoder.Decode(new ReadOnlySpan<byte>(data, sizeof(int), data.Length - sizeof(int)), m_frameBuffer[bufferIndex].TextureData);
            m_decodeDelayCounter += 1000L * (m_watch.ElapsedTicks - start) / Stopwatch.Frequency;
            m_framesDecoded += 1;

            if (decodeSuccess)
            {
                m_frameBuffer[bufferIndex].State = FrameState.Ready;
                session.SuccessHistory |= 0x1L;
            }
            else
            {
                m_frameBuffer[bufferIndex].State = FrameState.Failed;
            }
        }
    }

    void SendLoop()
    {
        AutoResetEvent nextFrame = new AutoResetEvent(false);
        int interval = 1000 / m_frameRate;
        Timer timer = new Timer(Timeup, nextFrame, interval, interval);

        int currentSessionIndex = 0;
        Task sendTask = null;
        while (true)
        {
            nextFrame.WaitOne();
            if (m_token.IsCancellationRequested)
            {
                break;
            }

            int bufferIndex = m_currentFrame % m_frameBuffer.Length;
            m_frameBuffer[bufferIndex].State = FrameState.Idle;

            if (sendTask == null || sendTask.IsCompleted)
            {
                if (m_sessions.Count > 0 && m_sceneDataLength > 0)
                {
                    currentSessionIndex = (currentSessionIndex + 1) % m_sessions.Count;
                    ClientSession session = m_sessions[currentSessionIndex];

                    m_frameBuffer[bufferIndex].IssueTimestamp = m_watch.ElapsedMilliseconds;
                    m_frameBuffer[bufferIndex].Session = session;
                    m_frameBuffer[bufferIndex].State = FrameState.Pending;

                    
                    byte[] data = new byte[m_sceneDataLength + sizeof(int) * 2 + 4];
                    Array.Copy(BitConverter.GetBytes(session.Id), 0, data, 0, sizeof(int));
                    Array.Copy(BitConverter.GetBytes(m_currentFrame), 0, data, sizeof(int), sizeof(int));
                    Array.Copy(m_sceneDataBuffer, 0, data, sizeof(int) * 2, m_sceneDataLength);
                    Crc32CAlgorithm.ComputeAndWriteToEnd(data);
                    sendTask = session.Client.Send(data);
                    m_uploadBytes += data.Length;

                    _ = DelayUpdate(bufferIndex);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("Send operation too slow. Dropping frame request.");
            }

            m_currentFrame += 1;
        }
    }

    async Task DelayUpdate(int bufferIndex)
    {
        await Task.Delay(m_delayMilliseconds);
        if (m_token.IsCancellationRequested)
        {
            return;
        }

        m_onTimeHistory <<= 1;

        ClientSession session = m_frameBuffer[bufferIndex].Session;
        session.OnTimeHistory <<= 1;

        switch (m_frameBuffer[bufferIndex].State)
        {
            case FrameState.Idle:
                UnityEngine.Debug.LogWarning("Delay may be too long.");
                break;
            case FrameState.Pending:
                break;
            case FrameState.Ready:
                m_onTimeHistory |= 0x1L;
                session.OnTimeHistory |= 0x1L;
                m_nextData = m_frameBuffer[bufferIndex].TextureData;
                m_framesUpdated += 1;
                break;
            case FrameState.Failed:
                m_onTimeHistory |= 0x1L;
                session.OnTimeHistory |= 0x1L;
                break;
            default:
                break;
        }

        m_frameCounter -= 1;
        if (m_frameCounter <= 0)
        {
            m_frameCounter += m_delayAdjustPeriod;
            m_onTimeRate = CalculateRate(m_onTimeHistory, m_historyLength);
            if (m_onTimeRate < m_lengthenDelayThreshold)
            {
                m_delayMilliseconds += m_delayIncrementMilliseconds;
            }
            else if (m_onTimeRate > m_shortenDelayThreshold && m_delayMilliseconds > m_satisfyingDelayMilliseconds)
            {
                m_delayMilliseconds -= m_delayIncrementMilliseconds;
            }
        }

        m_delayMilliseconds = Mathf.Clamp(m_delayMilliseconds, 0, m_maxDelayMilliseconds);
    }

    float CalculateRate(long history, int bits)
    {
        long ones = 0;
        for (int i = 0; i < bits; i++)
        {
            ones += (history >> i) & 0x1L;
        }
        return (float)ones / bits;
    }
}
