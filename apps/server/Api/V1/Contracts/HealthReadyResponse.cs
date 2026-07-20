namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record HealthReadyResponse(
    string Status,
    bool DatabaseAvailable,
    int TemplateCatalogCount,
    DateTimeOffset CheckedAtUtc
);
