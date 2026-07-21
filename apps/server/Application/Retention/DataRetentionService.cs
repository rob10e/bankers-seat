using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankersSeat.Server.Application.Retention;

public interface IDataRetentionService
{
    Task<SessionTtlPolicy> CreatePolicyAsync(Guid sessionId, int retentionDays, bool autoDeleteOnComplete, CancellationToken ct = default);
    Task<SessionTtlPolicy?> GetPolicyAsync(Guid sessionId, CancellationToken ct = default);
    Task CleanupExpiredSessionsAsync(bool dryRun = false, CancellationToken ct = default);
    Task ArchiveOldLedgerEntriesAsync(int archiveAfterDays = 90, bool dryRun = false, CancellationToken ct = default);
    Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default);
}

public sealed class DataRetentionService : IDataRetentionService
{
    private readonly BankersSeatDbContext _db;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly IConfiguration _config;

    public DataRetentionService(BankersSeatDbContext db, ILogger<DataRetentionService> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _config = config;
    }

    public async Task<SessionTtlPolicy> CreatePolicyAsync(Guid sessionId, int retentionDays, bool autoDeleteOnComplete, CancellationToken ct = default)
    {
        var policy = new SessionTtlPolicyEntity
        {
            SessionId = sessionId,
            RetentionDays = retentionDays,
            AutoDeleteOnComplete = autoDeleteOnComplete,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(retentionDays),
            IsArchived = false,
            ArchivedAtUtc = null
        };

        _db.SessionTtlPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created TTL policy for session {SessionId}: {Days} days, autoDelete: {AutoDelete}",
            sessionId, retentionDays, autoDeleteOnComplete);

        return new SessionTtlPolicy
        {
            SessionId = policy.SessionId,
            RetentionDays = policy.RetentionDays,
            AutoDeleteOnComplete = policy.AutoDeleteOnComplete,
            ExpiresAtUtc = policy.ExpiresAtUtc,
            IsArchived = policy.IsArchived,
            ArchivedAtUtc = policy.ArchivedAtUtc
        };
    }

    public async Task<SessionTtlPolicy?> GetPolicyAsync(Guid sessionId, CancellationToken ct = default)
    {
        var policy = await _db.SessionTtlPolicies.FirstOrDefaultAsync(p => p.SessionId == sessionId, ct);
        if (policy == null)
            return null;

        return new SessionTtlPolicy
        {
            SessionId = policy.SessionId,
            RetentionDays = policy.RetentionDays,
            AutoDeleteOnComplete = policy.AutoDeleteOnComplete,
            ExpiresAtUtc = policy.ExpiresAtUtc,
            IsArchived = policy.IsArchived,
            ArchivedAtUtc = policy.ArchivedAtUtc
        };
    }

    public async Task CleanupExpiredSessionsAsync(bool dryRun = false, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiredPolicies = await _db.SessionTtlPolicies
            .Where(p => !p.IsArchived && p.ExpiresAtUtc < now)
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} expired sessions to clean up (dryRun: {DryRun})", expiredPolicies.Count, dryRun);

        if (!dryRun)
        {
            foreach (var policy in expiredPolicies)
            {
                policy.IsArchived = true;
                policy.ArchivedAtUtc = now;

                var session = await _db.GameSessions.FirstOrDefaultAsync(s => s.Id == policy.SessionId, ct);
                if (session != null)
                {
                    var participants = await _db.Participants.Where(p => p.SessionId == policy.SessionId).ToListAsync(ct);
                    var ledger = await _db.LedgerTransactions.Where(t => t.SessionId == policy.SessionId).ToListAsync(ct);

                    _db.Participants.RemoveRange(participants);
                    _db.LedgerTransactions.RemoveRange(ledger);
                    _db.GameSessions.Remove(session);
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Cleaned up {Count} expired sessions", expiredPolicies.Count);
        }
    }

    public async Task ArchiveOldLedgerEntriesAsync(int archiveAfterDays = 90, bool dryRun = false, CancellationToken ct = default)
    {
        var archiveThreshold = DateTime.UtcNow.AddDays(-archiveAfterDays);
        var oldTransactions = await _db.LedgerTransactions
            .Where(t => t.CreatedAtUtc < archiveThreshold)
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} ledger entries older than {Days} days to archive (dryRun: {DryRun})",
            oldTransactions.Count, archiveAfterDays, dryRun);

        if (!dryRun && oldTransactions.Count > 0)
        {
            foreach (var transaction in oldTransactions)
            {
                transaction.Note = $"[ARCHIVED] {transaction.Note}";
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Archived {Count} ledger entries", oldTransactions.Count);
        }
    }

    public async Task DeleteUserDataAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogWarning("Initiating GDPR data deletion for user {UserId}", userId);

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user != null)
        {
            user.IsDeleted = true;
            user.Email = $"deleted-{Guid.NewGuid()}@deleted.local";
            user.DisplayName = "[DELETED]";
        }

        var ownedSessions = await _db.SessionMetadata
            .Where(m => m.OwnerUserId == userId)
            .ToListAsync(ct);

        foreach (var session in ownedSessions)
        {
            var policy = await _db.SessionTtlPolicies.FirstOrDefaultAsync(p => p.SessionId == session.SessionId, ct);
            if (policy != null)
            {
                policy.ExpiresAtUtc = DateTime.UtcNow;
                policy.IsArchived = true;
                policy.ArchivedAtUtc = DateTime.UtcNow;
            }
        }

        var refreshTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(ct);
        _db.RefreshTokens.RemoveRange(refreshTokens);

        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("GDPR data deletion completed for user {UserId}", userId);
    }
}

public sealed class SessionTtlPolicy
{
    public required Guid SessionId { get; init; }
    public int RetentionDays { get; init; }
    public bool AutoDeleteOnComplete { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public bool IsArchived { get; init; }
    public DateTime? ArchivedAtUtc { get; init; }
}
