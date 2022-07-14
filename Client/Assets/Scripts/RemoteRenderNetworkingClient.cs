using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using UnityEngine;

public class RemoteRenderNetworkingClient : IDisposable
{
    const int BUFFER_SIZE = 5 * 1024 * 1024;

    UdpClient m_udpClient;
    int m_mtu;

    CancellationTokenSource m_cts;
    CancellationToken m_token;
    IPEndPoint m_remoteEP;

    ChannelWriter<byte[]> m_writer;

    public IPEndPoint RemoteEP => m_remoteEP;

    public RemoteRenderNetworkingClient(IPEndPoint remoteEP, int mtu, ChannelWriter<byte[]> writer)
    {
        m_udpClient = new UdpClient();
        m_mtu = mtu;
        m_writer = writer;
        m_cts = new CancellationTokenSource();
        m_token = m_cts.Token;
        m_remoteEP = remoteEP;

        m_udpClient.Connect(remoteEP);

        new Thread(ReceiveLoop).Start();
    }

    public void Dispose()
    {
        m_cts.Cancel();
        m_cts.Dispose();
        m_udpClient.Close();
        m_udpClient.Dispose();
    }

    void ReceiveLoop()
    {
        byte[] buffer = new byte[BUFFER_SIZE];
        int pos = 0;
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
                if (pos + data.Length > BUFFER_SIZE)
                {
                    Debug.LogError("Buffer size not enough. Discarding packet.");
                    pos = 0;
                    continue;
                }

                if ((data[0] & 0x01) != 0x00)
                {
                    //might drop data
                    pos = 0;
                }

                Array.Copy(data, 1, buffer, pos, data.Length - 1);
                pos += data.Length - 1;

                if ((data[0] & 0x02) != 0x00)
                {
                    //received
                    byte[] received = new byte[pos];
                    Array.Copy(buffer, 0, received, 0, pos);
                    if (!m_writer.TryWrite(received))
                    {
                        Debug.LogWarning("ReceiveChannel Full");
                    }
                    pos = 0;
                }
            }
        }
    }

    public async Task Send(byte[] data)
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
                if (await m_udpClient.SendAsync(fullFrame, m_mtu + 1) != m_mtu + 1)
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
        if (await m_udpClient.SendAsync(frame, bytesLeft + 1) != bytesLeft + 1)
        {
            throw new Exception("Bytes sent error");
        }
        bytesSent += bytesLeft;
    }
}
