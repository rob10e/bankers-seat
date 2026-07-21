using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Sessions;
using BankersSeat.Server.Application.RoomSecurity;
using BankersSeat.Server.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankersSeat.Server.Api.V1;

[ApiController]
[Route("api/v1/sessions")]
public sealed class Phase4SessionsController : ControllerBase
{
    private readonly ISessionMetadataService _metadataService;
    private readonly IRoomSecurityService _roomSecurityService;
    private readonly IAuditService _auditService;
    private readonly ILogger<Phase4SessionsController> _logger;

    public Phase4SessionsController(ISessionMetadataService metadataService, IRoomSecurityService roomSecurityService, 
        IAuditService auditService, ILogger<Phase4SessionsController> logger)
    {
        _metadataService = metadataService;
        _roomSecurityService = roomSecurityService;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet("owned")]
    [Authorize]
    [ProducesResponseType<SavedSessionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SavedSessionsResponse>> GetOwnedSessions(
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var sessions = await _metadataService.GetOwnedSessionsAsync(userId, limit, ct);
            var response = new SavedSessionsResponse
            {
                Sessions = sessions.Select(s => new SavedSessionInfo
                {
                    SessionId = s.SessionId,
                    RoomCode = "XXXX-XXXX",
                    Label = s.Label,
                    TemplateName = "Template",
                    CreatedAtUtc = s.CreatedAtUtc,
                    LastAccessedAtUtc = s.LastAccessedAtUtc,
                    ParticipantCount = s.ParticipantCount
                }),
                Total = sessions.Count()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving owned sessions for user {UserId}", userId);
            return Problem("Failed to retrieve sessions", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("{sessionId:guid}/join-links")]
    [Authorize]
    [ProducesResponseType<JoinLinkResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JoinLinkResponse>> CreateJoinLink(
        [FromRoute] Guid sessionId,
        [FromBody] CreateJoinLinkRequest request,
        CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var link = await _roomSecurityService.CreateTemporaryJoinLinkAsync(sessionId, request.ExpirationMinutes, ct);

            var joinUrl = $"{Request.Scheme}://{Request.Host}/join?link={link.LinkToken}";

            await _auditService.LogActionAsync(sessionId, userId, null, "CreateJoinLink", 
                $"Created temporary join link, expires in {request.ExpirationMinutes} minutes", 
                HttpContext.Connection.RemoteIpAddress?.ToString(), ct: ct);

            return CreatedAtAction(nameof(CreateJoinLink), new JoinLinkResponse
            {
                Id = link.Id,
                LinkToken = link.LinkToken,
                ExpiresAtUtc = link.ExpiresAtUtc,
                JoinUrl = joinUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating join link for session {SessionId}", sessionId);
            return Problem("Failed to create join link", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("{sessionId:guid}/audit-logs")]
    [Authorize]
    [ProducesResponseType<IEnumerable<AuditLogResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<AuditLogResponse>>> GetAuditLogs(
        [FromRoute] Guid sessionId,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var logs = await _auditService.GetAuditLogsAsync(sessionId, limit, ct);
            var response = logs.Select(l => new AuditLogResponse
            {
                Id = l.Id,
                ActorUserId = l.ActorUserId,
                Action = l.Action,
                Details = l.Details,
                IpAddress = l.IpAddress,
                CreatedAtUtc = l.CreatedAtUtc,
                Result = l.Result
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for session {SessionId}", sessionId);
            return Problem("Failed to retrieve audit logs", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
