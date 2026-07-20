using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Sessions;
using Microsoft.AspNetCore.SignalR;

namespace BankersSeat.Server.Realtime;

public sealed class SignalRSessionEventBroadcaster : ISessionEventBroadcaster
{
    private readonly IHubContext<GameHub> hubContext;

    public SignalRSessionEventBroadcaster(IHubContext<GameHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    public Task BroadcastSessionSnapshotAsync(
        Guid sessionId,
        SessionSnapshotResponse snapshot,
        CancellationToken cancellationToken
    )
    {
        return hubContext.Clients
            .Group(sessionId.ToString("D"))
            .SendAsync("SessionSnapshot", snapshot, cancellationToken);
    }
}
