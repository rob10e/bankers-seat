namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record JoinSessionResponse(
    Guid SessionId,
    Guid ParticipantId,
    string ReconnectCredential,
    SessionSnapshotResponse Snapshot,
    SessionConnectionInfoResponse Connection
);
