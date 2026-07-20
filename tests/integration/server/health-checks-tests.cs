using BankersSeat.Server.Application.Health;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BankersSeat.Server.Tests.Integration;

public sealed class HealthCheckTests
{
    private static readonly string TemplatesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "templates")
    );

    [Fact]
    public async Task GetLiveStatus_ReturnsHealthyStatus()
    {
        var service = new DefaultHealthService(null!, new FileTemplateCatalogService(TemplatesRoot));
        
        var response = await service.GetLiveStatusAsync();
        
        Assert.Equal("healthy", response.Status);
        Assert.True(response.CheckedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetReadyStatus_ReturnsDatabaseAvailableWhenConnected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new BankersSeatDbContext(dbOptions);
        await dbContext.Database.MigrateAsync();

        var service = new DefaultHealthService(dbContext, new FileTemplateCatalogService(TemplatesRoot));

        var response = await service.GetReadyStatusAsync(CancellationToken.None);

        Assert.Equal("healthy", response.Status);
        Assert.True(response.DatabaseAvailable);
        Assert.True(response.TemplateCatalogCount > 0);
        Assert.True(response.CheckedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetTemplatesStatus_ReturnsCatalogStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new BankersSeatDbContext(dbOptions);
        await dbContext.Database.MigrateAsync();

        var service = new DefaultHealthService(dbContext, new FileTemplateCatalogService(TemplatesRoot));

        var response = await service.GetTemplatesStatusAsync(CancellationToken.None);

        Assert.True(new[] { "healthy", "degraded" }.Contains(response.Status));
        Assert.True(response.ValidTemplateCount > 0);
        Assert.Equal(0, response.InvalidTemplateCount);
        Assert.True(response.CheckedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetVersionStatus_ReturnsVersionInfo()
    {
        var service = new DefaultHealthService(null!, new FileTemplateCatalogService(TemplatesRoot));

        var response = await service.GetVersionStatusAsync(CancellationToken.None);

        Assert.Equal("0.1.0", response.ApplicationVersion);
        Assert.Equal(1, response.TemplateSchemaVersion);
        Assert.Equal("healthy", response.Status);
        Assert.True(response.CheckedAtUtc <= DateTimeOffset.UtcNow);
    }
}
