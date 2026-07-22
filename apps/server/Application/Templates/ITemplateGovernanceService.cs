namespace BankersSeat.Server.Application.Templates;

public interface ITemplateGovernanceService
{
    /// <summary>
    /// Publish a template (mark as Published and set initial status).
    /// </summary>
    Task PublishTemplateAsync(
        string templateId,
        string editionId,
        Guid publisherUserId,
        string author,
        string? authorEmail,
        string? authorUrl,
        string license,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Get the moderation queue (pending items).
    /// </summary>
    Task<ModerationQueueInfo> GetModerationQueueAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Approve a template in moderation.
    /// </summary>
    Task ApproveTemplateAsync(
        string templateId,
        string editionId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Reject a template in moderation.
    /// </summary>
    Task RejectTemplateAsync(
        string templateId,
        string editionId,
        string reason,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Flag a template for review.
    /// </summary>
    Task FlagTemplateAsync(
        string templateId,
        string editionId,
        string[] reasons,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Get metadata for a template.
    /// </summary>
    Task<TemplateGovernanceInfo?> GetMetadataAsync(
        string templateId,
        string editionId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Get public templates (published and approved).
    /// </summary>
    Task<IReadOnlyList<TemplateGovernanceInfo>> GetPublicTemplatesAsync(
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default
    );
}

public sealed record TemplateGovernanceInfo(
    Guid MetadataId,
    string TemplateId,
    string EditionId,
    string Author,
    string? AuthorEmail,
    string? AuthorUrl,
    string License,
    DateTime PublishedAtUtc,
    string TemplateStatus,
    string ModerationStatus,
    int DownloadCount,
    string[]? FlagReasons
);

public sealed record ModerationQueueItem(
    string TemplateId,
    string EditionId,
    string Author,
    DateTime PublishedAtUtc,
    string ModerationStatus,
    string[]? FlagReasons
);

public sealed record ModerationQueueInfo(
    IReadOnlyList<ModerationQueueItem> Items,
    int TotalCount,
    int PendingCount
);
