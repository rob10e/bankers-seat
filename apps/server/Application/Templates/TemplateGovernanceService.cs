using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BankersSeat.Server.Application.Templates;

public sealed class TemplateGovernanceService : ITemplateGovernanceService
{
    private readonly BankersSeatDbContext dbContext;
    private readonly ILogger<TemplateGovernanceService> logger;

    // SPDX license identifiers and proprietary option
    private static readonly HashSet<string> ValidLicenses = new(StringComparer.OrdinalIgnoreCase)
    {
        "MIT", "Apache-2.0", "GPL-3.0", "GPL-2.0", "BSD-3-Clause", "BSD-2-Clause",
        "CC0-1.0", "CC-BY-4.0", "CC-BY-SA-4.0", "ISC", "Unlicense", "Proprietary"
    };

    public TemplateGovernanceService(
        BankersSeatDbContext dbContext,
        ILogger<TemplateGovernanceService> logger
    )
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    public async Task PublishTemplateAsync(
        string templateId,
        string editionId,
        Guid publisherUserId,
        string author,
        string? authorEmail,
        string? authorUrl,
        string license,
        CancellationToken cancellationToken
    )
    {
        // Validate license
        if (!ValidLicenses.Contains(license))
        {
            throw new ArgumentException($"Invalid license: {license}. Must be a valid SPDX identifier or 'Proprietary'.");
        }

        // Check for existing metadata
        var existing = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == templateId && m.EditionId == editionId, cancellationToken);

        if (existing != null)
        {
            logger.LogWarning(
                "Attempted to publish already-published template {TemplateId} {EditionId}",
                templateId,
                editionId
            );
            throw new InvalidOperationException("Template is already published.");
        }

        var metadata = new TemplateMetadataEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            EditionId = editionId,
            OwnerUserId = publisherUserId,
            Author = author,
            AuthorEmail = authorEmail?.ToLowerInvariant(),
            AuthorUrl = authorUrl,
            License = license,
            PublishedAtUtc = DateTime.UtcNow,
            TemplateStatus = "Published",
            ModerationStatus = "Pending",
            DownloadCount = 0,
            UpdatedAtUtc = DateTime.UtcNow,
            FlagReasons = null
        };

        dbContext.TemplateMetadata.Add(metadata);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Published template {TemplateId} {EditionId} by {UserId} with license {License}",
            templateId,
            editionId,
            publisherUserId,
            license
        );
    }

    public async Task<ModerationQueueInfo> GetModerationQueueAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default
    )
    {
        take = Math.Min(take, 100); // Cap at 100 per page

        var pendingQuery = dbContext.TemplateMetadata
            .Where(m => m.ModerationStatus == "Pending");

        var totalQuery = dbContext.TemplateMetadata;

        var total = await totalQuery.CountAsync(cancellationToken);
        var pending = await pendingQuery.CountAsync(cancellationToken);

        var items = await pendingQuery
            .OrderBy(m => m.PublishedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(m => new ModerationQueueItem(
                m.TemplateId,
                m.EditionId,
                m.Author,
                m.PublishedAtUtc,
                m.ModerationStatus,
                string.IsNullOrEmpty(m.FlagReasons)
                    ? null
                    : JsonSerializer.Deserialize<string[]>(m.FlagReasons)
            ))
            .ToListAsync(cancellationToken);

        return new ModerationQueueInfo(items, total, pending);
    }

    public async Task ApproveTemplateAsync(
        string templateId,
        string editionId,
        CancellationToken cancellationToken
    )
    {
        var metadata = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == templateId && m.EditionId == editionId, cancellationToken);

        if (metadata == null)
        {
            logger.LogWarning(
                "Attempted to approve non-existent template {TemplateId} {EditionId}",
                templateId,
                editionId
            );
            throw new InvalidOperationException("Template not found.");
        }

        metadata.ModerationStatus = "Approved";
        metadata.UpdatedAtUtc = DateTime.UtcNow;
        metadata.FlagReasons = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Approved template {TemplateId} {EditionId}",
            templateId,
            editionId
        );
    }

    public async Task RejectTemplateAsync(
        string templateId,
        string editionId,
        string reason,
        CancellationToken cancellationToken
    )
    {
        var metadata = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == templateId && m.EditionId == editionId, cancellationToken);

        if (metadata == null)
        {
            logger.LogWarning(
                "Attempted to reject non-existent template {TemplateId} {EditionId}",
                templateId,
                editionId
            );
            throw new InvalidOperationException("Template not found.");
        }

        metadata.ModerationStatus = "Rejected";
        metadata.UpdatedAtUtc = DateTime.UtcNow;
        metadata.FlagReasons = JsonSerializer.Serialize(new[] { reason });

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Rejected template {TemplateId} {EditionId}: {Reason}",
            templateId,
            editionId,
            reason
        );
    }

    public async Task FlagTemplateAsync(
        string templateId,
        string editionId,
        string[] reasons,
        CancellationToken cancellationToken
    )
    {
        var metadata = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == templateId && m.EditionId == editionId, cancellationToken);

        if (metadata == null)
        {
            logger.LogWarning(
                "Attempted to flag non-existent template {TemplateId} {EditionId}",
                templateId,
                editionId
            );
            throw new InvalidOperationException("Template not found.");
        }

        metadata.ModerationStatus = "Flagged";
        metadata.UpdatedAtUtc = DateTime.UtcNow;
        metadata.FlagReasons = JsonSerializer.Serialize(reasons);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Flagged template {TemplateId} {EditionId} with {ReasonCount} reasons",
            templateId,
            editionId,
            reasons.Length
        );
    }

    public async Task<TemplateGovernanceInfo?> GetMetadataAsync(
        string templateId,
        string editionId,
        CancellationToken cancellationToken
    )
    {
        var metadata = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == templateId && m.EditionId == editionId, cancellationToken);

        if (metadata == null)
        {
            return null;
        }

        return MapToGovernanceInfo(metadata);
    }

    public async Task<IReadOnlyList<TemplateGovernanceInfo>> GetPublicTemplatesAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default
    )
    {
        take = Math.Min(take, 100); // Cap at 100 per page

        var templates = await dbContext.TemplateMetadata
            .Where(m => m.TemplateStatus == "Published" && m.ModerationStatus == "Approved")
            .OrderByDescending(m => m.DownloadCount)
            .ThenByDescending(m => m.PublishedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(m => MapToGovernanceInfo(m))
            .ToListAsync(cancellationToken);

        return templates;
    }

    private static TemplateGovernanceInfo MapToGovernanceInfo(TemplateMetadataEntity metadata)
    {
        var flags = string.IsNullOrEmpty(metadata.FlagReasons)
            ? null
            : JsonSerializer.Deserialize<string[]>(metadata.FlagReasons);

        return new TemplateGovernanceInfo(
            metadata.Id,
            metadata.TemplateId,
            metadata.EditionId,
            metadata.Author,
            metadata.AuthorEmail,
            metadata.AuthorUrl,
            metadata.License,
            metadata.PublishedAtUtc,
            metadata.TemplateStatus,
            metadata.ModerationStatus,
            metadata.DownloadCount,
            flags
        );
    }
}
