namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record JoinSessionRequest(
    string RoomCode,
    string DisplayName,
    string IdentityKey
);
