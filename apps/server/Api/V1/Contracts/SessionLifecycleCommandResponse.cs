namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record SessionLifecycleCommandResponse(
    SessionSnapshotResponse Snapshot,
    bool IdempotentReplay
);
