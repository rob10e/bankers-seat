namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record SessionConnectionInfoResponse(
    string HubPath,
    int ProtocolVersion
);
