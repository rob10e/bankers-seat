using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Domain.Templates;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BankersSeat.Server.Api.V1;

[ApiController]
[Route("api/v1/templates")]
public sealed class TemplatesController : ControllerBase
{
    private readonly ITemplateCatalogService templateCatalogService;
    private readonly ITemplateDiffService diffService;
    private readonly ILogger<TemplatesController> logger;

    public TemplatesController(
        ITemplateCatalogService templateCatalogService,
        ITemplateDiffService diffService,
        ILogger<TemplatesController> logger)
    {
        this.templateCatalogService = templateCatalogService;
        this.diffService = diffService;
        this.logger = logger;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TemplateCatalogEntryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TemplateCatalogEntryResponse>>> GetCatalog(
        CancellationToken cancellationToken
    )
    {
        var catalog = await templateCatalogService.GetCatalogAsync(cancellationToken);
        var response = catalog
            .Select(ToCatalogEntryResponse)
            .ToList();

        return Ok(response);
    }

    [HttpGet("{templateId}/editions/{editionId}/versions/{templateVersion}")]
    [ProducesResponseType<TemplateDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateDetailResponse>> GetTemplateByVersion(
        [FromRoute] string templateId,
        [FromRoute] string editionId,
        [FromRoute] string templateVersion,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await templateCatalogService.GetTemplateSnapshotAsync(
            templateId,
            editionId,
            templateVersion,
            cancellationToken
        );
        if (snapshot is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Template not found.",
                detail: "No template matched the requested identity.",
                extensions: new Dictionary<string, object?> { ["code"] = "template-not-found" }
            );
        }

        var catalog = await templateCatalogService.GetCatalogAsync(cancellationToken);
        var entry = catalog.SingleOrDefault(candidate =>
            string.Equals(candidate.Identity.TemplateId, templateId, StringComparison.Ordinal)
            && string.Equals(candidate.Identity.EditionId, editionId, StringComparison.Ordinal)
            && string.Equals(candidate.Identity.TemplateVersion, templateVersion, StringComparison.Ordinal)
        );
        if (entry is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Template not found.",
                detail: "No template metadata matched the requested identity.",
                extensions: new Dictionary<string, object?> { ["code"] = "template-not-found" }
            );
        }

        using var document = JsonDocument.Parse(snapshot.TemplateJson);
        var response = new TemplateDetailResponse(
            entry.Identity.TemplateId,
            entry.Identity.EditionId,
            entry.Identity.TemplateVersion,
            entry.Name,
            entry.EditionName,
            entry.Description,
            entry.MinimumPlayers,
            entry.MaximumPlayers,
            entry.Tags,
            snapshot.SchemaVersion,
            snapshot.ContentHash,
            snapshot.StartingPlayerBalance,
            snapshot.AllowPlayerOverdraft,
            document.RootElement.Clone()
        );

        return Ok(response);
    }

    [HttpGet("{templateId}/diff")]
    [ProducesResponseType<TemplateDiffResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateDiffResponse>> DiffTemplateVersions(
        [FromRoute] string templateId,
        [FromQuery] string fromEditionId,
        [FromQuery] string fromVersion,
        [FromQuery] string toEditionId,
        [FromQuery] string toVersion,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(fromEditionId) || string.IsNullOrWhiteSpace(fromVersion) ||
            string.IsNullOrWhiteSpace(toEditionId) || string.IsNullOrWhiteSpace(toVersion))
        {
            return BadRequest(new
            {
                error = "Missing query parameters",
                required = new[] { "fromEditionId", "fromVersion", "toEditionId", "toVersion" }
            });
        }

        var fromSnapshot = await templateCatalogService.GetTemplateSnapshotAsync(
            templateId,
            fromEditionId,
            fromVersion,
            cancellationToken
        );

        if (fromSnapshot is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "From template not found.",
                detail: $"No template found for {templateId}/{fromEditionId}/{fromVersion}",
                extensions: new Dictionary<string, object?> { ["code"] = "from-template-not-found" }
            );
        }

        var toSnapshot = await templateCatalogService.GetTemplateSnapshotAsync(
            templateId,
            toEditionId,
            toVersion,
            cancellationToken
        );

        if (toSnapshot is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "To template not found.",
                detail: $"No template found for {templateId}/{toEditionId}/{toVersion}",
                extensions: new Dictionary<string, object?> { ["code"] = "to-template-not-found" }
            );
        }

        try
        {
            var diff = await diffService.DiffTemplatesAsync(fromSnapshot, toSnapshot, cancellationToken);
            return Ok(diff);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error computing template diff");
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Failed to compute diff.",
                detail: ex.Message,
                extensions: new Dictionary<string, object?> { ["code"] = "diff-computation-failed" }
            );
        }
    }

    private static TemplateCatalogEntryResponse ToCatalogEntryResponse(TemplateCatalogEntry entry)
    {
        return new TemplateCatalogEntryResponse(
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
        );
    }
}
