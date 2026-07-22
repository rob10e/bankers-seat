using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankersSeat.Server.Api.V1;

[ApiController]
[Route("api/v1/templates")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class TemplateShareAndGovernanceController : ControllerBase
{
    private readonly ITemplateShareService shareService;
    private readonly ITemplateGovernanceService governanceService;
    private readonly ILogger<TemplateShareAndGovernanceController> logger;

    public TemplateShareAndGovernanceController(
        ITemplateShareService shareService,
        ITemplateGovernanceService governanceService,
        ILogger<TemplateShareAndGovernanceController> logger
    )
    {
        this.shareService = shareService;
        this.governanceService = governanceService;
        this.logger = logger;
    }

    // ===== Component 6: Private Template Sharing =====

    /// <summary>
    /// Share a template with specific users.
    /// </summary>
    [HttpPost("{templateId}/share")]
    [ProducesResponseType<ShareTemplateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ShareTemplateResponse>> ShareTemplate(
        [FromRoute] string templateId,
        [FromBody] ShareTemplateRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.RecipientEmails == null || request.RecipientEmails.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "At least one recipient email is required."
            });
        }

        var userId = GetUserIdFromClaims();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var shareIds = new List<Guid>();
        var errors = new List<string>();

        foreach (var email in request.RecipientEmails)
        {
            try
            {
                var shareId = await shareService.GrantShareAsync(
                    templateId,
                    userId,
                    email,
                    cancellationToken
                );
                shareIds.Add(shareId);
            }
            catch (Exception ex)
            {
                errors.Add($"{email}: {ex.Message}");
                logger.LogWarning(ex, "Failed to share template {TemplateId} with {Email}", templateId, email);
            }
        }

        return Ok(new ShareTemplateResponse
        {
            ShareIds = shareIds.ToArray(),
            SuccessCount = shareIds.Count,
            Errors = errors.Count > 0 ? errors.ToArray() : null
        });
    }

    /// <summary>
    /// Revoke a template share.
    /// </summary>
    [HttpDelete("{templateId}/share/{shareId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> RevokeShare(
        [FromRoute] string templateId,
        [FromRoute] Guid shareId,
        CancellationToken cancellationToken
    )
    {
        var userId = GetUserIdFromClaims();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            await shareService.RevokeShareAsync(shareId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Share not found",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// List all templates shared with the current user.
    /// </summary>
    [HttpGet("shared-with-me")]
    [ProducesResponseType<SharedWithMeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SharedWithMeResponse>> GetSharedWithMe(
        CancellationToken cancellationToken
    )
    {
        var userEmail = GetUserEmailFromClaims();
        if (string.IsNullOrEmpty(userEmail))
        {
            return Unauthorized();
        }

        var templates = await shareService.GetSharedWithMeAsync(userEmail, cancellationToken);

        return Ok(new SharedWithMeResponse
        {
            Templates = templates
                .Select(t => new SharedTemplateItemResponse
                {
                    TemplateId = t.TemplateId,
                    EditionId = t.EditionId,
                    SharedByName = t.SharedByName,
                    GrantedAtUtc = t.GrantedAtUtc
                })
                .ToArray()
        });
    }

    // ===== Component 7: Marketplace Governance & Licensing =====

    /// <summary>
    /// Publish a template with licensing and author information.
    /// </summary>
    [HttpPost("{templateId}/publish")]
    [ProducesResponseType<PublishTemplateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PublishTemplateResponse>> PublishTemplate(
        [FromRoute] string templateId,
        [FromBody] PublishTemplateRequest request,
        CancellationToken cancellationToken
    )
    {
        var userId = GetUserIdFromClaims();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            await governanceService.PublishTemplateAsync(
                request.TemplateId,
                request.EditionId,
                userId,
                request.Author,
                request.AuthorEmail,
                request.AuthorUrl,
                request.License,
                cancellationToken
            );

            return Ok(new PublishTemplateResponse
            {
                Success = true
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid license",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Publication failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get the moderation queue (admin only).
    /// </summary>
    [HttpGet("admin/moderation-queue")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<ModerationQueueResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ModerationQueueResponse>> GetModerationQueue(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default
    )
    {
        var queue = await governanceService.GetModerationQueueAsync(skip, take, cancellationToken);

        return Ok(new ModerationQueueResponse
        {
            Items = queue.Items
                .Select(item => new ModerationQueueItemResponse
                {
                    TemplateId = item.TemplateId,
                    EditionId = item.EditionId,
                    Author = item.Author,
                    PublishedAtUtc = item.PublishedAtUtc,
                    ModerationStatus = item.ModerationStatus,
                    FlagReasons = item.FlagReasons
                })
                .ToArray(),
            TotalCount = queue.TotalCount,
            PendingCount = queue.PendingCount
        });
    }

    /// <summary>
    /// Approve a template in moderation (admin only).
    /// </summary>
    [HttpPost("admin/templates/{templateId}/{editionId}/approve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ApproveTemplate(
        [FromRoute] string templateId,
        [FromRoute] string editionId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await governanceService.ApproveTemplateAsync(templateId, editionId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Template not found",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Reject a template in moderation (admin only).
    /// </summary>
    [HttpPost("admin/templates/{templateId}/{editionId}/reject")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> RejectTemplate(
        [FromRoute] string templateId,
        [FromRoute] string editionId,
        [FromBody] RejectTemplateRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await governanceService.RejectTemplateAsync(
                templateId,
                editionId,
                request.Reason,
                cancellationToken
            );
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Template not found",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Flag a template for review (admin only).
    /// </summary>
    [HttpPost("admin/templates/{templateId}/{editionId}/flag")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> FlagTemplate(
        [FromRoute] string templateId,
        [FromRoute] string editionId,
        [FromBody] FlagTemplateRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await governanceService.FlagTemplateAsync(
                templateId,
                editionId,
                request.Reasons,
                cancellationToken
            );
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Template not found",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get public templates (published and approved).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("public")]
    [ProducesResponseType<IReadOnlyList<TemplateGovernanceInfoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TemplateGovernanceInfoResponse>>> GetPublicTemplates(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default
    )
    {
        var templates = await governanceService.GetPublicTemplatesAsync(skip, take, cancellationToken);

        return Ok(templates
            .Select(t => new TemplateGovernanceInfoResponse
            {
                MetadataId = t.MetadataId,
                TemplateId = t.TemplateId,
                EditionId = t.EditionId,
                Author = t.Author,
                AuthorEmail = t.AuthorEmail,
                AuthorUrl = t.AuthorUrl,
                License = t.License,
                PublishedAtUtc = t.PublishedAtUtc,
                TemplateStatus = t.TemplateStatus,
                ModerationStatus = t.ModerationStatus,
                DownloadCount = t.DownloadCount,
                FlagReasons = t.FlagReasons
            })
            .ToList());
    }

    private Guid GetUserIdFromClaims()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && Guid.TryParse(claim.Value, out var userId))
        {
            return userId;
        }
        return Guid.Empty;
    }

    private string? GetUserEmailFromClaims()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value;
    }
}
