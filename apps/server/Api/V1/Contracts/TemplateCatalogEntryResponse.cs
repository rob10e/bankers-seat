namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record TemplateCatalogEntryResponse(
    string TemplateId,
    string EditionId,
    string TemplateVersion,
    string Name,
    string EditionName,
    string Description,
    int MinimumPlayers,
    int MaximumPlayers,
    IReadOnlyList<string> Tags,
    string ValidationStatus,
    string SourceType
);
