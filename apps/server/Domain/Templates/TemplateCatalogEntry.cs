namespace BankersSeat.Server.Domain.Templates;

public sealed record TemplateCatalogEntry(
    TemplateIdentity Identity,
    string Name,
    string EditionName,
    string Description,
    int MinimumPlayers,
    int MaximumPlayers,
    IReadOnlyList<string> Tags,
    int SchemaVersion,
    DateTimeOffset DiscoveredAtUtc
);
