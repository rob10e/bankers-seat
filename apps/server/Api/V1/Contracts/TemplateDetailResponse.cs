using System.Text.Json;

namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record TemplateDetailResponse(
    string TemplateId,
    string EditionId,
    string TemplateVersion,
    string Name,
    string EditionName,
    string Description,
    int MinimumPlayers,
    int MaximumPlayers,
    IReadOnlyList<string> Tags,
    int SchemaVersion,
    string ContentHash,
    long StartingPlayerBalance,
    bool AllowPlayerOverdraft,
    JsonElement Template
);
