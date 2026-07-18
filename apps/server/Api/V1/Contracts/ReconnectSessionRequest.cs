namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record ReconnectSessionRequest(
    Guid ParticipantId,
    string ReconnectCredential
);
