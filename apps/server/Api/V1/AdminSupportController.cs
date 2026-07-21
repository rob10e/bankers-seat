using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Api.V1.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BankersSeat.Server.Api.V1.Admin;

[ApiController]
[Route("api/v1/admin")]
[Authorize]
public sealed class AdminSupportController : ControllerBase
{
    private readonly BankersSeatDbContext _db;
    private readonly ILogger<AdminSupportController> _logger;

    public AdminSupportController(BankersSeatDbContext db, ILogger<AdminSupportController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType<AdminSessionInfoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminSessionInfoResponse>> GetSessionInfo(
        [FromRoute] Guid sessionId,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(ct))
            return Forbid();

        var session = await _db.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null)
            return NotFound();

        var metadata = await _db.SessionMetadata.FirstOrDefaultAsync(m => m.SessionId == sessionId, ct);
        var participants = await _db.Participants.Where(p => p.SessionId == sessionId).CountAsync(ct);
        var transactions = await _db.LedgerTransactions.Where(t => t.SessionId == sessionId).CountAsync(ct);

        return Ok(new AdminSessionInfoResponse
        {
            SessionId = session.Id,
            RoomCode = session.RoomCode,
            Status = session.Status,
            OwnerUserId = metadata?.OwnerUserId,
            ParticipantCount = participants,
            CreatedAtUtc = session.CreatedAtUtc,
            LastAccessedAtUtc = (metadata?.LastAccessedAtUtc ?? new DateTimeOffset(DateTime.UtcNow)),
            TransactionCount = (int)transactions
        });
    }

    [HttpGet("sessions")]
    [ProducesResponseType<IEnumerable<AdminSessionInfoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<AdminSessionInfoResponse>>> SearchSessions(
        [FromQuery] string? roomCode,
        [FromQuery] Guid? ownerId,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(ct))
            return Forbid();

        var query = _db.GameSessions.AsQueryable();

        if (!string.IsNullOrEmpty(roomCode))
            query = query.Where(s => s.RoomCode.Contains(roomCode));

        var sessions = await query.Take(limit).ToListAsync(ct);

        var results = new List<AdminSessionInfoResponse>();
        foreach (var session in sessions)
        {
            var metadata = await _db.SessionMetadata.FirstOrDefaultAsync(m => m.SessionId == session.Id, ct);
            
            if (ownerId.HasValue && metadata?.OwnerUserId != ownerId)
                continue;

            var participants = await _db.Participants.Where(p => p.SessionId == session.Id).CountAsync(ct);
            var transactions = await _db.LedgerTransactions.Where(t => t.SessionId == session.Id).CountAsync(ct);

            results.Add(new AdminSessionInfoResponse
            {
                SessionId = session.Id,
                RoomCode = session.RoomCode,
                Status = session.Status,
                OwnerUserId = metadata?.OwnerUserId,
                ParticipantCount = participants,
                CreatedAtUtc = session.CreatedAtUtc,
                LastAccessedAtUtc = (metadata?.LastAccessedAtUtc ?? new DateTimeOffset(DateTime.UtcNow)),
                TransactionCount = (int)transactions
            });
        }

        return Ok(results);
    }

    [HttpPost("sessions/{sessionId:guid}/pause")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminPauseSession(
        [FromRoute] Guid sessionId,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(ct))
            return Forbid();

        var session = await _db.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null)
            return NotFound();

        session.Status = "paused";
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Admin paused session {SessionId}", sessionId);

        return NoContent();
    }

    [HttpPost("sessions/{sessionId:guid}/resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminResumeSession(
        [FromRoute] Guid sessionId,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(ct))
            return Forbid();

        var session = await _db.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null)
            return NotFound();

        session.Status = "active";
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Admin resumed session {SessionId}", sessionId);

        return NoContent();
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminDeleteSession(
        [FromRoute] Guid sessionId,
        CancellationToken ct = default)
    {
        if (!await IsAdminAsync(ct))
            return Forbid();

        var session = await _db.GameSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null)
            return NotFound();

        var participants = await _db.Participants.Where(p => p.SessionId == sessionId).ToListAsync(ct);
        var transactions = await _db.LedgerTransactions.Where(t => t.SessionId == sessionId).ToListAsync(ct);
        var postings = await _db.LedgerPostings.Where(p => p.SessionId == sessionId).ToListAsync(ct);
        var fields = await _db.PlayerFieldValues.Where(f => f.SessionId == sessionId).ToListAsync(ct);
        var metadata = await _db.SessionMetadata.FirstOrDefaultAsync(m => m.SessionId == sessionId, ct);

        _db.Participants.RemoveRange(participants);
        _db.LedgerTransactions.RemoveRange(transactions);
        _db.LedgerPostings.RemoveRange(postings);
        _db.PlayerFieldValues.RemoveRange(fields);
        if (metadata != null)
            _db.SessionMetadata.Remove(metadata);
        _db.GameSessions.Remove(session);

        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Admin deleted session {SessionId}", sessionId);

        return NoContent();
    }

    private async Task<bool> IsAdminAsync(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return false;

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user?.Email?.EndsWith("@admin.local") == true;
    }
}
