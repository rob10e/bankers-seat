using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankersSeat.Server.Application.Templates;

public sealed class TemplateShareService : ITemplateShareService
{
    private readonly BankersSeatDbContext dbContext;
    private readonly ILogger<TemplateShareService> logger;

    public TemplateShareService(
        BankersSeatDbContext dbContext,
        ILogger<TemplateShareService> logger
    )
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task<Guid> GrantShareAsync(
        string templateId,
        Guid sharingUserId,
        string recipientEmail,
        CancellationToken cancellationToken
    )
    {
        // Normalize email
        var normalizedEmail = recipientEmail.ToLowerInvariant();

        // Check for existing active share
        var existingShare = await dbContext.TemplateShares
            .Where(s =>
                s.TemplateId == templateId &&
                s.SharedWithEmail == normalizedEmail &&
                s.RevokedAtUtc == null
            )
            .FirstOrDefaultAsync(cancellationToken);

        if (existingShare != null)
        {
            logger.LogWarning(
                "Attempted to grant duplicate share for template {TemplateId} to {Email}",
                templateId,
                normalizedEmail
            );
            throw new InvalidOperationException(
                "This template is already shared with this user."
            );
        }

        var share = new TemplateShareEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            SharedByUserId = sharingUserId,
            SharedWithEmail = normalizedEmail,
            GrantedAtUtc = DateTime.UtcNow,
            RevokedAtUtc = null
        };

        dbContext.TemplateShares.Add(share);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Granted template share: {TemplateId} to {Email} by {UserId}",
            templateId,
            normalizedEmail,
            sharingUserId
        );

        return share.Id;
    }

    public async Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken)
    {
        var share = await dbContext.TemplateShares
            .FirstOrDefaultAsync(s => s.Id == shareId, cancellationToken);

        if (share == null)
        {
            logger.LogWarning("Attempted to revoke non-existent share {ShareId}", shareId);
            throw new InvalidOperationException("Share not found.");
        }

        if (share.RevokedAtUtc != null)
        {
            logger.LogWarning("Attempted to revoke already-revoked share {ShareId}", shareId);
            throw new InvalidOperationException("This share is already revoked.");
        }

        share.RevokedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Revoked template share {ShareId}", shareId);
    }

    public async Task<IReadOnlyList<TemplateShareInfo>> GetSharesAsync(
        string templateId,
        CancellationToken cancellationToken
    )
    {
        var shares = await dbContext.TemplateShares
            .Where(s => s.TemplateId == templateId)
            .Join(
                dbContext.UserAccounts,
                share => share.SharedByUserId,
                user => user.Id,
                (share, user) => new { share, user }
            )
            .Select(x => new TemplateShareInfo(
                x.share.Id,
                x.share.TemplateId,
                x.share.SharedWithEmail,
                x.user.DisplayName,
                x.share.GrantedAtUtc,
                x.share.RevokedAtUtc == null
            ))
            .ToListAsync(cancellationToken);

        return shares;
    }

    public async Task<IReadOnlyList<SharedTemplateInfo>> GetSharedWithMeAsync(
        string userEmail,
        CancellationToken cancellationToken
    )
    {
        var normalizedEmail = userEmail.ToLowerInvariant();

        var templates = await dbContext.TemplateShares
            .Where(s =>
                s.SharedWithEmail == normalizedEmail &&
                s.RevokedAtUtc == null
            )
            .Join(
                dbContext.UserAccounts,
                share => share.SharedByUserId,
                user => user.Id,
                (share, user) => new { share, user }
            )
            .Join(
                dbContext.TemplateMetadata,
                x => x.share.TemplateId,
                meta => meta.TemplateId,
                (x, meta) => new SharedTemplateInfo(
                    x.share.TemplateId,
                    meta.Author, // Use author as template name placeholder
                    meta.EditionId,
                    x.user.DisplayName,
                    x.share.GrantedAtUtc
                )
            )
            .ToListAsync(cancellationToken);

        return templates;
    }

    public async Task<bool> HasAccessAsync(
        string userEmail,
        string templateId,
        CancellationToken cancellationToken
    )
    {
        var normalizedEmail = userEmail.ToLowerInvariant();

        // Check if user is the owner
        var user = await dbContext.UserAccounts
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null)
        {
            return false;
        }

        var metadata = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == templateId, cancellationToken);

        if (metadata?.OwnerUserId == user.Id)
        {
            return true;
        }

        // Check if user has an active share
        var hasShare = await dbContext.TemplateShares
            .AnyAsync(s =>
                s.TemplateId == templateId &&
                s.SharedWithEmail == normalizedEmail &&
                s.RevokedAtUtc == null,
                cancellationToken
            );

        return hasShare;
    }
}
