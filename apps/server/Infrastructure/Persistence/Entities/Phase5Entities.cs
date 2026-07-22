namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

/// <summary>
/// TemplateShareEntity: Represents a private share of a template with a specific user.
/// </summary>
public sealed class TemplateShareEntity
{
    public required Guid Id { get; set; }
    public required string TemplateId { get; set; }
    public required Guid SharedByUserId { get; set; }
    public required string SharedWithEmail { get; set; }
    public required DateTime GrantedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

/// <summary>
/// TemplateMetadataEntity: Stores licensing, author, and governance information for templates.
/// </summary>
public sealed class TemplateMetadataEntity
{
    public required Guid Id { get; set; }
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required Guid OwnerUserId { get; set; }
    public required string Author { get; set; }
    public string? AuthorEmail { get; set; }
    public string? AuthorUrl { get; set; }
    public required string License { get; set; }
    public required DateTime PublishedAtUtc { get; set; }
    public required string TemplateStatus { get; set; } // Draft, Published, Featured, Archived
    public required string ModerationStatus { get; set; } // Pending, Approved, Flagged, Rejected
    public int DownloadCount { get; set; }
    public required DateTime UpdatedAtUtc { get; set; }
    public string? FlagReasons { get; set; } // JSON array
}
