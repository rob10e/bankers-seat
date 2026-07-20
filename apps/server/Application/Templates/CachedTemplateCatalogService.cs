using BankersSeat.Server.Domain.Templates;

namespace BankersSeat.Server.Application.Templates;

public sealed class CachedTemplateCatalogService : ITemplateCatalogService
{
    private readonly ITemplateCatalogService innerService;
    private IReadOnlyList<TemplateCatalogEntry> cachedCatalog = [];
    private DateTimeOffset lastScannedUtc = DateTimeOffset.MinValue;
    private readonly object lockObject = new();

    public CachedTemplateCatalogService(ITemplateCatalogService innerService)
    {
        this.innerService = innerService;
    }

    public async Task<IReadOnlyList<TemplateCatalogEntry>> GetCatalogAsync(
        CancellationToken cancellationToken
    )
    {
        lock (lockObject)
        {
            if (cachedCatalog.Count > 0)
            {
                return cachedCatalog;
            }
        }

        var catalog = await innerService.GetCatalogAsync(cancellationToken);

        lock (lockObject)
        {
            cachedCatalog = catalog;
            lastScannedUtc = DateTimeOffset.UtcNow;
            return cachedCatalog;
        }
    }

    public async Task<TemplateSnapshot?> GetTemplateSnapshotAsync(
        string templateId,
        string editionId,
        string templateVersion,
        CancellationToken cancellationToken
    )
    {
        return await innerService.GetTemplateSnapshotAsync(templateId, editionId, templateVersion, cancellationToken);
    }

    public async Task RescanAsync(CancellationToken cancellationToken)
    {
        var freshCatalog = await innerService.GetCatalogAsync(cancellationToken);

        lock (lockObject)
        {
            cachedCatalog = freshCatalog;
            lastScannedUtc = DateTimeOffset.UtcNow;
        }
    }

    public DateTimeOffset GetLastScannedUtc()
    {
        lock (lockObject)
        {
            return lastScannedUtc;
        }
    }
}
