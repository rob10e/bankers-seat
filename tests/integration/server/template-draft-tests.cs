using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Domain.Templates;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;

namespace BankersSeat.Server.Integration.Tests.Templates;

/// <summary>
/// Integration tests for TemplateDraftService.
/// Tests CRUD operations, authorization, and draft lifecycle.
/// </summary>
public class TemplateDraftServiceTests
{
    private readonly ITemplateDraftService _draftService;
    private readonly Mock<ITemplateCatalogService> _mockCatalogService;
    private readonly Mock<ILogger<TemplateDraftService>> _mockLogger;

    public TemplateDraftServiceTests()
    {
        _mockCatalogService = new Mock<ITemplateCatalogService>();
        _mockLogger = new Mock<ILogger<TemplateDraftService>>();
        _draftService = new TemplateDraftService(_mockCatalogService.Object, _mockLogger.Object);
    }

    private static TemplateSnapshot CreateMockSnapshot(string templateId = "test-template")
    {
        return new TemplateSnapshot(
            Id: Guid.NewGuid(),
            Identity: new TemplateIdentity(templateId, "edition-1", "1.0.0"),
            SchemaVersion: 1,
            ContentHash: "hash123",
            TemplateJson: "{\"id\":\"" + templateId + "\"}",
            StartingPlayerBalance: 1500,
            AllowPlayerOverdraft: false,
            CreatedAtUtc: DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateDraftAsync_WithValidTemplate_CreatesDraftSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = "monopoly";
        var snapshot = CreateMockSnapshot(templateId);

        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(templateId, "edition-1", "1.0.0", default))
            .ReturnsAsync(snapshot);

        // Act
        var draft = await _draftService.CreateDraftAsync(userId, templateId, "edition-1", "1.0.0");

        // Assert
        Assert.NotNull(draft);
        Assert.Equal(userId, draft.UserId);
        Assert.Equal(templateId, draft.TemplateId);
        Assert.NotEqual(Guid.Empty, draft.DraftId);
    }

    [Fact]
    public async Task CreateDraftAsync_WithInvalidTemplate_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync("missing", "edition-1", "1.0.0", default))
            .ReturnsAsync((TemplateSnapshot?)null);

        // Act
        var draft = await _draftService.CreateDraftAsync(userId, "missing", "edition-1", "1.0.0");

        // Assert
        Assert.Null(draft);
    }

    [Fact]
    public async Task GetDraftAsync_AsCreator_ReturnsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var snapshot = CreateMockSnapshot();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(snapshot);

        var createdDraft = await _draftService.CreateDraftAsync(userId, "test-template", "edition-1", "1.0.0");
        Assert.NotNull(createdDraft);

        // Act
        var retrieved = await _draftService.GetDraftAsync(createdDraft.DraftId, userId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(createdDraft.DraftId, retrieved.DraftId);
    }

    [Fact]
    public async Task GetDraftAsync_AsNonCreator_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var snapshot = CreateMockSnapshot();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(snapshot);

        var createdDraft = await _draftService.CreateDraftAsync(userId, "test-template", "edition-1", "1.0.0");
        Assert.NotNull(createdDraft);

        // Act
        var retrieved = await _draftService.GetDraftAsync(createdDraft.DraftId, otherUserId);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task ListDraftsAsync_ReturnsPaginatedResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var snapshot = CreateMockSnapshot();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(snapshot);

        // Create 5 drafts
        for (int i = 0; i < 5; i++)
        {
            await _draftService.CreateDraftAsync(userId, "test-template", "edition-1", "1.0.0");
        }

        // Act
        var page1 = await _draftService.ListDraftsAsync(userId, pageSize: 3, pageNumber: 1);
        var page2 = await _draftService.ListDraftsAsync(userId, pageSize: 3, pageNumber: 2);

        // Assert
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.Drafts.Length);
        Assert.Equal(2, page2.Drafts.Length);
    }

    [Fact]
    public async Task UpdateDraftAsync_WithValidData_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var snapshot = CreateMockSnapshot();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(snapshot);

        var createdDraft = await _draftService.CreateDraftAsync(userId, "test-template", "edition-1", "1.0.0");
        Assert.NotNull(createdDraft);

        var newData = new { id = "test-template", name = "Updated Name" };

        // Act
        var updated = await _draftService.UpdateDraftAsync(createdDraft.DraftId, userId, newData);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(newData, updated.TemplateData);
    }

    [Fact]
    public async Task UpdateDraftAsync_AsNonCreator_ReturnsFalseUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var snapshot = CreateMockSnapshot();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(snapshot);

        var createdDraft = await _draftService.CreateDraftAsync(userId, "test-template", "edition-1", "1.0.0");
        Assert.NotNull(createdDraft);

        var newData = new { id = "test-template", name = "Updated Name" };

        // Act
        var updated = await _draftService.UpdateDraftAsync(createdDraft.DraftId, otherUserId, newData);

        // Assert
        Assert.Null(updated);
    }

    [Fact]
    public async Task DeleteDraftAsync_AsCreator_DeletesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var snapshot = CreateMockSnapshot();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(snapshot);

        var createdDraft = await _draftService.CreateDraftAsync(userId, "test-template", "edition-1", "1.0.0");
        Assert.NotNull(createdDraft);

        // Act
        var deleted = await _draftService.DeleteDraftAsync(createdDraft.DraftId, userId);

        // Assert
        Assert.True(deleted);

        // Verify draft is gone
        var retrieved = await _draftService.GetDraftAsync(createdDraft.DraftId, userId);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteDraftAsync_AsNonCreator_ReturnsFalseUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var snapshot = CreateMockSnapshot();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(snapshot);

        var createdDraft = await _draftService.CreateDraftAsync(userId, "test-template", "edition-1", "1.0.0");
        Assert.NotNull(createdDraft);

        // Act
        var deleted = await _draftService.DeleteDraftAsync(createdDraft.DraftId, otherUserId);

        // Assert
        Assert.False(deleted);

        // Verify draft still exists for creator
        var retrieved = await _draftService.GetDraftAsync(createdDraft.DraftId, userId);
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task ExportDraftAsync_ReturnsValidJson()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var snapshot = CreateMockSnapshot();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(snapshot);

        var createdDraft = await _draftService.CreateDraftAsync(userId, "test-template", "edition-1", "1.0.0");
        Assert.NotNull(createdDraft);

        // Act
        var exported = await _draftService.ExportDraftAsync(createdDraft.DraftId, userId);

        // Assert
        Assert.NotNull(exported);
        Assert.True(exported.Length > 0);

        var json = System.Text.Encoding.UTF8.GetString(exported);
        Assert.Contains("test-template", json);
    }

    [Fact]
    public async Task ExportDraftAsync_AsNonCreator_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var snapshot = CreateMockSnapshot();
        _mockCatalogService
            .Setup(s => s.GetTemplateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(snapshot);

        var createdDraft = await _draftService.CreateDraftAsync(userId, "test-template", "edition-1", "1.0.0");
        Assert.NotNull(createdDraft);

        // Act
        var exported = await _draftService.ExportDraftAsync(createdDraft.DraftId, otherUserId);

        // Assert
        Assert.Null(exported);
    }
}
