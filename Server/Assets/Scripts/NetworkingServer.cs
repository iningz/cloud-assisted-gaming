using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using UnityEngine;

public class NetworkingServer : IDisposable
{
    public struct ClientRequest
    {
        public IPEndPoint RemoteEP;
        public byte[] Data;
    }

    public struct SendRequest
    {
        public IPEndPoint ClientEP;
        public byte[] Data;
    }

    class ClientBuffer
    {
        public int Pos;
        public byte[] Data;
    }

    const int BUFFER_SIZE = 5 * 1024 * 1024;
    const int CHANNEL_SIZE = 16;

    readonly int m_mtu;

    CancellationTokenSource m_cts;
    CancellationToken m_token;

    UdpClient m_udpClient;

    Dictionary<int, ClientBuffer> m_bufferTable;

    Channel<ClientRequest> m_clientChannel;
    Channel<SendRequest> m_sendChannel;

    public ChannelReader<ClientRequest> Reader => m_clientChannel.Reader;
    public ChannelWriter<SendRequest> Writer => m_sendChannel.Writer;

    public NetworkingServer(int mtu, int port)
    {
        m_mtu = mtu;

        m_cts = new CancellationTokenSource();
        m_token = m_cts.Token;

        m_udpClient = new UdpClient(port);

        m_clientChannel = Channel.CreateBounded<ClientRequest>(CHANNEL_SIZE);
        m_sendChannel = Channel.CreateBounded<SendRequest>(CHANNEL_SIZE);

        m_bufferTable = new Dictionary<int, ClientBuffer>();
        new Thread(Produce).Start();
        new Thread(Consume).Start();

        Debug.Log($"Working on port {(m_udpClient.Client.LocalEndPoint as IPEndPoint).Port}");
    }

    public void Dispose()
    {
        m_cts.Cancel();

        m_bufferTable.Clear();

        m_sendChannel.Writer.Complete();
        m_clientChannel.Writer.Complete();

        m_udpClient.Close();
        m_udpClient.Dispose();
        m_cts.Dispose();
    }

    void Produce()
    {
        while (true)
        {
            IPEndPoint remoteEP = null;
            byte[] data = null;
            try
            {
                data = m_udpClient.Receive(ref remoteEP);
            }
            catch (Exception) { }

            if (m_token.IsCancellationRequested)
            {
                break;
            }

            if (data != null && data.Length > 1)
            {
                //Debug.Log($"Receiving from {remoteEP}, hash {remoteEP.GetHashCode()}");
                if (!m_bufferTable.TryGetValue(remoteEP.GetHashCode(), out ClientBuffer buffer))
                {
                    buffer = new ClientBuffer();
                    buffer.Pos = 0;
                    buffer.Data = new byte[BUFFER_SIZE];
                    m_bufferTable.Add(remoteEP.GetHashCode(), buffer);
                }

                if (buffer.Pos + data.Length > BUFFER_SIZE)
                {
                    Debug.LogError("Buffer size not enough. Discarding packet.");
                    buffer.Pos = 0;
                    continue;
                }

                if ((data[0] & 0x01) != 0x00)
                {
                    //might drop data
                    buffer.Pos = 0;
                }

                Array.Copy(data, 1, buffer.Data, buffer.Pos, data.Length - 1);
                buffer.Pos += data.Length - 1;

                if ((data[0] & 0x02) != 0x00)
                {
                    //received
                    byte[] toProduce = new byte[buffer.Pos];
                    Array.Copy(buffer.Data, 0, toProduce, 0, buffer.Pos);
                    if (!m_clientChannel.Writer.TryWrite(new ClientRequest { RemoteEP = remoteEP, Data = toProduce }))
                    {
                        Debug.LogWarning("ClientChannel Full");
                    }
                    buffer.Pos = 0;
                }
            }
        }
    }

    void Consume()
    {
        SendRequest request;
        while (true)
        {
            if (m_sendChannel.Reader.TryRead(out request))
            {
                SendTo(request.Data, request.ClientEP).Wait();
            }

            if (m_token.IsCancellationRequested)
            {
                break;
            }
        }
    }

    async Task SendTo(byte[] data, IPEndPoint remoteEP)
    {
        if (data == null || data.Length == 0)
        {
            return;
        }

        int fullFrames = data.Length / m_mtu;
        int bytesLeft = data.Length % m_mtu;
        if (bytesLeft == 0)
        {
            fullFrames -= 1;
            bytesLeft += m_mtu;
        }

        int bytesSent = 0;
        if (fullFrames > 0)
        {
            byte[] fullFrame = new byte[m_mtu + 1];
            fullFrame[0] = 0x01;
            for (int i = 0; i < fullFrames; i++)
            {
                Array.Copy(data, bytesSent, fullFrame, 1, m_mtu);
                if (await m_udpClient.SendAsync(fullFrame, m_mtu + 1, remoteEP) != m_mtu + 1)
                {
                    throw new Exception("Bytes sent error");
                }
                fullFrame[0] = 0x00;
                bytesSent += m_mtu;
            }
        }

        byte[] frame = new byte[bytesLeft + 1];
        frame[0] = (byte)(fullFrames == 0 ? 0x03 : 0x02);
        Array.Copy(data, bytesSent, frame, 1, bytesLeft);
        if (await m_udpClient.SendAsync(frame, bytesLeft + 1, remoteEP) != bytesLeft + 1)
        {
            throw new Exception("Bytes sent error");
        }
        bytesSent += bytesLeft;
    }
}
