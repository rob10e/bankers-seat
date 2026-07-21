using System.Text.Json;
using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Domain.Templates;

namespace BankersSeat.Server.Application.Templates;

/// <summary>
/// Service for managing template drafts and editing workflow.
/// Drafts allow non-technical authors to create templates visually without JSON editing.
/// </summary>
public interface ITemplateDraftService
{
    /// <summary>
    /// Create a new draft from an existing template or from scratch.
    /// </summary>
    Task<TemplateDraftResponse?> CreateDraftAsync(
        Guid userId,
        string templateId,
        string editionId,
        string templateVersion,
        CancellationToken ct = default);

    /// <summary>
    /// Get a draft by ID.
    /// </summary>
    Task<TemplateDraftResponse?> GetDraftAsync(
        Guid draftId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// List drafts for the current user.
    /// </summary>
    Task<ListTemplateDraftsResponse> ListDraftsAsync(
        Guid userId,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken ct = default);

    /// <summary>
    /// Update draft data. Validates JSON structure against schema.
    /// </summary>
    Task<TemplateDraftResponse?> UpdateDraftAsync(
        Guid draftId,
        Guid userId,
        object templateData,
        CancellationToken ct = default);

    /// <summary>
    /// Delete a draft. Only the creator can delete.
    /// </summary>
    Task<bool> DeleteDraftAsync(
        Guid draftId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Export a draft as a template.json file for download.
    /// </summary>
    Task<byte[]?> ExportDraftAsync(
        Guid draftId,
        Guid userId,
        CancellationToken ct = default);
}

/// <summary>
/// In-memory implementation of draft service for Phase 5.
/// In production, this would use a database table for persistence.
/// </summary>
public sealed class TemplateDraftService : ITemplateDraftService
{
    private readonly ITemplateCatalogService _catalogService;
    private readonly ILogger<TemplateDraftService> _logger;

    // In-memory storage: draftId -> (userId, TemplateDraft)
    private static readonly Dictionary<Guid, (Guid userId, TemplateDraft draft)> Drafts = new();
    private static readonly object DraftLock = new();

    public TemplateDraftService(
        ITemplateCatalogService catalogService,
        ILogger<TemplateDraftService> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public async Task<TemplateDraftResponse?> CreateDraftAsync(
        Guid userId,
        string templateId,
        string editionId,
        string templateVersion,
        CancellationToken ct = default)
    {
        // Load existing template as basis for draft
        var snapshot = await _catalogService.GetTemplateSnapshotAsync(
            templateId, editionId, templateVersion, ct);
        
        if (snapshot == null)
        {
            _logger.LogWarning("Template not found: {TemplateId}/{EditionId}/{Version}",
                templateId, editionId, templateVersion);
            return null;
        }

        // Use template.json as the editable data
        object templateData;
        try
        {
            var json = JsonSerializer.Serialize(snapshot.TemplateJson, new JsonSerializerOptions
            {
                WriteIndented = false,
            });
            templateData = JsonSerializer.Deserialize<object>(json) ?? new { };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid template JSON: {TemplateId}", templateId);
            return null;
        }

        var draftId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var draft = new TemplateDraft
        {
            DraftId = draftId,
            UserId = userId,
            TemplateId = templateId,
            EditionId = editionId,
            TemplateVersion = templateVersion,
            TemplateData = templateData,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        lock (DraftLock)
        {
            Drafts[draftId] = (userId, draft);
        }

        _logger.LogInformation("Draft created: {DraftId} by {UserId} from {TemplateId}",
            draftId, userId, templateId);

        return MapToResponse(draft);
    }

    public async Task<TemplateDraftResponse?> GetDraftAsync(
        Guid draftId,
        Guid userId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask; // Async signature for future persistence
        lock (DraftLock)
        {
            if (!Drafts.TryGetValue(draftId, out var entry))
            {
                return null;
            }

            var draftUserId = entry.userId;
            var draft = entry.draft;

            // Authorization: only creator can access
            if (draftUserId != userId)
            {
                _logger.LogWarning("Unauthorized draft access: {DraftId} by {UserId}", draftId, userId);
                return null;
            }

            return MapToResponse(draft);
        }
    }

    public async Task<ListTemplateDraftsResponse> ListDraftsAsync(
        Guid userId,
        int pageSize = 20,
        int pageNumber = 1,
        CancellationToken ct = default)
    {
        await Task.CompletedTask; // Async signature for future persistence
        lock (DraftLock)
        {
            var userDrafts = Drafts.Values
                .Where(x => x.userId == userId)
                .OrderByDescending(x => x.draft.UpdatedAtUtc)
                .ToList();

            var skip = (pageNumber - 1) * pageSize;
            var paginated = userDrafts
                .Skip(skip)
                .Take(pageSize)
                .Select(x => MapToResponse(x.draft))
                .ToArray();

            return new ListTemplateDraftsResponse
            {
                Drafts = paginated,
                TotalCount = userDrafts.Count,
            };
        }
    }

    public async Task<TemplateDraftResponse?> UpdateDraftAsync(
        Guid draftId,
        Guid userId,
        object templateData,
        CancellationToken ct = default)
    {
        // TODO: In production, validate templateData against JSON Schema
        // For now, accept any object and let frontend validation handle it

        await Task.CompletedTask; // Async signature for future persistence
        lock (DraftLock)
        {
            if (!Drafts.TryGetValue(draftId, out var entry))
            {
                return null;
            }

            var draftUserId = entry.userId;
            var draft = entry.draft;

            // Authorization: only creator can update
            if (draftUserId != userId)
            {
                _logger.LogWarning("Unauthorized draft update: {DraftId} by {UserId}", draftId, userId);
                return null;
            }

            // Update draft in place
            draft.TemplateData = templateData;
            draft.UpdatedAtUtc = DateTime.UtcNow;

            _logger.LogInformation("Draft updated: {DraftId} by {UserId}", draftId, userId);

            return MapToResponse(draft);
        }
    }

    public async Task<bool> DeleteDraftAsync(
        Guid draftId,
        Guid userId,
        CancellationToken ct = default)
    {
        await Task.CompletedTask; // Async signature for future persistence
        lock (DraftLock)
        {
            if (!Drafts.TryGetValue(draftId, out var entry))
            {
                return false;
            }

            var draftUserId = entry.userId;
            // Authorization: only creator can delete
            if (draftUserId != userId)
            {
                _logger.LogWarning("Unauthorized draft deletion: {DraftId} by {UserId}", draftId, userId);
                return false;
            }

            var removed = Drafts.Remove(draftId);
            if (removed)
            {
                _logger.LogInformation("Draft deleted: {DraftId} by {UserId}", draftId, userId);
            }

            return removed;
        }
    }

    public async Task<byte[]?> ExportDraftAsync(
        Guid draftId,
        Guid userId,
        CancellationToken ct = default)
    {
        var draft = await GetDraftAsync(draftId, userId, ct);
        if (draft == null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(draft.TemplateData, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            return System.Text.Encoding.UTF8.GetBytes(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export draft: {DraftId}", draftId);
            return null;
        }
    }

    private static TemplateDraftResponse MapToResponse(TemplateDraft draft)
    {
        return new TemplateDraftResponse
        {
            DraftId = draft.DraftId,
            UserId = draft.UserId,
            TemplateId = draft.TemplateId,
            EditionId = draft.EditionId,
            TemplateVersion = draft.TemplateVersion,
            TemplateData = draft.TemplateData,
            CreatedAtUtc = draft.CreatedAtUtc,
            UpdatedAtUtc = draft.UpdatedAtUtc,
        };
    }
}

/// <summary>
/// In-memory model for a template draft during editing.
/// </summary>
public sealed class TemplateDraft
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
