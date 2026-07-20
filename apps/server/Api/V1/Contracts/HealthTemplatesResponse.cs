namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record HealthTemplatesResponse(
    string Status,
    int ValidTemplateCount,
    int InvalidTemplateCount,
    DateTimeOffset CatalogScannedAtUtc,
    DateTimeOffset CheckedAtUtc
);
