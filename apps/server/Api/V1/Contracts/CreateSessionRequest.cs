using System.Text.Json;

namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record CreateSessionRequest(
    string TemplateId,
    string EditionId,
    string TemplateVersion,
    string HostDisplayName,
    IReadOnlyDictionary<string, JsonElement> SessionOptions
);
