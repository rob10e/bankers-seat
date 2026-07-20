using BankersSeat.Server.Application.Templates;
using Xunit;

namespace BankersSeat.Server.Tests.Integration;

public sealed class TemplateRescanTests
{
    private static readonly string TemplatesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "templates")
    );

    [Fact]
    public async Task RescanAsync_RefreshesCatalog()
    {
        var innerService = new FileTemplateCatalogService(TemplatesRoot);
        var cachedService = new CachedTemplateCatalogService(innerService);

        var beforeRescan = await cachedService.GetCatalogAsync(CancellationToken.None);
        var beforeCount = beforeRescan.Count;

        await cachedService.RescanAsync(CancellationToken.None);

        var afterRescan = await cachedService.GetCatalogAsync(CancellationToken.None);
        var afterCount = afterRescan.Count;

        Assert.Equal(beforeCount, afterCount);
        Assert.NotEqual(DateTimeOffset.MinValue, cachedService.GetLastScannedUtc());
    }

    [Fact]
    public async Task GetCatalogAsync_ReturnsCachedResultAfterFirstCall()
    {
        var innerService = new FileTemplateCatalogService(TemplatesRoot);
        var cachedService = new CachedTemplateCatalogService(innerService);

        var firstCall = await cachedService.GetCatalogAsync(CancellationToken.None);
        var secondCall = await cachedService.GetCatalogAsync(CancellationToken.None);

        Assert.Same(firstCall, secondCall);
    }

    [Fact]
    public async Task RescanAsync_UpdatesCatalogReference()
    {
        var innerService = new FileTemplateCatalogService(TemplatesRoot);
        var cachedService = new CachedTemplateCatalogService(innerService);

        var firstCatalog = await cachedService.GetCatalogAsync(CancellationToken.None);
        await cachedService.RescanAsync(CancellationToken.None);
        var secondCatalog = await cachedService.GetCatalogAsync(CancellationToken.None);

        Assert.NotSame(firstCatalog, secondCatalog);
    }

    [Fact]
    public async Task DiscoveredTemplatesMatchInnerService()
    {
        var innerService = new FileTemplateCatalogService(TemplatesRoot);
        var cachedService = new CachedTemplateCatalogService(innerService);

        var innerCatalog = await innerService.GetCatalogAsync(CancellationToken.None);
        var cachedCatalog = await cachedService.GetCatalogAsync(CancellationToken.None);

        Assert.Equal(innerCatalog.Count, cachedCatalog.Count);
        var innerIds = innerCatalog.Select(t => t.Identity).ToHashSet();
        var cachedIds = cachedCatalog.Select(t => t.Identity).ToHashSet();
        Assert.Equal(innerIds, cachedIds);
    }
}
