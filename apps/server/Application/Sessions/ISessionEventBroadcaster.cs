using BankersSeat.Server.Api.V1.Contracts;

namespace BankersSeat.Server.Application.Sessions;

public interface ISessionEventBroadcaster
{
    Task BroadcastSessionSnapshotAsync(
        Guid sessionId,
        SessionSnapshotResponse snapshot,
        CancellationToken cancellationToken
    );
}

public sealed class NullSessionEventBroadcaster : ISessionEventBroadcaster
{
    public static readonly NullSessionEventBroadcaster Instance = new();

    private NullSessionEventBroadcaster()
    {
    }

    public Task BroadcastSessionSnapshotAsync(
        Guid sessionId,
        SessionSnapshotResponse snapshot,
        CancellationToken cancellationToken
    )
    {
        _ = sessionId;
        _ = snapshot;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
