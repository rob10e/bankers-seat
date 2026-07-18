using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankersSeat.Server.Domain.Templates;

namespace BankersSeat.Server.Application.Templates;

public sealed class FileTemplateCatalogService : ITemplateCatalogService
{
    private readonly string templatesRoot;

    public FileTemplateCatalogService(string templatesRoot)
    {
        this.templatesRoot = templatesRoot;
    }

    public async Task<IReadOnlyList<TemplateCatalogEntry>> GetCatalogAsync(
        CancellationToken cancellationToken
    )
    {
        var templateFiles = DiscoverTemplateFiles();
        var catalogEntries = new List<TemplateCatalogEntry>();

        foreach (var filePath in templateFiles)
        {
            var parsed = await TryParseTemplateAsync(filePath, cancellationToken);
            if (parsed is null)
            {
                continue;
            }

            catalogEntries.Add(parsed.Value.Entry);
        }

        return catalogEntries;
    }

    public async Task<TemplateSnapshot?> GetTemplateSnapshotAsync(
        string templateId,
        string editionId,
        string templateVersion,
        CancellationToken cancellationToken
    )
    {
        var templateFiles = DiscoverTemplateFiles();
        foreach (var filePath in templateFiles)
        {
            var parsed = await TryParseTemplateAsync(filePath, cancellationToken);
            if (parsed is null)
            {
                continue;
            }

            if (
                !string.Equals(parsed.Value.Entry.Identity.TemplateId, templateId, StringComparison.Ordinal)
                || !string.Equals(parsed.Value.Entry.Identity.EditionId, editionId, StringComparison.Ordinal)
                || !string.Equals(parsed.Value.Entry.Identity.TemplateVersion, templateVersion, StringComparison.Ordinal)
            )
            {
                continue;
            }

            return parsed.Value.Snapshot;
        }

        return null;
    }

    private IEnumerable<string> DiscoverTemplateFiles()
    {
        if (!Directory.Exists(templatesRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(
            templatesRoot,
            "template.json",
            SearchOption.AllDirectories
        );
    }

    private async Task<(TemplateCatalogEntry Entry, TemplateSnapshot Snapshot)?> TryParseTemplateAsync(
        string templatePath,
        CancellationToken cancellationToken
    )
    {
        await using var stream = File.OpenRead(templatePath);
        JsonDocument jsonDocument;
        try
        {
            jsonDocument = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken
            );
        }
        catch (JsonException)
        {
            return null;
        }

        using (jsonDocument)
        {
            var root = jsonDocument.RootElement;
            if (
                !TryReadString(root, "templateId", out var templateId)
                || !TryReadString(root, "templateVersion", out var templateVersion)
                || !TryReadString(root, "name", out var name)
                || !TryReadObject(root, "edition", out var edition)
                || !TryReadString(edition, "id", out var editionId)
                || !TryReadString(edition, "name", out var editionName)
                || !TryReadObject(root, "playerCount", out var playerCount)
                || !TryReadInt32(playerCount, "minimum", out var minimumPlayers)
                || !TryReadInt32(playerCount, "maximum", out var maximumPlayers)
                || !TryReadInt32(root, "schemaVersion", out var schemaVersion)
                || !TryReadObject(root, "bank", out var bank)
                || !TryReadInt64(bank, "startingPlayerBalance", out var startingPlayerBalance)
                || !TryReadBoolean(bank, "allowPlayerOverdraft", out var allowPlayerOverdraft)
            )
            {
                return null;
            }

            var description = TryReadString(root, "description", out var descriptionValue)
                ? descriptionValue
                : string.Empty;
            var tags = TryReadStringArray(root, "tags", out var tagValues)
                ? tagValues
                : [];
            var identity = new TemplateIdentity(templateId, editionId, templateVersion);
            var discoveredAtUtc = DateTimeOffset.UtcNow;
            var normalizedJson = JsonSerializer.Serialize(root);
            var hash = ComputeSha256(normalizedJson);
            var snapshot = new TemplateSnapshot(
                Guid.NewGuid(),
                identity,
                schemaVersion,
                hash,
                normalizedJson,
                startingPlayerBalance,
                allowPlayerOverdraft,
                discoveredAtUtc
            );
            var entry = new TemplateCatalogEntry(
                identity,
                name,
                editionName,
                description,
                minimumPlayers,
                maximumPlayers,
                tags,
                schemaVersion,
                discoveredAtUtc
            );

            return (entry, snapshot);
        }
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool TryReadObject(
        JsonElement source,
        string propertyName,
        out JsonElement value
    )
    {
        if (
            !source.TryGetProperty(propertyName, out value)
            || value.ValueKind != JsonValueKind.Object
        )
        {
            value = default;
            return false;
        }

        return true;
    }

    private static bool TryReadString(
        JsonElement source,
        string propertyName,
        out string value
    )
    {
        if (
            !source.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString())
        )
        {
            value = string.Empty;
            return false;
        }

        value = property.GetString()!;
        return true;
    }

    private static bool TryReadBoolean(
        JsonElement source,
        string propertyName,
        out bool value
    )
    {
        if (
            !source.TryGetProperty(propertyName, out var property)
            || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
        )
        {
            value = default;
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryReadInt32(JsonElement source, string propertyName, out int value)
    {
        if (
            !source.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out value)
        )
        {
            value = default;
            return false;
        }

        return true;
    }

    private static bool TryReadInt64(JsonElement source, string propertyName, out long value)
    {
        if (
            !source.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt64(out value)
        )
        {
            value = default;
            return false;
        }

        return true;
    }

    private static bool TryReadStringArray(
        JsonElement source,
        string propertyName,
        out IReadOnlyList<string> values
    )
    {
        if (
            !source.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array
        )
        {
            values = [];
            return false;
        }

        var list = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                continue;
            }

            list.Add(item.GetString()!);
        }

        values = list;
        return true;
    }
}
