using System.Threading.Tasks;
using UnityEngine;
using OpenSession;
using Grpc.Core;

public class OpenSessionService : OpenSession.OpenSession.OpenSessionBase
{
    readonly RenderServer m_renderServer;

    public OpenSessionService(RenderServer renderServer)
    {
        m_renderServer = renderServer;
    }

    public override Task<SessionInfo> Request(SchedulerRequest request, ServerCallContext context)
    {
        Debug.LogWarning($"Received request: x = {request.ResX}, y = {request.ResY}, version = {request.Version}");
        if (m_renderServer.StartSession(new Vector2Int(request.ResX, request.ResY), request.Version, out int id))
        {
            return Task.FromResult(new SessionInfo { Status = 0, SessionId = id });
        }
        else
        {
            return Task.FromResult(new SessionInfo { Status = 1 });
        }
    }
}
