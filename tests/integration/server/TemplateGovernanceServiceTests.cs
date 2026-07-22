using Xunit;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankersSeat.Tests.Integration.Templates;

public class TemplateGovernanceServiceTests : IAsyncLifetime
{
    private readonly BankersSeatDbContext dbContext;
    private readonly ITemplateGovernanceService governanceService;
    private readonly ILogger<TemplateGovernanceService> logger;
    private Guid publisherUserId;
    private const string TemplateId = "test-template";
    private const string EditionId = "v1";
    private const string Author = "Test Author";

    public TemplateGovernanceServiceTests()
    {
        var options = new DbContextOptionsBuilder<BankersSeatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        dbContext = new BankersSeatDbContext(options);
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
        logger = loggerFactory.CreateLogger<TemplateGovernanceService>();
        governanceService = new TemplateGovernanceService(dbContext, logger);
    }

    public async Task InitializeAsync()
    {
        publisherUserId = Guid.NewGuid();
        var publisher = new UserAccountEntity
        {
            Id = publisherUserId,
            Email = "publisher@example.com",
            PasswordHashBcrypt = "hash",
            DisplayName = "Publisher",
            CreatedAtUtc = DateTime.UtcNow,
            LastAuthenticatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };
        dbContext.UserAccounts.Add(publisher);
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task PublishTemplate_CreatesMetadata()
    {
        await governanceService.PublishTemplateAsync(
            TemplateId,
            EditionId,
            publisherUserId,
            Author,
            "author@example.com",
            "https://example.com",
            "MIT",
            CancellationToken.None
        );

        var metadata = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == TemplateId && m.EditionId == EditionId);

        Assert.NotNull(metadata);
        Assert.Equal(Author, metadata.Author);
        Assert.Equal("MIT", metadata.License);
        Assert.Equal("Published", metadata.TemplateStatus);
        Assert.Equal("Pending", metadata.ModerationStatus);
    }

    [Fact]
    public async Task PublishTemplate_InvalidLicense_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            governanceService.PublishTemplateAsync(
                TemplateId,
                EditionId,
                publisherUserId,
                Author,
                "author@example.com",
                "https://example.com",
                "InvalidLicense",
                CancellationToken.None
            )
        );

        Assert.Contains("Invalid license", ex.Message);
    }

    [Fact]
    public async Task PublishTemplate_ValidSpdxLicenses()
    {
        var validLicenses = new[] { "MIT", "Apache-2.0", "GPL-3.0", "CC-BY-4.0", "Proprietary" };

        foreach (var license in validLicenses)
        {
            var templateId = $"{TemplateId}-{license}";
            await governanceService.PublishTemplateAsync(
                templateId,
                EditionId,
                publisherUserId,
                Author,
                null,
                null,
                license,
                CancellationToken.None
            );

            var metadata = await dbContext.TemplateMetadata
                .FirstOrDefaultAsync(m => m.TemplateId == templateId);

            Assert.NotNull(metadata);
            Assert.Equal(license, metadata.License);
        }
    }

    [Fact]
    public async Task PublishTemplate_Duplicate_Throws()
    {
        await governanceService.PublishTemplateAsync(
            TemplateId,
            EditionId,
            publisherUserId,
            Author,
            null,
            null,
            "MIT",
            CancellationToken.None
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            governanceService.PublishTemplateAsync(
                TemplateId,
                EditionId,
                publisherUserId,
                Author,
                null,
                null,
                "MIT",
                CancellationToken.None
            )
        );

        Assert.Contains("already published", ex.Message);
    }

    [Fact]
    public async Task ApproveTemplate_UpdatesStatus()
    {
        await governanceService.PublishTemplateAsync(
            TemplateId,
            EditionId,
            publisherUserId,
            Author,
            null,
            null,
            "MIT",
            CancellationToken.None
        );

        await governanceService.ApproveTemplateAsync(TemplateId, EditionId, CancellationToken.None);

        var metadata = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == TemplateId);

        Assert.NotNull(metadata);
        Assert.Equal("Approved", metadata.ModerationStatus);
    }

    [Fact]
    public async Task RejectTemplate_UpdatesStatusWithReason()
    {
        await governanceService.PublishTemplateAsync(
            TemplateId,
            EditionId,
            publisherUserId,
            Author,
            null,
            null,
            "MIT",
            CancellationToken.None
        );

        const string reason = "License violation";
        await governanceService.RejectTemplateAsync(TemplateId, EditionId, reason, CancellationToken.None);

        var metadata = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == TemplateId);

        Assert.NotNull(metadata);
        Assert.Equal("Rejected", metadata.ModerationStatus);
        Assert.Contains(reason, metadata.FlagReasons);
    }

    [Fact]
    public async Task FlagTemplate_StoresReasons()
    {
        await governanceService.PublishTemplateAsync(
            TemplateId,
            EditionId,
            publisherUserId,
            Author,
            null,
            null,
            "MIT",
            CancellationToken.None
        );

        var reasons = new[] { "Offensive content", "Trademark violation" };
        await governanceService.FlagTemplateAsync(TemplateId, EditionId, reasons, CancellationToken.None);

        var metadata = await dbContext.TemplateMetadata
            .FirstOrDefaultAsync(m => m.TemplateId == TemplateId);

        Assert.NotNull(metadata);
        Assert.Equal("Flagged", metadata.ModerationStatus);
        Assert.NotNull(metadata.FlagReasons);
        Assert.Contains("Offensive content", metadata.FlagReasons);
    }

    [Fact]
    public async Task GetModerationQueue_ReturnsPendingTemplates()
    {
        // Create one pending and one approved template
        await governanceService.PublishTemplateAsync(
            "template-1",
            EditionId,
            publisherUserId,
            Author,
            null,
            null,
            "MIT",
            CancellationToken.None
        );

        await governanceService.PublishTemplateAsync(
            "template-2",
            EditionId,
            publisherUserId,
            Author,
            null,
            null,
            "Apache-2.0",
            CancellationToken.None
        );

        await governanceService.ApproveTemplateAsync("template-2", EditionId, CancellationToken.None);

        var queue = await governanceService.GetModerationQueueAsync(0, 50, CancellationToken.None);

        Assert.Single(queue.Items);
        Assert.Equal("template-1", queue.Items[0].TemplateId);
        Assert.Equal(2, queue.TotalCount);
        Assert.Equal(1, queue.PendingCount);
    }

    [Fact]
    public async Task GetPublicTemplates_ReturnsApprovedPublished()
    {
        await governanceService.PublishTemplateAsync(
            "template-1",
            EditionId,
            publisherUserId,
            "Author 1",
            null,
            null,
            "MIT",
            CancellationToken.None
        );

        await governanceService.PublishTemplateAsync(
            "template-2",
            EditionId,
            publisherUserId,
            "Author 2",
            null,
            null,
            "Apache-2.0",
            CancellationToken.None
        );

        await governanceService.ApproveTemplateAsync("template-2", EditionId, CancellationToken.None);

        var publicTemplates = await governanceService.GetPublicTemplatesAsync(0, 50, CancellationToken.None);

        Assert.Single(publicTemplates);
        Assert.Equal("template-2", publicTemplates[0].TemplateId);
        Assert.Equal("Approved", publicTemplates[0].ModerationStatus);
    }

    [Fact]
    public async Task GetMetadata_ReturnsTemplateInfo()
    {
        await governanceService.PublishTemplateAsync(
            TemplateId,
            EditionId,
            publisherUserId,
            Author,
            "author@example.com",
            "https://example.com",
            "MIT",
            CancellationToken.None
        );

        var metadata = await governanceService.GetMetadataAsync(TemplateId, EditionId, CancellationToken.None);

        Assert.NotNull(metadata);
        Assert.Equal(TemplateId, metadata.TemplateId);
        Assert.Equal(EditionId, metadata.EditionId);
        Assert.Equal(Author, metadata.Author);
        Assert.Equal("author@example.com", metadata.AuthorEmail);
        Assert.Equal("MIT", metadata.License);
    }

    [Fact]
    public async Task GetMetadata_NonExistent_ReturnsNull()
    {
        var metadata = await governanceService.GetMetadataAsync("nonexistent", EditionId, CancellationToken.None);

        Assert.Null(metadata);
    }
}
