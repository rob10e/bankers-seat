using System.Text.Json;
using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;

namespace BankersSeat.Server.Application.Health;

public sealed class DefaultHealthService : IHealthService
{
    private const int TemplateSchemaVersion = 1;
    private const string ApplicationVersion = "0.1.0";
    private static readonly DateTimeOffset ApplicationStartedUtc = DateTimeOffset.UtcNow;
    
    private readonly BankersSeatDbContext dbContext;
    private readonly ITemplateCatalogService templateCatalog;
    private DateTimeOffset? lastCatalogScanUtc;
    private int lastValidCount;
    private int lastInvalidCount;

    public DefaultHealthService(BankersSeatDbContext dbContext, ITemplateCatalogService templateCatalog)
    {
        this.dbContext = dbContext;
        this.templateCatalog = templateCatalog;
        this.lastValidCount = 0;
        this.lastInvalidCount = 0;
    }

    public Task<HealthLiveResponse> GetLiveStatusAsync()
    {
        return Task.FromResult(new HealthLiveResponse(
            Status: "healthy",
            CheckedAtUtc: DateTimeOffset.UtcNow
        ));
    }

    public async Task<HealthReadyResponse> GetReadyStatusAsync(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var databaseAvailable = false;
        var templateCount = 0;

        try
        {
            await dbContext.Database.CanConnectAsync(cancellationToken);
            databaseAvailable = true;
        }
        catch
        {
            // Database unavailable
        }

        try
        {
            var catalog = await templateCatalog.GetCatalogAsync(cancellationToken);
            templateCount = catalog.Count;
        }
        catch
        {
            // Catalog scan failed
        }

        var status = databaseAvailable ? "healthy" : "unhealthy";

        return new HealthReadyResponse(
            Status: status,
            DatabaseAvailable: databaseAvailable,
            TemplateCatalogCount: templateCount,
            CheckedAtUtc: checkedAt
        );
    }

    public async Task<HealthTemplatesResponse> GetTemplatesStatusAsync(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var validCount = 0;
        var invalidCount = 0;
        var catalogScannedAt = lastCatalogScanUtc ?? ApplicationStartedUtc;

        try
        {
            var catalog = await templateCatalog.GetCatalogAsync(cancellationToken);
            validCount = catalog.Count;
            catalogScannedAt = DateTimeOffset.UtcNow;
            lastCatalogScanUtc = catalogScannedAt;
            lastValidCount = validCount;
            lastInvalidCount = invalidCount;
        }
        catch
        {
            validCount = lastValidCount;
            invalidCount = lastInvalidCount;
        }

        var status = invalidCount == 0 ? "healthy" : "degraded";

        return new HealthTemplatesResponse(
            Status: status,
            ValidTemplateCount: validCount,
            InvalidTemplateCount: invalidCount,
            CatalogScannedAtUtc: catalogScannedAt,
            CheckedAtUtc: checkedAt
        );
    }

    public Task<HealthVersionResponse> GetVersionStatusAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new HealthVersionResponse(
            ApplicationVersion: ApplicationVersion,
            TemplateSchemaVersion: TemplateSchemaVersion,
            Status: "healthy",
            CheckedAtUtc: DateTimeOffset.UtcNow
        ));
    }
}
