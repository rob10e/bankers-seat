namespace BankersSeat.Server.Api.V1.Contracts;

/// <summary>
/// Contracts for Phase 5 — Template Ecosystem operations.
/// </summary>

// Template Package Export/Import
public sealed class ExportTemplateRequest
{
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string TemplateVersion { get; set; }
}

public sealed class ExportTemplateResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? PackageUrl { get; set; } // Download URL for ZIP
}

public sealed class ImportTemplateResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? TemplateId { get; set; }
    public string? EditionId { get; set; }
    public string? TemplateVersion { get; set; }
    public string? DestinationPath { get; set; }
}

// Template Preview
public sealed class TemplatePreviewResponse
{
    public required string TemplateId { get; set; }
    public required string TemplateName { get; set; }
    public required string EditionId { get; set; }
    public required string EditionName { get; set; }
    public required string TemplateVersion { get; set; }
    public required string Description { get; set; }
    public required CurrencyInfo Currency { get; set; }
    public required BankInfo Bank { get; set; }
    public required PlayerCountInfo PlayerCount { get; set; }
    public DenominationInfo[]? Denominations { get; set; }
    public FieldInfo[]? PlayerFields { get; set; }
    public ActionInfo[]? Actions { get; set; }
    public AssetInfo? Assets { get; set; }
}

public sealed class CurrencyInfo
{
    public required string Code { get; set; }
    public required string Symbol { get; set; }
    public required string Name { get; set; }
}

public sealed class BankInfo
{
    public required int StartingPlayerBalance { get; set; }
    public required string BankMode { get; set; }
    public required bool AllowPlayerOverdraft { get; set; }
}

public sealed class PlayerCountInfo
{
    public required int Min { get; set; }
    public required int Max { get; set; }
}

public sealed class DenominationInfo
{
    public required int Value { get; set; }
    public required string Label { get; set; }
    public string? Asset { get; set; }
}

public sealed class FieldInfo
{
    public required string Id { get; set; }
    public required string Label { get; set; }
    public required string Type { get; set; }
}

public sealed class ActionInfo
{
    public required string Id { get; set; }
    public required string Label { get; set; }
    public required string ActionType { get; set; }
}

public sealed class AssetInfo
{
    public string? Logo { get; set; }
    public string? Thumbnail { get; set; }
    public string? Background { get; set; }
}

// Template Editor & Drafts
public sealed class CreateTemplateDraftRequest
{
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string TemplateVersion { get; set; }
}

public sealed class UpdateTemplateDraftRequest
{
    public required object TemplateData { get; set; } // Full template snapshot
}

public sealed class TemplateDraftResponse
{
    public required Guid DraftId { get; set; }
    public required Guid UserId { get; set; }
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string TemplateVersion { get; set; }
    public required object TemplateData { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime UpdatedAtUtc { get; set; }
}

public sealed class ListTemplateDraftsResponse
{
    public required TemplateDraftResponse[] Drafts { get; set; }
    public required int TotalCount { get; set; }
}

// Template Diff/Migration
public sealed class TemplateDiffRequest
{
    public required string FromTemplateId { get; set; }
    public required string FromEditionId { get; set; }
    public required string FromVersion { get; set; }
    public required string ToTemplateId { get; set; }
    public required string ToEditionId { get; set; }
    public required string ToVersion { get; set; }
}

public sealed class TemplateDiffResponse
{
    public bool CompatibleUpgrade { get; set; }
    public string[]? BreakingChanges { get; set; }
    public string[]? NewFeatures { get; set; }
    public string[]? RemovedFeatures { get; set; }
    public string[]? ChangedFields { get; set; }
    public string? MigrationAdvice { get; set; }
    public string? Changelog { get; set; }
}

// Template Private Sharing
public sealed class ShareTemplateRequest
{
    public required string[] RecipientEmails { get; set; }
}

public sealed class ShareTemplateResponse
{
    public required Guid[] ShareIds { get; set; }
    public int SuccessCount { get; set; }
    public string[]? Errors { get; set; }
}

public sealed class TemplateShareListResponse
{
    public required TemplateShareItemResponse[] Shares { get; set; }
}

public sealed class TemplateShareItemResponse
{
    public required Guid ShareId { get; set; }
    public required string TemplateId { get; set; }
    public required string SharedWithEmail { get; set; }
    public required string SharedByName { get; set; }
    public required DateTime GrantedAtUtc { get; set; }
    public required bool IsActive { get; set; }
}

public sealed class SharedWithMeResponse
{
    public required SharedTemplateItemResponse[] Templates { get; set; }
}

public sealed class SharedTemplateItemResponse
{
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string SharedByName { get; set; }
    public required DateTime GrantedAtUtc { get; set; }
}

// Template Marketplace & Governance
public sealed class PublishTemplateRequest
{
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string Author { get; set; }
    public string? AuthorEmail { get; set; }
    public string? AuthorUrl { get; set; }
    public required string License { get; set; } // MIT, CC-BY-4.0, CC0-1.0, Proprietary, etc.
}

public sealed class PublishTemplateResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public Guid? MetadataId { get; set; }
}

public sealed class TemplateGovernanceInfoResponse
{
    public required Guid MetadataId { get; set; }
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string Author { get; set; }
    public string? AuthorEmail { get; set; }
    public string? AuthorUrl { get; set; }
    public required string License { get; set; }
    public required DateTime PublishedAtUtc { get; set; }
    public required string TemplateStatus { get; set; }
    public required string ModerationStatus { get; set; }
    public required int DownloadCount { get; set; }
    public string[]? FlagReasons { get; set; }
}

// Admin Governance
public sealed class ModerationQueueItemResponse
{
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string Author { get; set; }
    public required DateTime PublishedAtUtc { get; set; }
    public required string ModerationStatus { get; set; }
    public string[]? FlagReasons { get; set; }
}

public sealed class ApproveTemplateRequest
{
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
}

public sealed class RejectTemplateRequest
{
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string Reason { get; set; }
}

public sealed class FlagTemplateRequest
{
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string[] Reasons { get; set; }
}

public sealed class ModerationQueueResponse
{
    public required ModerationQueueItemResponse[] Items { get; set; }
    public required int TotalCount { get; set; }
    public required int PendingCount { get; set; }
}
