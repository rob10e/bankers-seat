using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using Microsoft.AspNetCore.Mvc;

namespace BankersSeat.Server.Api.V1;

/// <summary>
/// HTTP endpoints for template draft management.
/// Drafts are personal, editable copies of templates for non-technical authoring.
/// </summary>
[ApiController]
[Route("api/v1/templates/drafts")]
[Produces("application/json")]
public sealed class TemplateDraftController : ControllerBase
{
    private readonly ITemplateDraftService _draftService;
    private readonly ILogger<TemplateDraftController> _logger;

    public TemplateDraftController(
        ITemplateDraftService draftService,
        ILogger<TemplateDraftController> logger)
    {
        _draftService = draftService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new draft from an existing template.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TemplateDraftResponse>> CreateDraft(
        [FromBody] CreateTemplateDraftRequest request,
        CancellationToken ct)
    {
        // TODO: Extract userId from ClaimsPrincipal when auth is integrated
        var userId = Guid.NewGuid(); // Placeholder

        _logger.LogInformation("Creating draft from {TemplateId}/{EditionId}",
            request.TemplateId, request.EditionId);

        var draft = await _draftService.CreateDraftAsync(
            userId,
            request.TemplateId,
            request.EditionId,
            request.TemplateVersion,
            ct);

        if (draft == null)
        {
            _logger.LogWarning("Failed to create draft: template not found {TemplateId}",
                request.TemplateId);
            return BadRequest(new { error = "Template not found or invalid" });
        }

        return Ok(draft);
    }

    /// <summary>
    /// Get a draft by ID.
    /// </summary>
    [HttpGet("{draftId:guid}")]
    public async Task<ActionResult<TemplateDraftResponse>> GetDraft(
        Guid draftId,
        CancellationToken ct)
    {
        // TODO: Extract userId from ClaimsPrincipal
        var userId = Guid.NewGuid(); // Placeholder

        var draft = await _draftService.GetDraftAsync(draftId, userId, ct);
        if (draft == null)
        {
            return NotFound();
        }

        return Ok(draft);
    }

    /// <summary>
    /// List drafts for the authenticated user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ListTemplateDraftsResponse>> ListDrafts(
        [FromQuery] int pageSize = 20,
        [FromQuery] int pageNumber = 1,
        CancellationToken ct = default)
    {
        // TODO: Extract userId from ClaimsPrincipal
        var userId = Guid.NewGuid(); // Placeholder

        var result = await _draftService.ListDraftsAsync(userId, pageSize, pageNumber, ct);
        return Ok(result);
    }

    /// <summary>
    /// Update a draft with new template data.
    /// </summary>
    [HttpPut("{draftId:guid}")]
    public async Task<ActionResult<TemplateDraftResponse>> UpdateDraft(
        Guid draftId,
        [FromBody] UpdateTemplateDraftRequest request,
        CancellationToken ct)
    {
        // TODO: Extract userId from ClaimsPrincipal
        var userId = Guid.NewGuid(); // Placeholder

        _logger.LogInformation("Updating draft {DraftId}", draftId);

        var draft = await _draftService.UpdateDraftAsync(draftId, userId, request.TemplateData, ct);
        if (draft == null)
        {
            return NotFound();
        }

        return Ok(draft);
    }

    /// <summary>
    /// Delete a draft.
    /// </summary>
    [HttpDelete("{draftId:guid}")]
    public async Task<ActionResult> DeleteDraft(
        Guid draftId,
        CancellationToken ct)
    {
        // TODO: Extract userId from ClaimsPrincipal
        var userId = Guid.NewGuid(); // Placeholder

        _logger.LogInformation("Deleting draft {DraftId}", draftId);

        var deleted = await _draftService.DeleteDraftAsync(draftId, userId, ct);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Export a draft as a JSON file.
    /// </summary>
    [HttpGet("{draftId:guid}/export")]
    public async Task<ActionResult> ExportDraft(
        Guid draftId,
        CancellationToken ct)
    {
        // TODO: Extract userId from ClaimsPrincipal
        var userId = Guid.NewGuid(); // Placeholder

        _logger.LogInformation("Exporting draft {DraftId}", draftId);

        var content = await _draftService.ExportDraftAsync(draftId, userId, ct);
        if (content == null)
        {
            return NotFound();
        }

        return File(
            content,
            "application/json",
            $"draft-{draftId}.json");
    }
}
