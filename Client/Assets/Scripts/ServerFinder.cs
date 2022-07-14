using System.Net;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Threading;
using Grpc.Core;
using AssignRenderer;
using System;

public class ServerFinder : IDisposable
{
    readonly Channel m_channel;
    readonly Assignor.AssignorClient m_client;

    readonly List<string> m_blackList = new List<string>();

    public ServerFinder(string host)
    {
        m_channel = new Channel(host, ChannelCredentials.Insecure);
        m_client = new Assignor.AssignorClient(m_channel);
    }

    public void AddToBlackList(IPEndPoint ep)
    {
        m_blackList.Add(ep.ToString());
    }

    public void RemoveFromBlackList(IPEndPoint ep)
    {
        m_blackList.RemoveAll(e => e.Equals(ep.ToString()));
    }
    
    public async Task<(string, int, int)> FindServer(int version, Vector2Int resolution, int timeoutMilliseconds, CancellationToken token)
    {
        //Debug.LogWarning($"Sending request!");
        var request = new ClientRequest { Version = version, ResX = resolution.x, ResY = resolution.y };
        request.ExServers.AddRange(m_blackList);
        try
        {
            var reply = await m_client.RequestAsync(request,
                deadline: DateTime.UtcNow + new TimeSpan(timeoutMilliseconds * 10000),
                cancellationToken: token);
            if (reply.Status != 0)
            {
                return (null, 0, 0);
            }
            //Debug.LogWarning($"Got: {reply.Host}:{reply.Port}, status: {reply.Status}, sessionId: {reply.SessionId}");
            return (reply.Host, reply.Port, reply.SessionId);
        }
        catch (RpcException ex)
        {
            if (ex.StatusCode == StatusCode.DeadlineExceeded)
            {
                Debug.LogWarning("Schedule server timeout.");
            }
            else
            {
                Debug.LogWarning(ex);
            }
        }

        return (null, 0, 0);
    }

    public void Dispose()
    {
        m_channel.ShutdownAsync();
    }
}
