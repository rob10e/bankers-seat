namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record HealthLiveResponse(
    string Status,
    DateTimeOffset CheckedAtUtc
);
