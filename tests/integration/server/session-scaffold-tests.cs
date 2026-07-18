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
    private readonly FileTemplateCatalogService catalogService = new(
        Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "templates")
        )
    );

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
}
