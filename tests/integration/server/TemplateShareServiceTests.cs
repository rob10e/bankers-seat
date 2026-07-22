using Xunit;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankersSeat.Tests.Integration.Templates;

public class TemplateShareServiceTests : IAsyncLifetime
{
    private readonly BankersSeatDbContext dbContext;
    private readonly ITemplateShareService shareService;
    private readonly ILogger<TemplateShareService> logger;
    private Guid ownerUserId;
    private Guid recipientUserId;
    private const string TemplateId = "test-template";
    private const string OwnerEmail = "owner@example.com";
    private const string RecipientEmail = "recipient@example.com";

    public TemplateShareServiceTests()
    {
        var options = new DbContextOptionsBuilder<BankersSeatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        dbContext = new BankersSeatDbContext(options);
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
        logger = loggerFactory.CreateLogger<TemplateShareService>();
        shareService = new TemplateShareService(dbContext, logger);
    }

    public async Task InitializeAsync()
    {
        ownerUserId = Guid.NewGuid();
        recipientUserId = Guid.NewGuid();

        var owner = new UserAccountEntity
        {
            Id = ownerUserId,
            Email = OwnerEmail.ToLowerInvariant(),
            PasswordHashBcrypt = "hash",
            DisplayName = "Owner",
            CreatedAtUtc = DateTime.UtcNow,
            LastAuthenticatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };
        var recipient = new UserAccountEntity
        {
            Id = recipientUserId,
            Email = RecipientEmail.ToLowerInvariant(),
            PasswordHashBcrypt = "hash",
            DisplayName = "Recipient",
            CreatedAtUtc = DateTime.UtcNow,
            LastAuthenticatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };

        dbContext.UserAccounts.Add(owner);
        dbContext.UserAccounts.Add(recipient);
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GrantShare_CreatesNewShare()
    {
        var shareId = await shareService.GrantShareAsync(TemplateId, ownerUserId, RecipientEmail, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, shareId);
        var share = await dbContext.TemplateShares.FindAsync(shareId);
        Assert.NotNull(share);
        Assert.Equal(TemplateId, share.TemplateId);
        Assert.Equal(ownerUserId, share.SharedByUserId);
        Assert.Equal(RecipientEmail.ToLowerInvariant(), share.SharedWithEmail);
        Assert.Null(share.RevokedAtUtc);
    }

    [Fact]
    public async Task GrantShare_DuplicateShare_Throws()
    {
        await shareService.GrantShareAsync(TemplateId, ownerUserId, RecipientEmail, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            shareService.GrantShareAsync(TemplateId, ownerUserId, RecipientEmail, CancellationToken.None)
        );

        Assert.Contains("already shared", ex.Message);
    }

    [Fact]
    public async Task GrantShare_NormalizesEmail()
    {
        var mixedCaseEmail = "Recipient@EXAMPLE.COM";
        var shareId = await shareService.GrantShareAsync(TemplateId, ownerUserId, mixedCaseEmail, CancellationToken.None);

        var share = await dbContext.TemplateShares.FindAsync(shareId);
        Assert.Equal(RecipientEmail.ToLowerInvariant(), share.SharedWithEmail);
    }

    [Fact]
    public async Task RevokeShare_MarksAsRevoked()
    {
        var shareId = await shareService.GrantShareAsync(TemplateId, ownerUserId, RecipientEmail, CancellationToken.None);

        await shareService.RevokeShareAsync(shareId, CancellationToken.None);

        var share = await dbContext.TemplateShares.FindAsync(shareId);
        Assert.NotNull(share.RevokedAtUtc);
    }

    [Fact]
    public async Task RevokeShare_AlreadyRevoked_Throws()
    {
        var shareId = await shareService.GrantShareAsync(TemplateId, ownerUserId, RecipientEmail, CancellationToken.None);
        await shareService.RevokeShareAsync(shareId, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            shareService.RevokeShareAsync(shareId, CancellationToken.None)
        );

        Assert.Contains("revoked", ex.Message);
    }

    [Fact]
    public async Task GetSharedWithMe_ReturnsSharedTemplates()
    {
        await shareService.GrantShareAsync(TemplateId, ownerUserId, RecipientEmail, CancellationToken.None);

        // Add metadata for the template
        var metadata = new TemplateMetadataEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = TemplateId,
            EditionId = "v1",
            OwnerUserId = ownerUserId,
            Author = "Owner",
            License = "MIT",
            PublishedAtUtc = DateTime.UtcNow,
            TemplateStatus = "Published",
            ModerationStatus = "Approved",
            UpdatedAtUtc = DateTime.UtcNow
        };
        dbContext.TemplateMetadata.Add(metadata);
        await dbContext.SaveChangesAsync();

        var templates = await shareService.GetSharedWithMeAsync(RecipientEmail, CancellationToken.None);

        Assert.NotEmpty(templates);
        Assert.Single(templates);
        Assert.Equal(TemplateId, templates[0].TemplateId);
    }

    [Fact]
    public async Task HasAccess_OwnerHasAccess()
    {
        var metadata = new TemplateMetadataEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = TemplateId,
            EditionId = "v1",
            OwnerUserId = ownerUserId,
            Author = "Owner",
            License = "MIT",
            PublishedAtUtc = DateTime.UtcNow,
            TemplateStatus = "Published",
            ModerationStatus = "Approved",
            UpdatedAtUtc = DateTime.UtcNow
        };
        dbContext.TemplateMetadata.Add(metadata);
        await dbContext.SaveChangesAsync();

        var hasAccess = await shareService.HasAccessAsync(OwnerEmail, TemplateId, CancellationToken.None);

        Assert.True(hasAccess);
    }

    [Fact]
    public async Task HasAccess_SharedUserHasAccess()
    {
        var metadata = new TemplateMetadataEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = TemplateId,
            EditionId = "v1",
            OwnerUserId = ownerUserId,
            Author = "Owner",
            License = "MIT",
            PublishedAtUtc = DateTime.UtcNow,
            TemplateStatus = "Published",
            ModerationStatus = "Approved",
            UpdatedAtUtc = DateTime.UtcNow
        };
        dbContext.TemplateMetadata.Add(metadata);
        await dbContext.SaveChangesAsync();

        await shareService.GrantShareAsync(TemplateId, ownerUserId, RecipientEmail, CancellationToken.None);

        var hasAccess = await shareService.HasAccessAsync(RecipientEmail, TemplateId, CancellationToken.None);

        Assert.True(hasAccess);
    }

    [Fact]
    public async Task HasAccess_RevokedShareNoAccess()
    {
        var metadata = new TemplateMetadataEntity
        {
            Id = Guid.NewGuid(),
            TemplateId = TemplateId,
            EditionId = "v1",
            OwnerUserId = ownerUserId,
            Author = "Owner",
            License = "MIT",
            PublishedAtUtc = DateTime.UtcNow,
            TemplateStatus = "Published",
            ModerationStatus = "Approved",
            UpdatedAtUtc = DateTime.UtcNow
        };
        dbContext.TemplateMetadata.Add(metadata);
        await dbContext.SaveChangesAsync();

        var shareId = await shareService.GrantShareAsync(TemplateId, ownerUserId, RecipientEmail, CancellationToken.None);
        await shareService.RevokeShareAsync(shareId, CancellationToken.None);

        var hasAccess = await shareService.HasAccessAsync(RecipientEmail, TemplateId, CancellationToken.None);

        Assert.False(hasAccess);
    }
}
