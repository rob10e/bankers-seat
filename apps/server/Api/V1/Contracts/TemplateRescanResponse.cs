namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record TemplateRescanResponse(
    int DiscoveredTemplateCount,
    DateTimeOffset RescanCompletedAtUtc,
    DateTimeOffset LastScannedAtUtc
);
