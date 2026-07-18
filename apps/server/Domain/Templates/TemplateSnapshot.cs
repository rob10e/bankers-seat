namespace BankersSeat.Server.Domain.Templates;

public sealed record TemplateSnapshot(
    Guid Id,
    TemplateIdentity Identity,
    int SchemaVersion,
    string ContentHash,
    string TemplateJson,
    long StartingPlayerBalance,
    bool AllowPlayerOverdraft,
    DateTimeOffset CreatedAtUtc
);
