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
    public string? ExpiresInDays { get; set; }
}

public sealed class ShareTemplateResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ShareCode { get; set; }
    public string? ShareUrl { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class TemplateAccessInfo
{
    public required Guid AccessId { get; set; }
    public required string TemplateId { get; set; }
    public required string AccessLevel { get; set; } // viewer, editor, owner
    public required string GrantedBy { get; set; }
    public required DateTime GrantedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

// Template Marketplace & Governance
public sealed class PublishTemplateRequest
{
    public required string TemplateId { get; set; }
    public required string License { get; set; } // MIT, CC0, CC-BY, Custom, etc.
    public required string Author { get; set; }
    public string? Repository { get; set; }
    public string? SupportEmail { get; set; }
    public string? Website { get; set; }
}

public sealed class PublishTemplateResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? PublishUrl { get; set; }
}

public sealed class TemplateMetadataResponse
{
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string TemplateName { get; set; }
    public required string License { get; set; }
    public required string Author { get; set; }
    public required DateTime PublishedAtUtc { get; set; }
    public required int DownloadCount { get; set; }
    public string? Repository { get; set; }
    public string? SupportEmail { get; set; }
    public string? Website { get; set; }
}

// Admin Governance
public sealed class ModerationQueueItem
{
    public required Guid Id { get; set; }
    public required string TemplateId { get; set; }
    public required string EditionId { get; set; }
    public required string Status { get; set; } // pending, approved, rejected
    public required string Author { get; set; }
    public required DateTime SubmittedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public string? LicenseIssue { get; set; }
}

public sealed class ApproveModerationRequest
{
    public required Guid ItemId { get; set; }
    public string? Comment { get; set; }
}

public sealed class RejectModerationRequest
{
    public required Guid ItemId { get; set; }
    public required string Reason { get; set; }
}

public sealed class ModerationQueueResponse
{
    public required ModerationQueueItem[] Items { get; set; }
    public required int TotalCount { get; set; }
    public required int PendingCount { get; set; }
}
