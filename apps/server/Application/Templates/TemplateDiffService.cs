using System.Text.Json;
using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Domain.Templates;

namespace BankersSeat.Server.Application.Templates;

/// <summary>
/// Service for computing template differences and detecting breaking changes.
/// Used for template versioning, migration guidance, and changelog generation.
/// </summary>
public interface ITemplateDiffService
{
    /// <summary>
    /// Compute the diff between two template versions.
    /// Returns breaking changes, new features, and migration advice.
    /// </summary>
    Task<TemplateDiffResponse> DiffTemplatesAsync(
        TemplateSnapshot from,
        TemplateSnapshot to,
        CancellationToken cancellationToken);
}

/// <summary>
/// Implementation of template diff service.
/// Analyzes JSON structure changes and identifies compatibility issues.
/// </summary>
public sealed class TemplateDiffService : ITemplateDiffService
{
    private readonly ILogger<TemplateDiffService> _logger;

    public TemplateDiffService(ILogger<TemplateDiffService> logger)
    {
        _logger = logger;
    }

    public Task<TemplateDiffResponse> DiffTemplatesAsync(
        TemplateSnapshot from,
        TemplateSnapshot to,
        CancellationToken cancellationToken)
    {
        try
        {
            var fromDoc = JsonDocument.Parse(from.TemplateJson);
            var toDoc = JsonDocument.Parse(to.TemplateJson);

            var breakingChanges = new List<string>();
            var newFeatures = new List<string>();
            var removedFeatures = new List<string>();
            var changedFields = new List<string>();

            var fromRoot = fromDoc.RootElement;
            var toRoot = toDoc.RootElement;

            // Compare bank configuration
            CompareBankConfig(fromRoot, toRoot, breakingChanges, changedFields);

            // Compare denominations
            CompareDenominations(fromRoot, toRoot, breakingChanges, newFeatures, removedFeatures);

            // Compare player fields
            ComparePlayerFields(fromRoot, toRoot, breakingChanges, newFeatures, removedFeatures);

            // Compare actions
            CompareActions(fromRoot, toRoot, newFeatures, removedFeatures);

            // Compare currency
            CompareCurrency(fromRoot, toRoot, breakingChanges);

            bool compatibleUpgrade = breakingChanges.Count == 0;

            var migrationAdvice = GenerateMigrationAdvice(from, to, breakingChanges, newFeatures);
            var changelog = GenerateChangelog(breakingChanges, newFeatures, removedFeatures, changedFields);

            _logger.LogInformation(
                "Diff computed: {FromVersion} → {ToVersion}, Compatible: {CompatibleUpgrade}",
                from.Identity.TemplateVersion,
                to.Identity.TemplateVersion,
                compatibleUpgrade);

            var response = new TemplateDiffResponse
            {
                CompatibleUpgrade = compatibleUpgrade,
                BreakingChanges = breakingChanges.Count > 0 ? breakingChanges.ToArray() : null,
                NewFeatures = newFeatures.Count > 0 ? newFeatures.ToArray() : null,
                RemovedFeatures = removedFeatures.Count > 0 ? removedFeatures.ToArray() : null,
                ChangedFields = changedFields.Count > 0 ? changedFields.ToArray() : null,
                MigrationAdvice = migrationAdvice,
                Changelog = changelog
            };

            return Task.FromResult(response);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing template JSON during diff");
            throw;
        }
    }

    private static void CompareBankConfig(
        JsonElement fromRoot,
        JsonElement toRoot,
        List<string> breakingChanges,
        List<string> changedFields)
    {
        if (!fromRoot.TryGetProperty("bank", out var fromBank) ||
            !toRoot.TryGetProperty("bank", out var toBank))
        {
            return;
        }

        // Check for startingPlayerBalance change
        if (fromBank.TryGetProperty("startingPlayerBalance", out var fromBalance) &&
            toBank.TryGetProperty("startingPlayerBalance", out var toBalance) &&
            fromBalance.GetInt64() != toBalance.GetInt64())
        {
            changedFields.Add(
                $"Starting balance changed: {fromBalance.GetInt64()} → {toBalance.GetInt64()}");
        }

        // Check for bank mode change (unlimited vs finite)
        if (fromBank.TryGetProperty("bankMode", out var fromMode) &&
            toBank.TryGetProperty("bankMode", out var toMode) &&
            fromMode.GetString() != toMode.GetString())
        {
            breakingChanges.Add(
                $"Bank mode changed: {fromMode.GetString()} → {toMode.GetString()}. Sessions may need reconfiguration.");
        }

        // Check for overdraft policy change
        if (fromBank.TryGetProperty("allowPlayerOverdraft", out var fromOverdraft) &&
            toBank.TryGetProperty("allowPlayerOverdraft", out var toOverdraft) &&
            fromOverdraft.GetBoolean() != toOverdraft.GetBoolean())
        {
            breakingChanges.Add(
                $"Overdraft policy changed: {(fromOverdraft.GetBoolean() ? "allowed" : "prohibited")} → {(toOverdraft.GetBoolean() ? "allowed" : "prohibited")}. May affect ongoing sessions.");
        }
    }

    private static void CompareDenominations(
        JsonElement fromRoot,
        JsonElement toRoot,
        List<string> breakingChanges,
        List<string> newFeatures,
        List<string> removedFeatures)
    {
        var fromDenoms = ExtractArray(fromRoot, "denominations");
        var toDenoms = ExtractArray(toRoot, "denominations");

        var fromDenomSet = new HashSet<string>(fromDenoms.Select(d => d.GetProperty("value").GetInt32().ToString()));
        var toDenomSet = new HashSet<string>(toDenoms.Select(d => d.GetProperty("value").GetInt32().ToString()));

        foreach (var removed in fromDenomSet.Except(toDenomSet))
        {
            removedFeatures.Add($"Denomination removed: {removed}");
            breakingChanges.Add($"Denomination {removed} removed. Physical bank components no longer valid.");
        }

        foreach (var added in toDenomSet.Except(fromDenomSet))
        {
            newFeatures.Add($"New denomination: {added}");
        }
    }

    private static void ComparePlayerFields(
        JsonElement fromRoot,
        JsonElement toRoot,
        List<string> breakingChanges,
        List<string> newFeatures,
        List<string> removedFeatures)
    {
        var fromFields = ExtractArray(fromRoot, "playerFields");
        var toFields = ExtractArray(toRoot, "playerFields");

        var fromFieldIds = new HashSet<string>(
            fromFields.Where(f => f.TryGetProperty("id", out _))
                .Select(f => f.GetProperty("id").GetString() ?? ""));

        var toFieldIds = new HashSet<string>(
            toFields.Where(f => f.TryGetProperty("id", out _))
                .Select(f => f.GetProperty("id").GetString() ?? ""));

        foreach (var removedId in fromFieldIds.Except(toFieldIds))
        {
            removedFeatures.Add($"Player field removed: {removedId}");
            breakingChanges.Add($"Player field '{removedId}' removed. Ongoing sessions will lose this tracked state.");
        }

        foreach (var addedId in toFieldIds.Except(fromFieldIds))
        {
            newFeatures.Add($"New player field: {addedId}");
        }
    }

    private static void CompareActions(
        JsonElement fromRoot,
        JsonElement toRoot,
        List<string> newFeatures,
        List<string> removedFeatures)
    {
        var fromActions = ExtractArray(fromRoot, "actions");
        var toActions = ExtractArray(toRoot, "actions");

        var fromActionIds = new HashSet<string>(
            fromActions.Where(a => a.TryGetProperty("id", out _))
                .Select(a => a.GetProperty("id").GetString() ?? ""));

        var toActionIds = new HashSet<string>(
            toActions.Where(a => a.TryGetProperty("id", out _))
                .Select(a => a.GetProperty("id").GetString() ?? ""));

        foreach (var removedId in fromActionIds.Except(toActionIds))
        {
            removedFeatures.Add($"Action removed: {removedId}");
        }

        foreach (var addedId in toActionIds.Except(fromActionIds))
        {
            newFeatures.Add($"New action: {addedId}");
        }
    }

    private static void CompareCurrency(
        JsonElement fromRoot,
        JsonElement toRoot,
        List<string> breakingChanges)
    {
        if (!fromRoot.TryGetProperty("currency", out var fromCurrency) ||
            !toRoot.TryGetProperty("currency", out var toCurrency))
        {
            return;
        }

        // Check for base unit change
        if (fromCurrency.TryGetProperty("fractionDigits", out var fromFractions) &&
            toCurrency.TryGetProperty("fractionDigits", out var toFractions) &&
            fromFractions.GetInt32() != toFractions.GetInt32())
        {
            breakingChanges.Add(
                $"Currency precision changed: {fromFractions.GetInt32()} → {toFractions.GetInt32()} digits. Existing balance data may be incompatible.");
        }
    }

    private static string GenerateMigrationAdvice(
        TemplateSnapshot from,
        TemplateSnapshot to,
        List<string> breakingChanges,
        List<string> newFeatures)
    {
        var lines = new List<string>();

        if (breakingChanges.Count == 0)
        {
            lines.Add("This is a backward-compatible upgrade. Active sessions can safely update.");
        }
        else
        {
            lines.Add("⚠️ BREAKING CHANGES DETECTED");
            lines.Add("");
            lines.Add("Active sessions using the previous version should:");
            lines.Add("1. Complete all in-progress transactions");
            lines.Add("2. Export or archive the current session state");
            lines.Add("3. Start new sessions with the updated template");
            lines.Add("");
            lines.AddRange(breakingChanges.Select(bc => $"  • {bc}"));
        }

        if (newFeatures.Count > 0)
        {
            lines.Add("");
            lines.Add("New features in this version:");
            lines.AddRange(newFeatures.Select(nf => $"  ✓ {nf}"));
        }

        return string.Join("\n", lines);
    }

    private static string GenerateChangelog(
        List<string> breakingChanges,
        List<string> newFeatures,
        List<string> removedFeatures,
        List<string> changedFields)
    {
        var lines = new List<string>();

        if (breakingChanges.Count > 0)
        {
            lines.Add("## Breaking Changes");
            lines.AddRange(breakingChanges.Select(bc => $"- {bc}"));
            lines.Add("");
        }

        if (newFeatures.Count > 0)
        {
            lines.Add("## New Features");
            lines.AddRange(newFeatures.Select(nf => $"- {nf}"));
            lines.Add("");
        }

        if (removedFeatures.Count > 0)
        {
            lines.Add("## Removed Features");
            lines.AddRange(removedFeatures.Select(rf => $"- {rf}"));
            lines.Add("");
        }

        if (changedFields.Count > 0)
        {
            lines.Add("## Modified");
            lines.AddRange(changedFields.Select(cf => $"- {cf}"));
            lines.Add("");
        }

        return string.Join("\n", lines);
    }

    private static JsonElement[] ExtractArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<JsonElement>();
        }

        return array.EnumerateArray().ToArray();
    }
}
