using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Sessions;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BankersSeat.Server.Tests.Integration;

public sealed class SessionScaffoldTests
{
    private static readonly string TemplatesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "templates")
    );
    private readonly FileTemplateCatalogService catalogService = new(TemplatesRoot);

    [Fact]
    public async Task TemplateCatalogContainsSampleTemplates()
    {
        var catalog = await catalogService.GetCatalogAsync(CancellationToken.None);

        Assert.Contains(catalog, template => template.Identity.TemplateId == "generic-property-trading");
        Assert.Contains(catalog, template => template.Identity.TemplateId == "generic-life-journey");
    }

    [Fact]
    public async Task CreateSessionPersistsTemplateSnapshotInSessionView()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new BankersSeatDbContext(dbOptions);
        await dbContext.Database.MigrateAsync();
        var sessionService = new SqliteSessionService(dbContext, catalogService);
        var request = new CreateSessionRequest(
            "generic-property-trading",
            "standard-edition",
            "1.0.0",
            "Rob",
            new Dictionary<string, System.Text.Json.JsonElement>()
        );

        var created = await sessionService.CreateSessionAsync(request, CancellationToken.None);
        var snapshot = await sessionService.GetAuthorizedSnapshotAsync(
            created.SessionId,
            created.HostParticipantId,
            created.ReconnectCredential,
            CancellationToken.None
        );

        Assert.Equal("generic-property-trading", snapshot.Template.TemplateId);
        Assert.Equal("standard-edition", snapshot.Template.EditionId);
        Assert.Equal("1.0.0", snapshot.Template.TemplateVersion);
        Assert.Equal(1, snapshot.SessionVersion);
        Assert.NotEmpty(snapshot.Template.ContentHash);
        Assert.Single(await dbContext.GameSessions.ToListAsync());
        Assert.Single(await dbContext.TemplateSnapshots.ToListAsync());
        Assert.Single(await dbContext.Participants.ToListAsync());
        Assert.Single(await dbContext.Accounts.ToListAsync());
    }

    [Fact]
    public async Task ExistingSessionSnapshotRemainsStableAfterTemplateFileChanges()
    {
        var tempTemplatesRoot = Path.Combine(
            Path.GetTempPath(),
            $"bankers-seat-templates-{Guid.NewGuid():N}"
        );
        var sourceTemplateDir = Path.Combine(TemplatesRoot, "samples", "generic-property-trading");
        var targetTemplateDir = Path.Combine(tempTemplatesRoot, "samples", "generic-property-trading");
        Directory.CreateDirectory(targetTemplateDir);

        var sourceTemplatePath = Path.Combine(sourceTemplateDir, "template.json");
        var targetTemplatePath = Path.Combine(targetTemplateDir, "template.json");
        File.Copy(sourceTemplatePath, targetTemplatePath, overwrite: true);

        try
        {
            var isolatedCatalog = new FileTemplateCatalogService(tempTemplatesRoot);
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var dbContext = new BankersSeatDbContext(dbOptions);
            await dbContext.Database.MigrateAsync();
            var sessionService = new SqliteSessionService(dbContext, isolatedCatalog);
            var request = new CreateSessionRequest(
                "generic-property-trading",
                "standard-edition",
                "1.0.0",
                "Host",
                new Dictionary<string, System.Text.Json.JsonElement>()
            );

            var firstSession = await sessionService.CreateSessionAsync(request, CancellationToken.None);
            var firstSnapshot = await sessionService.GetAuthorizedSnapshotAsync(
                firstSession.SessionId,
                firstSession.HostParticipantId,
                firstSession.ReconnectCredential,
                CancellationToken.None
            );
            var firstHostAccount = firstSnapshot.Accounts.Single(account =>
                account.OwnerId == firstSession.HostParticipantId
            );

            var originalTemplateJson = await File.ReadAllTextAsync(targetTemplatePath);
            var updatedTemplateJson = originalTemplateJson.Replace(
                "\"startingPlayerBalance\": 1500",
                "\"startingPlayerBalance\": 2500",
                StringComparison.Ordinal
            );
            Assert.NotEqual(originalTemplateJson, updatedTemplateJson);
            await File.WriteAllTextAsync(targetTemplatePath, updatedTemplateJson);

            var firstSnapshotAfterChange = await sessionService.GetAuthorizedSnapshotAsync(
                firstSession.SessionId,
                firstSession.HostParticipantId,
                firstSession.ReconnectCredential,
                CancellationToken.None
            );
            var firstHostAccountAfterChange = firstSnapshotAfterChange.Accounts.Single(account =>
                account.OwnerId == firstSession.HostParticipantId
            );
            Assert.Equal(firstSnapshot.Template.SnapshotId, firstSnapshotAfterChange.Template.SnapshotId);
            Assert.Equal(firstSnapshot.Template.ContentHash, firstSnapshotAfterChange.Template.ContentHash);
            Assert.Equal(firstHostAccount.Balance, firstHostAccountAfterChange.Balance);
            Assert.Equal(1500, firstHostAccountAfterChange.Balance);

            var secondSession = await sessionService.CreateSessionAsync(request, CancellationToken.None);
            var secondHostAccount = secondSession.InitialSnapshot.Accounts.Single(account =>
                account.OwnerId == secondSession.HostParticipantId
            );

            Assert.NotEqual(
                firstSnapshot.Template.ContentHash,
                secondSession.InitialSnapshot.Template.ContentHash
            );
            Assert.Equal(2500, secondHostAccount.Balance);
        }
        finally
        {
            if (Directory.Exists(tempTemplatesRoot))
            {
                Directory.Delete(tempTemplatesRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TransferBetweenParticipantsPersistsLedgerAndUpdatesBalances()
    {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var dbContext = new BankersSeatDbContext(dbOptions);
            await dbContext.Database.MigrateAsync();
            var sessionService = new SqliteSessionService(dbContext, catalogService);
            var created = await sessionService.CreateSessionAsync(
                new CreateSessionRequest(
                    "generic-property-trading",
                    "standard-edition",
                    "1.0.0",
                    "Host",
                    new Dictionary<string, System.Text.Json.JsonElement>()
                ),
                CancellationToken.None
            );
            var joined = await sessionService.JoinSessionAsync(
                new JoinSessionRequest(created.RoomCode, "Player2", "blue"),
                CancellationToken.None
            );

            var response = await sessionService.TransferBetweenParticipantsAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new TransferBetweenParticipantsRequest(
                    created.HostParticipantId,
                    joined.ParticipantId,
                    200,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "t-1",
                    Note: "Rent"
                ),
                CancellationToken.None
            );

            var hostBalance = response.Snapshot.Accounts.Single(account =>
                account.OwnerId == created.HostParticipantId
            );
            var playerBalance = response.Snapshot.Accounts.Single(account =>
                account.OwnerId == joined.ParticipantId
            );
            Assert.Equal(1300, hostBalance.Balance);
            Assert.Equal(1700, playerBalance.Balance);
            Assert.Equal(3, response.Snapshot.SessionVersion);
            Assert.Equal("transfer", response.Transaction.Kind);
            Assert.Equal(0, response.Transaction.Postings.Sum(posting => posting.Amount));
            Assert.False(response.IdempotentReplay);
            Assert.Single(await dbContext.LedgerTransactions.ToListAsync());
            Assert.Equal(2, await dbContext.LedgerPostings.CountAsync());
            Assert.Single(await dbContext.IdempotencyRecords.ToListAsync());
        }

        [Fact]
        public async Task TransferRejectsStaleVersionAndInsufficientFunds()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var dbContext = new BankersSeatDbContext(dbOptions);
            await dbContext.Database.MigrateAsync();
            var sessionService = new SqliteSessionService(dbContext, catalogService);
            var created = await sessionService.CreateSessionAsync(
                new CreateSessionRequest(
                    "generic-property-trading",
                    "standard-edition",
                    "1.0.0",
                    "Host",
                    new Dictionary<string, System.Text.Json.JsonElement>()
                ),
                CancellationToken.None
            );
            var joined = await sessionService.JoinSessionAsync(
                new JoinSessionRequest(created.RoomCode, "Player2", "blue"),
                CancellationToken.None
            );

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sessionService.TransferBetweenParticipantsAsync(
                    created.SessionId,
                    created.HostParticipantId,
                    created.ReconnectCredential,
                    new TransferBetweenParticipantsRequest(
                        created.HostParticipantId,
                        joined.ParticipantId,
                        10,
                        ExpectedSessionVersion: 1,
                        IdempotencyKey: "stale",
                        Note: "Stale"
                    ),
                    CancellationToken.None
                )
            );

            var insufficient = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sessionService.TransferBetweenParticipantsAsync(
                    created.SessionId,
                    created.HostParticipantId,
                    created.ReconnectCredential,
                    new TransferBetweenParticipantsRequest(
                        created.HostParticipantId,
                        joined.ParticipantId,
                        100_000,
                        ExpectedSessionVersion: 2,
                        IdempotencyKey: "insufficient",
                        Note: "Too much"
                    ),
                    CancellationToken.None
                )
            );
            Assert.Equal("insufficient-funds", insufficient.Message);
        }

        [Fact]
        public async Task TransferReplayWithSameIdempotencyKeyReturnsStoredResult()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var dbContext = new BankersSeatDbContext(dbOptions);
            await dbContext.Database.MigrateAsync();
            var sessionService = new SqliteSessionService(dbContext, catalogService);
            var created = await sessionService.CreateSessionAsync(
                new CreateSessionRequest(
                    "generic-property-trading",
                    "standard-edition",
                    "1.0.0",
                    "Host",
                    new Dictionary<string, System.Text.Json.JsonElement>()
                ),
                CancellationToken.None
            );
            var joined = await sessionService.JoinSessionAsync(
                new JoinSessionRequest(created.RoomCode, "Player2", "blue"),
                CancellationToken.None
            );

            var first = await sessionService.TransferBetweenParticipantsAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new TransferBetweenParticipantsRequest(
                    created.HostParticipantId,
                    joined.ParticipantId,
                    100,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "same-key",
                    Note: "First"
                ),
                CancellationToken.None
            );
            var replay = await sessionService.TransferBetweenParticipantsAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new TransferBetweenParticipantsRequest(
                    created.HostParticipantId,
                    joined.ParticipantId,
                    100,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "same-key",
                    Note: "First"
                ),
                CancellationToken.None
            );

            Assert.True(replay.IdempotentReplay);
            Assert.Equal(first.Transaction.TransactionId, replay.Transaction.TransactionId);
            Assert.Single(await dbContext.LedgerTransactions.ToListAsync());
        }

        [Fact]
        public async Task TransferRejectsUnauthorizedActor()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var dbContext = new BankersSeatDbContext(dbOptions);
            await dbContext.Database.MigrateAsync();
            var sessionService = new SqliteSessionService(dbContext, catalogService);
            var created = await sessionService.CreateSessionAsync(
                new CreateSessionRequest(
                    "generic-property-trading",
                    "standard-edition",
                    "1.0.0",
                    "Host",
                    new Dictionary<string, System.Text.Json.JsonElement>()
                ),
                CancellationToken.None
            );
            var joined = await sessionService.JoinSessionAsync(
                new JoinSessionRequest(created.RoomCode, "Player2", "blue"),
                CancellationToken.None
            );

            var unauthorized = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sessionService.TransferBetweenParticipantsAsync(
                    created.SessionId,
                    joined.ParticipantId,
                    joined.ReconnectCredential,
                    new TransferBetweenParticipantsRequest(
                        joined.ParticipantId,
                        created.HostParticipantId,
                        100,
                        ExpectedSessionVersion: 2,
                        IdempotencyKey: "unauth",
                        Note: "Not host"
                    ),
                    CancellationToken.None
                )
            );
            Assert.Equal("unauthorized-command", unauthorized.Message);
        }

        [Fact]
        public async Task TransferRejectsIdempotencyKeyReuseWithDifferentPayload()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var dbContext = new BankersSeatDbContext(dbOptions);
            await dbContext.Database.MigrateAsync();
            var sessionService = new SqliteSessionService(dbContext, catalogService);
            var created = await sessionService.CreateSessionAsync(
                new CreateSessionRequest(
                    "generic-property-trading",
                    "standard-edition",
                    "1.0.0",
                    "Host",
                    new Dictionary<string, System.Text.Json.JsonElement>()
                ),
                CancellationToken.None
            );
            var joined = await sessionService.JoinSessionAsync(
                new JoinSessionRequest(created.RoomCode, "Player2", "blue"),
                CancellationToken.None
            );

            _ = await sessionService.TransferBetweenParticipantsAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new TransferBetweenParticipantsRequest(
                    created.HostParticipantId,
                    joined.ParticipantId,
                    100,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "same-key",
                    Note: "A"
                ),
                CancellationToken.None
            );

            var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sessionService.TransferBetweenParticipantsAsync(
                    created.SessionId,
                    created.HostParticipantId,
                    created.ReconnectCredential,
                    new TransferBetweenParticipantsRequest(
                        created.HostParticipantId,
                        joined.ParticipantId,
                        150,
                        ExpectedSessionVersion: 2,
                        IdempotencyKey: "same-key",
                        Note: "B"
                    ),
                    CancellationToken.None
                )
            );
            Assert.Equal("duplicate-idempotency-key", duplicate.Message);
        }

        [Fact]
        public async Task CorrectionCreatesCompensatingTransactionAndPreventsDuplicateCorrection()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var dbContext = new BankersSeatDbContext(dbOptions);
            await dbContext.Database.MigrateAsync();
            var sessionService = new SqliteSessionService(dbContext, catalogService);
            var created = await sessionService.CreateSessionAsync(
                new CreateSessionRequest(
                    "generic-property-trading",
                    "standard-edition",
                    "1.0.0",
                    "Host",
                    new Dictionary<string, System.Text.Json.JsonElement>()
                ),
                CancellationToken.None
            );
            var joined = await sessionService.JoinSessionAsync(
                new JoinSessionRequest(created.RoomCode, "Player2", "blue"),
                CancellationToken.None
            );
            var transfer = await sessionService.TransferBetweenParticipantsAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new TransferBetweenParticipantsRequest(
                    created.HostParticipantId,
                    joined.ParticipantId,
                    150,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "xfer",
                    Note: "Mistake"
                ),
                CancellationToken.None
            );

            var correction = await sessionService.CorrectTransactionAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new CorrectTransactionRequest(
                    transfer.Transaction.TransactionId,
                    ExpectedSessionVersion: 3,
                    IdempotencyKey: "corr",
                    Reason: "Wrong player"
                ),
                CancellationToken.None
            );
            Assert.Equal("correction", correction.Transaction.Kind);
            Assert.Equal(transfer.Transaction.TransactionId, correction.Transaction.CorrectsTransactionId);
            var hostBalance = correction.Snapshot.Accounts.Single(account =>
                account.OwnerId == created.HostParticipantId
            );
            var playerBalance = correction.Snapshot.Accounts.Single(account =>
                account.OwnerId == joined.ParticipantId
            );
            Assert.Equal(1500, hostBalance.Balance);
            Assert.Equal(1500, playerBalance.Balance);

            var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sessionService.CorrectTransactionAsync(
                    created.SessionId,
                    created.HostParticipantId,
                    created.ReconnectCredential,
                    new CorrectTransactionRequest(
                        transfer.Transaction.TransactionId,
                        ExpectedSessionVersion: 4,
                        IdempotencyKey: "corr-2",
                        Reason: "Again"
                    ),
                    CancellationToken.None
                )
            );
            Assert.Equal("duplicate-correction", duplicate.Message);
        }
}
