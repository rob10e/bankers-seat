using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Sessions;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BankersSeat.Server.Tests.Integration;

public sealed class SessionScaffoldTests
{
    private static readonly string TemplatesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "templates")
    );
    private readonly FileTemplateCatalogService catalogService = new(TemplatesRoot);

    [Fact]
    public async Task TemplateCatalogContainsSampleTemplates()
    {
        var catalog = await catalogService.GetCatalogAsync(CancellationToken.None);

        Assert.Contains(catalog, template => template.Identity.TemplateId == "generic-property-trading");
        Assert.Contains(catalog, template => template.Identity.TemplateId == "generic-life-journey");
    }

    [Fact]
    public async Task CreateSessionPersistsTemplateSnapshotInSessionView()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new BankersSeatDbContext(dbOptions);
        await dbContext.Database.MigrateAsync();
        var sessionService = new SqliteSessionService(dbContext, catalogService);
        var request = new CreateSessionRequest(
            "generic-property-trading",
            "standard-edition",
            "1.0.0",
            "Rob",
            new Dictionary<string, System.Text.Json.JsonElement>()
        );

        var created = await sessionService.CreateSessionAsync(request, CancellationToken.None);
        var snapshot = await sessionService.GetAuthorizedSnapshotAsync(
            created.SessionId,
            created.HostParticipantId,
            created.ReconnectCredential,
            CancellationToken.None
        );

        Assert.Equal("generic-property-trading", snapshot.Template.TemplateId);
        Assert.Equal("standard-edition", snapshot.Template.EditionId);
        Assert.Equal("1.0.0", snapshot.Template.TemplateVersion);
        Assert.Equal(1, snapshot.SessionVersion);
        Assert.NotEmpty(snapshot.Template.ContentHash);
        Assert.Single(await dbContext.GameSessions.ToListAsync());
        Assert.Single(await dbContext.TemplateSnapshots.ToListAsync());
        Assert.Single(await dbContext.Participants.ToListAsync());
        Assert.Single(await dbContext.Accounts.ToListAsync());
    }

    [Fact]
    public async Task ExistingSessionSnapshotRemainsStableAfterTemplateFileChanges()
    {
        var tempTemplatesRoot = Path.Combine(
            Path.GetTempPath(),
            $"bankers-seat-templates-{Guid.NewGuid():N}"
        );
        var sourceTemplateDir = Path.Combine(TemplatesRoot, "samples", "generic-property-trading");
        var targetTemplateDir = Path.Combine(tempTemplatesRoot, "samples", "generic-property-trading");
        Directory.CreateDirectory(targetTemplateDir);

        var sourceTemplatePath = Path.Combine(sourceTemplateDir, "template.json");
        var targetTemplatePath = Path.Combine(targetTemplateDir, "template.json");
        File.Copy(sourceTemplatePath, targetTemplatePath, overwrite: true);

        try
        {
            var isolatedCatalog = new FileTemplateCatalogService(tempTemplatesRoot);
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var dbContext = new BankersSeatDbContext(dbOptions);
            await dbContext.Database.MigrateAsync();
            var sessionService = new SqliteSessionService(dbContext, isolatedCatalog);
            var request = new CreateSessionRequest(
                "generic-property-trading",
                "standard-edition",
                "1.0.0",
                "Host",
                new Dictionary<string, System.Text.Json.JsonElement>()
            );

            var firstSession = await sessionService.CreateSessionAsync(request, CancellationToken.None);
            var firstSnapshot = await sessionService.GetAuthorizedSnapshotAsync(
                firstSession.SessionId,
                firstSession.HostParticipantId,
                firstSession.ReconnectCredential,
                CancellationToken.None
            );
            var firstHostAccount = firstSnapshot.Accounts.Single(account =>
                account.OwnerId == firstSession.HostParticipantId
            );

            var originalTemplateJson = await File.ReadAllTextAsync(targetTemplatePath);
            var updatedTemplateJson = originalTemplateJson.Replace(
                "\"startingPlayerBalance\": 1500",
                "\"startingPlayerBalance\": 2500",
                StringComparison.Ordinal
            );
            Assert.NotEqual(originalTemplateJson, updatedTemplateJson);
            await File.WriteAllTextAsync(targetTemplatePath, updatedTemplateJson);

            var firstSnapshotAfterChange = await sessionService.GetAuthorizedSnapshotAsync(
                firstSession.SessionId,
                firstSession.HostParticipantId,
                firstSession.ReconnectCredential,
                CancellationToken.None
            );
            var firstHostAccountAfterChange = firstSnapshotAfterChange.Accounts.Single(account =>
                account.OwnerId == firstSession.HostParticipantId
            );
            Assert.Equal(firstSnapshot.Template.SnapshotId, firstSnapshotAfterChange.Template.SnapshotId);
            Assert.Equal(firstSnapshot.Template.ContentHash, firstSnapshotAfterChange.Template.ContentHash);
            Assert.Equal(firstHostAccount.Balance, firstHostAccountAfterChange.Balance);
            Assert.Equal(1500, firstHostAccountAfterChange.Balance);

            var secondSession = await sessionService.CreateSessionAsync(request, CancellationToken.None);
            var secondHostAccount = secondSession.InitialSnapshot.Accounts.Single(account =>
                account.OwnerId == secondSession.HostParticipantId
            );

            Assert.NotEqual(
                firstSnapshot.Template.ContentHash,
                secondSession.InitialSnapshot.Template.ContentHash
            );
            Assert.Equal(2500, secondHostAccount.Balance);
        }
        finally
        {
            if (Directory.Exists(tempTemplatesRoot))
            {
                Directory.Delete(tempTemplatesRoot, recursive: true);
            }
        }
    }
}
