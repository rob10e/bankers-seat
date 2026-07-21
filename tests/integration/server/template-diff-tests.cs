using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Domain.Templates;
using Microsoft.Extensions.Logging;
using Xunit;
using System.Text.Json;

namespace BankersSeat.Server.Integration.Tests.Templates;

/// <summary>
/// Integration tests for TemplateDiffService.
/// Tests diff computation, breaking change detection, and migration advice generation.
/// </summary>
public class TemplateDiffServiceTests
{
    private readonly ITemplateDiffService _diffService;

    public TemplateDiffServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<TemplateDiffService>();
        _diffService = new TemplateDiffService(logger);
    }

    private TemplateSnapshot CreateSnapshot(string templateJson, string version = "1.0.0")
    {
        return new TemplateSnapshot(
            Id: Guid.NewGuid(),
            Identity: new TemplateIdentity("test-game", "classic", version),
            SchemaVersion: 1,
            ContentHash: HashString(templateJson),
            TemplateJson: templateJson,
            StartingPlayerBalance: 1500,
            AllowPlayerOverdraft: false,
            CreatedAtUtc: DateTime.UtcNow
        );
    }

    private static string HashString(string input) => 
        Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)));

    private string BuildTemplateJson(
        long startingBalance = 1500,
        string bankMode = "unlimited",
        bool allowOverdraft = false,
        bool includeDenominations = false,
        bool includePlayerFields = false,
        bool includeActions = false)
    {
        var template = new
        {
            schemaVersion = 1,
            templateId = "test-game",
            templateVersion = "1.0.0",
            name = "Test Game",
            edition = new
            {
                id = "classic",
                name = "Classic",
                year = 2024
            },
            playerCount = new { min = 2, max = 6 },
            currency = new
            {
                code = "TEST",
                symbol = "T",
                name = "Tests",
                baseUnitName = "test",
                fractionDigits = 0
            },
            bank = new
            {
                startingPlayerBalance = startingBalance,
                bankMode = bankMode,
                allowPlayerOverdraft = allowOverdraft
            },
            denominations = includeDenominations ? new[]
            {
                new { value = 1, label = "1" },
                new { value = 5, label = "5" },
                new { value = 10, label = "10" }
            } : null,
            playerFields = includePlayerFields ? new[]
            {
                new { id = "home", label = "Home", type = "text" },
                new { id = "family", label = "Family", type = "number" }
            } : null,
            actions = includeActions ? new[]
            {
                new { id = "pay-debt", label = "Pay Debt", actionType = "transfer" }
            } : null
        };

        return JsonSerializer.Serialize(template);
    }

    [Fact]
    public async Task DiffTemplatesAsync_NoChanges_ReturnsCompatibleUpgrade()
    {
        // Arrange
        var json = BuildTemplateJson();
        var from = CreateSnapshot(json, "1.0.0");
        var to = CreateSnapshot(json, "1.0.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.True(diff.CompatibleUpgrade);
        Assert.Null(diff.BreakingChanges);
        Assert.Null(diff.NewFeatures);
        Assert.Null(diff.RemovedFeatures);
        Assert.NotNull(diff.Changelog);
        Assert.Contains("backward-compatible", diff.MigrationAdvice ?? "");
    }

    [Fact]
    public async Task DiffTemplatesAsync_StartingBalanceChanged_ReportsChange()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(startingBalance: 1500), "1.0.0");
        var to = CreateSnapshot(BuildTemplateJson(startingBalance: 2000), "1.1.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.True(diff.CompatibleUpgrade);
        Assert.NotNull(diff.ChangedFields);
        Assert.Single(diff.ChangedFields);
        Assert.Contains("1500 → 2000", diff.ChangedFields[0]);
    }

    [Fact]
    public async Task DiffTemplatesAsync_BankModeChanged_BreakingChange()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(bankMode: "unlimited"), "1.0.0");
        var to = CreateSnapshot(BuildTemplateJson(bankMode: "finite"), "2.0.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.False(diff.CompatibleUpgrade);
        Assert.NotNull(diff.BreakingChanges);
        Assert.True(diff.BreakingChanges.Any(bc => bc.Contains("Bank mode changed")));
        Assert.NotNull(diff.MigrationAdvice);
        Assert.Contains("⚠️ BREAKING CHANGES", diff.MigrationAdvice);
    }

    [Fact]
    public async Task DiffTemplatesAsync_OverdraftPolicyChanged_BreakingChange()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(allowOverdraft: false), "1.0.0");
        var to = CreateSnapshot(BuildTemplateJson(allowOverdraft: true), "2.0.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.False(diff.CompatibleUpgrade);
        Assert.NotNull(diff.BreakingChanges);
        Assert.True(diff.BreakingChanges.Any(bc => bc.Contains("Overdraft policy changed")));
    }

    [Fact]
    public async Task DiffTemplatesAsync_DenominationAdded_NewFeature()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(includeDenominations: false), "1.0.0");
        var to = CreateSnapshot(BuildTemplateJson(includeDenominations: true), "1.1.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.True(diff.CompatibleUpgrade);
        Assert.NotNull(diff.NewFeatures);
        Assert.NotEmpty(diff.NewFeatures);
    }

    [Fact]
    public async Task DiffTemplatesAsync_DenominationRemoved_BreakingChange()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(includeDenominations: true), "1.0.0");
        var to = CreateSnapshot(BuildTemplateJson(includeDenominations: false), "2.0.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.False(diff.CompatibleUpgrade);
        Assert.NotNull(diff.BreakingChanges);
        Assert.True(diff.BreakingChanges.Any(bc => bc.Contains("Denomination")));
    }

    [Fact]
    public async Task DiffTemplatesAsync_PlayerFieldRemoved_BreakingChange()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(includePlayerFields: true), "1.0.0");
        var to = CreateSnapshot(BuildTemplateJson(includePlayerFields: false), "2.0.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.False(diff.CompatibleUpgrade);
        Assert.NotNull(diff.BreakingChanges);
        Assert.True(diff.BreakingChanges.Any(bc => bc.Contains("Player field")));
        Assert.True(diff.BreakingChanges.Any(bc => bc.Contains("will lose this tracked state")));
    }

    [Fact]
    public async Task DiffTemplatesAsync_PlayerFieldAdded_NewFeature()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(includePlayerFields: false), "1.0.0");
        var to = CreateSnapshot(BuildTemplateJson(includePlayerFields: true), "1.1.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.True(diff.CompatibleUpgrade);
        Assert.NotNull(diff.NewFeatures);
        Assert.True(diff.NewFeatures.Any(nf => nf.Contains("player field")));
    }

    [Fact]
    public async Task DiffTemplatesAsync_ActionRemoved_RemovedFeature()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(includeActions: true), "1.0.0");
        var to = CreateSnapshot(BuildTemplateJson(includeActions: false), "1.1.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.True(diff.CompatibleUpgrade);
        Assert.NotNull(diff.RemovedFeatures);
        Assert.True(diff.RemovedFeatures.Any(rf => rf.Contains("Action removed")));
    }

    [Fact]
    public async Task DiffTemplatesAsync_ActionAdded_NewFeature()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(includeActions: false), "1.0.0");
        var to = CreateSnapshot(BuildTemplateJson(includeActions: true), "1.1.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.True(diff.CompatibleUpgrade);
        Assert.NotNull(diff.NewFeatures);
        Assert.True(diff.NewFeatures.Any(nf => nf.Contains("New action")));
    }

    [Fact]
    public async Task DiffTemplatesAsync_MultipleChanges_CorrectClassification()
    {
        // Arrange - multiple breaking changes
        var fromJson = BuildTemplateJson(
            startingBalance: 1500,
            bankMode: "unlimited",
            allowOverdraft: false,
            includeDenominations: true,
            includePlayerFields: true);

        var toJson = new
        {
            schemaVersion = 1,
            templateId = "test-game",
            templateVersion = "2.0.0",
            name = "Test Game",
            edition = new
            {
                id = "classic",
                name = "Classic",
                year = 2024
            },
            playerCount = new { min = 2, max = 8 },
            currency = new
            {
                code = "TEST",
                symbol = "T",
                name = "Tests",
                baseUnitName = "test",
                fractionDigits = 0
            },
            bank = new
            {
                startingPlayerBalance = 2000,
                bankMode = "finite",
                allowPlayerOverdraft = true
            },
            denominations = new[]
            {
                new { value = 1, label = "1" },
                new { value = 50, label = "50" } // Changed
            },
            playerFields = (object)null,  // Removed
            actions = new[]
            {
                new { id = "new-action", label = "New", actionType = "transfer" }  // New
            }
        };

        var from = CreateSnapshot(fromJson, "1.0.0");
        var to = CreateSnapshot(JsonSerializer.Serialize(toJson), "2.0.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.False(diff.CompatibleUpgrade);
        Assert.NotNull(diff.BreakingChanges);
        Assert.True(diff.BreakingChanges.Length >= 3);
        Assert.True(diff.BreakingChanges.Any(bc => bc.Contains("Bank mode")));
        Assert.True(diff.BreakingChanges.Any(bc => bc.Contains("Overdraft")));
        Assert.NotNull(diff.Changelog);
        Assert.Contains("Breaking Changes", diff.Changelog);
    }

    [Fact]
    public async Task DiffTemplatesAsync_InvalidJson_ThrowsException()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson());
        var invalidJson = "{ invalid json }";
        var to = new TemplateSnapshot(
            Id: Guid.NewGuid(),
            Identity: new TemplateIdentity("test-game", "classic", "1.1.0"),
            SchemaVersion: 1,
            ContentHash: HashString(invalidJson),
            TemplateJson: invalidJson,
            StartingPlayerBalance: 1500,
            AllowPlayerOverdraft: false,
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act & Assert - Should throw an exception during JSON parsing
        var exception = await Record.ExceptionAsync(async () =>
            await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None));
        Assert.NotNull(exception);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(exception);
    }

    [Fact]
    public async Task DiffTemplatesAsync_ChangelogFormatted_ContainsAllSections()
    {
        // Arrange
        var from = CreateSnapshot(BuildTemplateJson(includeDenominations: true, includePlayerFields: true), "1.0.0");
        var toJson = new
        {
            schemaVersion = 1,
            templateId = "test-game",
            templateVersion = "2.0.0",
            name = "Test Game",
            edition = new
            {
                id = "classic",
                name = "Classic",
                year = 2024
            },
            playerCount = new { min = 2, max = 6 },
            currency = new
            {
                code = "TEST",
                symbol = "T",
                name = "Tests",
                baseUnitName = "test",
                fractionDigits = 0
            },
            bank = new
            {
                startingPlayerBalance = 1500,
                bankMode = "finite",
                allowPlayerOverdraft = false
            },
            denominations = new[]
            {
                new { value = 1, label = "1" }
            },
            playerFields = (object)null,
            actions = new[]
            {
                new { id = "action1", label = "Action", actionType = "transfer" }
            }
        };

        var to = CreateSnapshot(JsonSerializer.Serialize(toJson), "2.0.0");

        // Act
        var diff = await _diffService.DiffTemplatesAsync(from, to, CancellationToken.None);

        // Assert
        Assert.NotNull(diff.Changelog);
        Assert.Contains("Breaking Changes", diff.Changelog);
        Assert.Contains("New Features", diff.Changelog);
        Assert.Contains("Removed Features", diff.Changelog);
    }
}
