using BankersSeat.Server.Application.Sessions;
using Microsoft.AspNetCore.SignalR;

namespace BankersSeat.Server.Realtime;

public sealed class GameHub : Hub
{
    private readonly ISessionService sessionService;

    public GameHub(ISessionService sessionService)
    {
        this.sessionService = sessionService;
    }

    public async Task SubscribeSession(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await sessionService.GetAuthorizedSnapshotAsync(
            sessionId,
            participantId,
            reconnectCredential,
            cancellationToken
        );
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString("D"), cancellationToken);
        await Clients.Caller.SendAsync("SessionSnapshot", snapshot, cancellationToken);
    }

    public async Task RequestResync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await sessionService.GetAuthorizedSnapshotAsync(
            sessionId,
            participantId,
            reconnectCredential,
            cancellationToken
        );
        await Clients.Caller.SendAsync("SessionSnapshot", snapshot, cancellationToken);
    }
}
