using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using Microsoft.AspNetCore.Mvc;

namespace BankersSeat.Server.Api.V1;

[ApiController]
[Route("api/v1/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly CachedTemplateCatalogService catalogService;

    public AdminController(ITemplateCatalogService templateCatalogService)
    {
        if (templateCatalogService is not CachedTemplateCatalogService cached)
        {
            throw new InvalidOperationException(
                "Template catalog service must be wrapped in CachedTemplateCatalogService for admin operations."
            );
        }

        this.catalogService = cached;
    }

    [HttpPost("templates/rescan")]
    [ProducesResponseType<TemplateRescanResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TemplateRescanResponse>> RescanTemplates(
        CancellationToken cancellationToken
    )
    {
        var lastScannedBefore = catalogService.GetLastScannedUtc();
        await catalogService.RescanAsync(cancellationToken);
        var catalog = await catalogService.GetCatalogAsync(cancellationToken);

        return Ok(new TemplateRescanResponse(
            DiscoveredTemplateCount: catalog.Count,
            RescanCompletedAtUtc: DateTimeOffset.UtcNow,
            LastScannedAtUtc: catalogService.GetLastScannedUtc()
        ));
    }
}
