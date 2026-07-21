using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankersSeat.Server.Application.RoomSecurity;

public interface IRoomSecurityService
{
    string GenerateStrongRoomCode();
    Task<JoinLink> CreateTemporaryJoinLinkAsync(Guid sessionId, int expirationMinutes = 60, CancellationToken ct = default);
    Task<Guid?> ValidateAndConsumeJoinLinkAsync(string linkToken, CancellationToken ct = default);
    Task RevokeJoinLinkAsync(Guid linkId, CancellationToken ct = default);
}

public sealed class RoomSecurityService : IRoomSecurityService
{
    private readonly BankersSeatDbContext _db;
    private readonly ILogger<RoomSecurityService> _logger;

    public RoomSecurityService(BankersSeatDbContext db, ILogger<RoomSecurityService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string GenerateStrongRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new System.Random();
        var roomCode = new string(Enumerable.Range(0, 8)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());

        return roomCode;
    }

    public async Task<JoinLink> CreateTemporaryJoinLinkAsync(Guid sessionId, int expirationMinutes = 60, CancellationToken ct = default)
    {
        var linkTokenBytes = new byte[32];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(linkTokenBytes);
        }

        var linkToken = Convert.ToBase64String(linkTokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var link = new JoinLinkEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            LinkToken = linkToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(expirationMinutes),
            CreatedAtUtc = DateTime.UtcNow,
            UseCount = 0,
            IsRevoked = false
        };

        _db.JoinLinks.Add(link);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created temporary join link for session {SessionId}, expires in {Minutes} minutes", sessionId, expirationMinutes);

        return new JoinLink
        {
            Id = link.Id,
            SessionId = link.SessionId,
            LinkToken = link.LinkToken,
            ExpiresAtUtc = link.ExpiresAtUtc,
            CreatedAtUtc = link.CreatedAtUtc
        };
    }

    public async Task<Guid?> ValidateAndConsumeJoinLinkAsync(string linkToken, CancellationToken ct = default)
    {
        var link = await _db.JoinLinks.FirstOrDefaultAsync(l => l.LinkToken == linkToken && !l.IsRevoked, ct);

        if (link == null || link.ExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogWarning("Invalid or expired join link attempted: {LinkToken}", linkToken);
            return null;
        }

        link.UseCount++;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Join link consumed for session {SessionId}", link.SessionId);

        return link.SessionId;
    }

    public async Task RevokeJoinLinkAsync(Guid linkId, CancellationToken ct = default)
    {
        var link = await _db.JoinLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct);
        if (link != null)
        {
            link.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Revoked join link {LinkId}", linkId);
        }
    }
}

public sealed class JoinLink
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required string LinkToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
