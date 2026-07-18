using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using Microsoft.AspNetCore.Mvc;

namespace BankersSeat.Server.Api.V1;

[ApiController]
[Route("api/v1/templates")]
public sealed class TemplatesController : ControllerBase
{
    private readonly ITemplateCatalogService templateCatalogService;

    public TemplatesController(ITemplateCatalogService templateCatalogService)
    {
        this.templateCatalogService = templateCatalogService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TemplateCatalogEntryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TemplateCatalogEntryResponse>>> GetCatalog(
        CancellationToken cancellationToken
    )
    {
        var catalog = await templateCatalogService.GetCatalogAsync(cancellationToken);
        var response = catalog
            .Select(entry => new TemplateCatalogEntryResponse(
                entry.Identity.TemplateId,
                entry.Identity.EditionId,
                entry.Identity.TemplateVersion,
                entry.Name,
                entry.EditionName,
                entry.Description,
                entry.MinimumPlayers,
                entry.MaximumPlayers,
                entry.Tags,
                "valid",
                "built-in"
            ))
            .ToList();

        return Ok(response);
    }
}
