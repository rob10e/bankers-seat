using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankersSeat.Server.Application.Sessions;

public interface ISessionMetadataService
{
    Task<SessionMetadataInfo> CreateOrUpdateMetadataAsync(Guid sessionId, Guid? ownerUserId, string label, CancellationToken ct = default);
    Task UpdateLastAccessedAsync(Guid sessionId, CancellationToken ct = default);
    Task UpdateParticipantCountAsync(Guid sessionId, int count, CancellationToken ct = default);
    Task<IEnumerable<SessionMetadataInfo>> GetOwnedSessionsAsync(Guid userId, int limit = 20, CancellationToken ct = default);
    Task<SessionMetadataInfo?> GetMetadataAsync(Guid sessionId, CancellationToken ct = default);
}

public sealed class SessionMetadataService : ISessionMetadataService
{
    private readonly BankersSeatDbContext _db;
    private readonly ILogger<SessionMetadataService> _logger;

    public SessionMetadataService(BankersSeatDbContext db, ILogger<SessionMetadataService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SessionMetadataInfo> CreateOrUpdateMetadataAsync(Guid sessionId, Guid? ownerUserId, string label, CancellationToken ct = default)
    {
        var metadata = await _db.SessionMetadata.FirstOrDefaultAsync(m => m.SessionId == sessionId, ct);

        if (metadata == null)
        {
            metadata = new SessionMetadataEntity
            {
                SessionId = sessionId,
                OwnerUserId = ownerUserId,
                Label = label,
                CreatedAtUtc = DateTime.UtcNow,
                LastAccessedAtUtc = DateTime.UtcNow,
                ParticipantCount = 0
            };
            _db.SessionMetadata.Add(metadata);
        }
        else
        {
            metadata.OwnerUserId = ownerUserId;
            metadata.Label = label;
            metadata.LastAccessedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Session metadata updated: {SessionId}, owner: {Owner}, label: {Label}", sessionId, ownerUserId, label);

        return MapToInfo(metadata);
    }

    public async Task UpdateLastAccessedAsync(Guid sessionId, CancellationToken ct = default)
    {
        var metadata = await _db.SessionMetadata.FirstOrDefaultAsync(m => m.SessionId == sessionId, ct);
        if (metadata != null)
        {
            metadata.LastAccessedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task UpdateParticipantCountAsync(Guid sessionId, int count, CancellationToken ct = default)
    {
        var metadata = await _db.SessionMetadata.FirstOrDefaultAsync(m => m.SessionId == sessionId, ct);
        if (metadata != null)
        {
            metadata.ParticipantCount = count;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IEnumerable<SessionMetadataInfo>> GetOwnedSessionsAsync(Guid userId, int limit = 20, CancellationToken ct = default)
    {
        var metadata = await _db.SessionMetadata
            .Where(m => m.OwnerUserId == userId)
            .OrderByDescending(m => m.LastAccessedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        return metadata.Select(MapToInfo);
    }

    public async Task<SessionMetadataInfo?> GetMetadataAsync(Guid sessionId, CancellationToken ct = default)
    {
        var metadata = await _db.SessionMetadata.FirstOrDefaultAsync(m => m.SessionId == sessionId, ct);
        return metadata == null ? null : MapToInfo(metadata);
    }

    private static SessionMetadataInfo MapToInfo(SessionMetadataEntity entity)
    {
        return new SessionMetadataInfo
        {
            SessionId = entity.SessionId,
            OwnerUserId = entity.OwnerUserId,
            Label = entity.Label,
            CreatedAtUtc = entity.CreatedAtUtc,
            LastAccessedAtUtc = entity.LastAccessedAtUtc,
            ParticipantCount = entity.ParticipantCount
        };
    }
}

public sealed class SessionMetadataInfo
{
    public required Guid SessionId { get; init; }
    public Guid? OwnerUserId { get; init; }
    public required string Label { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime LastAccessedAtUtc { get; init; }
    public int ParticipantCount { get; init; }
}
