using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankersSeat.Server.Application.Audit;

public interface IAuditService
{
    Task LogActionAsync(Guid? sessionId, Guid? userIdActor, Guid? participantIdActor, string action, string details, 
        string? ipAddress = null, string? userAgent = null, string? result = null, CancellationToken ct = default);
    Task<IEnumerable<AuditLogEntry>> GetAuditLogsAsync(Guid? sessionId, int limit = 100, CancellationToken ct = default);
}

public sealed class AuditService : IAuditService
{
    private readonly BankersSeatDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(BankersSeatDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogActionAsync(Guid? sessionId, Guid? userIdActor, Guid? participantIdActor, string action, string details,
        string? ipAddress = null, string? userAgent = null, string? result = null, CancellationToken ct = default)
    {
        var log = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ActorUserId = userIdActor,
            ActorParticipantId = participantIdActor,
            Action = action,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
            Result = result
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Audit log: {Action} on session {SessionId}", action, sessionId);
    }

    public async Task<IEnumerable<AuditLogEntry>> GetAuditLogsAsync(Guid? sessionId, int limit = 100, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsQueryable();
        
        if (sessionId.HasValue)
        {
            query = query.Where(l => l.SessionId == sessionId);
        }

        var logs = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        return logs.Select(l => new AuditLogEntry
        {
            Id = l.Id,
            SessionId = l.SessionId,
            ActorUserId = l.ActorUserId,
            ActorParticipantId = l.ActorParticipantId,
            Action = l.Action,
            Details = l.Details,
            IpAddress = l.IpAddress,
            CreatedAtUtc = l.CreatedAtUtc,
            Result = l.Result
        });
    }
}

public sealed class AuditLogEntry
{
    public required Guid Id { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? ActorUserId { get; init; }
    public Guid? ActorParticipantId { get; init; }
    public required string Action { get; init; }
    public required string Details { get; init; }
    public string? IpAddress { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public string? Result { get; init; }
}
