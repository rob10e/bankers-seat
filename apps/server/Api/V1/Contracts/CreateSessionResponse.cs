namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record CreateSessionResponse(
    Guid SessionId,
    string RoomCode,
    Guid HostParticipantId,
    string ReconnectCredential,
    SessionSnapshotResponse InitialSnapshot,
    SessionConnectionInfoResponse Connection
);
