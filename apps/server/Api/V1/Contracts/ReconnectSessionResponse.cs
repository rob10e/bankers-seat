namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record ReconnectSessionResponse(
    Guid SessionId,
    Guid ParticipantId,
    string ReconnectCredential,
    SessionSnapshotResponse Snapshot
);
