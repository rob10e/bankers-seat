namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record HealthVersionResponse(
    string ApplicationVersion,
    int TemplateSchemaVersion,
    string Status,
    DateTimeOffset CheckedAtUtc
);
