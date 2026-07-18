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
        Assert.Equal(2, await dbContext.Accounts.CountAsync());
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

        [Fact]
        public async Task GetAuthorizedLedgerPageReturnsNewestFirstWithPaginationCursor()
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

            var transferA = await sessionService.TransferBetweenParticipantsAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new TransferBetweenParticipantsRequest(
                    created.HostParticipantId,
                    joined.ParticipantId,
                    50,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "ledger-a",
                    Note: "A"
                ),
                CancellationToken.None
            );
            var transferB = await sessionService.TransferBetweenParticipantsAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new TransferBetweenParticipantsRequest(
                    joined.ParticipantId,
                    created.HostParticipantId,
                    10,
                    ExpectedSessionVersion: 3,
                    IdempotencyKey: "ledger-b",
                    Note: "B"
                ),
                CancellationToken.None
            );
            var correction = await sessionService.CorrectTransactionAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new CorrectTransactionRequest(
                    transferA.Transaction.TransactionId,
                    ExpectedSessionVersion: 4,
                    IdempotencyKey: "ledger-c",
                    Reason: "Fix"
                ),
                CancellationToken.None
            );

            var firstPage = await sessionService.GetAuthorizedLedgerPageAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                beforeSequence: null,
                take: 2,
                CancellationToken.None
            );
            Assert.Equal(2, firstPage.Transactions.Count);
            Assert.Equal(correction.Transaction.TransactionId, firstPage.Transactions[0].TransactionId);
            Assert.Equal(transferB.Transaction.TransactionId, firstPage.Transactions[1].TransactionId);
            Assert.NotNull(firstPage.NextBeforeSequence);

            var secondPage = await sessionService.GetAuthorizedLedgerPageAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                beforeSequence: firstPage.NextBeforeSequence,
                take: 2,
                CancellationToken.None
            );
            Assert.Single(secondPage.Transactions);
            Assert.Equal(transferA.Transaction.TransactionId, secondPage.Transactions[0].TransactionId);
            Assert.Null(secondPage.NextBeforeSequence);
        }

        [Fact]
        public async Task GetAuthorizedLedgerPageRejectsUnauthorizedParticipant()
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

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sessionService.GetAuthorizedLedgerPageAsync(
                    created.SessionId,
                    Guid.NewGuid(),
                    created.ReconnectCredential,
                    beforeSequence: null,
                    take: 50,
                    CancellationToken.None
                )
            );

            Assert.Equal("unauthorized-command", exception.Message);
        }

        [Fact]
        public async Task GetAuthorizedSessionExportReturnsSnapshotAndFullLedgerHistory()
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
                    100,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "export-a",
                    Note: "First"
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
                    IdempotencyKey: "export-b",
                    Reason: "Fix"
                ),
                CancellationToken.None
            );

            var export = await sessionService.GetAuthorizedSessionExportAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                CancellationToken.None
            );

            Assert.Equal(created.SessionId, export.Snapshot.SessionId);
            Assert.Equal(2, export.Transactions.Count);
            Assert.Equal(transfer.Transaction.TransactionId, export.Transactions[0].TransactionId);
            Assert.Equal(correction.Transaction.TransactionId, export.Transactions[1].TransactionId);
        }

        [Fact]
        public async Task GetAuthorizedSessionExportRejectsUnauthorizedParticipant()
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

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sessionService.GetAuthorizedSessionExportAsync(
                    created.SessionId,
                    Guid.NewGuid(),
                    created.ReconnectCredential,
                    CancellationToken.None
                )
            );

            Assert.Equal("unauthorized-command", exception.Message);
        }

        [Fact]
        public async Task BankPaymentAndCollectionMutatePlayerAndBankBalances()
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

            var payment = await sessionService.BankToParticipantAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new BankToParticipantRequest(
                    joined.ParticipantId,
                    200,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "bank-pay-1",
                    Note: "Salary"
                ),
                CancellationToken.None
            );
            var playerAfterPayment = payment.Snapshot.Accounts.Single(account =>
                account.OwnerId == joined.ParticipantId
            );
            var bankAfterPayment = payment.Snapshot.Accounts.Single(account =>
                account.OwnerType == "bank"
            );
            Assert.Equal(1700, playerAfterPayment.Balance);
            Assert.Equal(-200, bankAfterPayment.Balance);

            var collection = await sessionService.ParticipantToBankAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                new ParticipantToBankRequest(
                    joined.ParticipantId,
                    300,
                    ExpectedSessionVersion: 3,
                    IdempotencyKey: "bank-col-1",
                    Note: "Fee"
                ),
                CancellationToken.None
            );
            var playerAfterCollection = collection.Snapshot.Accounts.Single(account =>
                account.OwnerId == joined.ParticipantId
            );
            var bankAfterCollection = collection.Snapshot.Accounts.Single(account =>
                account.OwnerType == "bank"
            );
            Assert.Equal(1400, playerAfterCollection.Balance);
            Assert.Equal(100, bankAfterCollection.Balance);
        }

        [Fact]
        public async Task ExecuteTemplateActionAppliesFinancialActionsFromTemplate()
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

            var income = await sessionService.ExecuteTemplateActionAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                "standard-income",
                new ExecuteTemplateActionRequest(
                    joined.ParticipantId,
                    null,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "action-1",
                    Note: string.Empty
                ),
                CancellationToken.None
            );
            var playerAfterIncome = income.Snapshot.Accounts.Single(account =>
                account.OwnerId == joined.ParticipantId
            );
            var bankAfterIncome = income.Snapshot.Accounts.Single(account => account.OwnerType == "bank");
            Assert.Equal(1700, playerAfterIncome.Balance);
            Assert.Equal(-200, bankAfterIncome.Balance);
            Assert.Equal("Action: Standard income", income.Transaction.Note);

            var fee = await sessionService.ExecuteTemplateActionAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                "standard-fee",
                new ExecuteTemplateActionRequest(
                    joined.ParticipantId,
                    null,
                    ExpectedSessionVersion: 3,
                    IdempotencyKey: "action-2",
                    Note: string.Empty
                ),
                CancellationToken.None
            );
            var playerAfterFee = fee.Snapshot.Accounts.Single(account =>
                account.OwnerId == joined.ParticipantId
            );
            var bankAfterFee = fee.Snapshot.Accounts.Single(account => account.OwnerType == "bank");
            Assert.Equal(1650, playerAfterFee.Balance);
            Assert.Equal(-150, bankAfterFee.Balance);
            Assert.Equal("Action: Standard fee", fee.Transaction.Note);
        }

        [Fact]
        public async Task ExecuteTemplateActionRejectsUnsupportedOperationTypes()
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
                    "generic-life-journey",
                    "family-edition",
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

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sessionService.ExecuteTemplateActionAsync(
                    created.SessionId,
                    created.HostParticipantId,
                    created.ReconnectCredential,
                    "buy-home",
                    new ExecuteTemplateActionRequest(
                        joined.ParticipantId,
                        null,
                        ExpectedSessionVersion: 2,
                        IdempotencyKey: "unsupported-action",
                        Note: string.Empty
                    ),
                    CancellationToken.None
                )
            );

            Assert.Equal("unsupported-template-action", exception.Message);
        }

        [Fact]
        public async Task SnapshotIncludesDefaultPlayerFieldValuesForParticipants()
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
                    "generic-life-journey",
                    "family-edition",
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

            var snapshot = await sessionService.GetAuthorizedSnapshotAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                CancellationToken.None
            );

            var hostChildrenField = snapshot.PlayerFieldValues.Single(value =>
                value.ParticipantId == created.HostParticipantId && value.FieldId == "children-count"
            );
            var joinedChildrenField = snapshot.PlayerFieldValues.Single(value =>
                value.ParticipantId == joined.ParticipantId && value.FieldId == "children-count"
            );
            Assert.Equal("0", hostChildrenField.ValueJson);
            Assert.Equal("0", joinedChildrenField.ValueJson);
            Assert.Equal(8, snapshot.PlayerFieldValues.Count);
        }

        [Fact]
        public async Task ExecuteTemplateActionAppliesIncrementFieldOperation()
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
                    "generic-life-journey",
                    "family-edition",
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

            var response = await sessionService.ExecuteTemplateActionAsync(
                created.SessionId,
                created.HostParticipantId,
                created.ReconnectCredential,
                "new-child",
                new ExecuteTemplateActionRequest(
                    joined.ParticipantId,
                    null,
                    ExpectedSessionVersion: 2,
                    IdempotencyKey: "field-action-1",
                    Note: string.Empty
                ),
                CancellationToken.None
            );

            var joinedChildrenField = response.Snapshot.PlayerFieldValues.Single(value =>
                value.ParticipantId == joined.ParticipantId && value.FieldId == "children-count"
            );
            Assert.Equal("1", joinedChildrenField.ValueJson);
            Assert.Equal("action", response.Transaction.Kind);
            Assert.Empty(response.Transaction.Postings);
        }

        [Fact]
        public async Task ExecuteTemplateActionSupportsAllPlayersScopeForBankPayments()
        {
            var tempTemplatesRoot = CreateTempTemplatesRoot();
            var templatePath = Path.Combine(
                tempTemplatesRoot,
                "samples",
                "generic-property-trading",
                "template.json"
            );

            try
            {
                var json = await File.ReadAllTextAsync(templatePath);
                var updatedJson = json.Replace(
                    "\"scope\": \"single-player\"",
                    "\"scope\": \"all-players\"",
                    StringComparison.Ordinal
                );
                await File.WriteAllTextAsync(templatePath, updatedJson);

                var isolatedCatalog = new FileTemplateCatalogService(tempTemplatesRoot);
                await using var connection = new SqliteConnection("Data Source=:memory:");
                await connection.OpenAsync();
                var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                    .UseSqlite(connection)
                    .Options;
                await using var dbContext = new BankersSeatDbContext(dbOptions);
                await dbContext.Database.MigrateAsync();
                var sessionService = new SqliteSessionService(dbContext, isolatedCatalog);
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

                var response = await sessionService.ExecuteTemplateActionAsync(
                    created.SessionId,
                    created.HostParticipantId,
                    created.ReconnectCredential,
                    "standard-income",
                    new ExecuteTemplateActionRequest(
                        null,
                        null,
                        ExpectedSessionVersion: 2,
                        IdempotencyKey: "all-players-1",
                        Note: string.Empty
                    ),
                    CancellationToken.None
                );

                var host = response.Snapshot.Accounts.Single(account =>
                    account.OwnerId == created.HostParticipantId
                );
                var player = response.Snapshot.Accounts.Single(account =>
                    account.OwnerId == joined.ParticipantId
                );
                var bank = response.Snapshot.Accounts.Single(account => account.OwnerType == "bank");
                Assert.Equal(1700, host.Balance);
                Assert.Equal(1700, player.Balance);
                Assert.Equal(-400, bank.Balance);
                Assert.Equal(4, response.Transaction.Postings.Count);
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
        public async Task ExecuteTemplateActionSupportsTwoPlayersScopeForPlayerTransfers()
        {
            var tempTemplatesRoot = CreateTempTemplatesRoot();
            var templatePath = Path.Combine(
                tempTemplatesRoot,
                "samples",
                "generic-property-trading",
                "template.json"
            );

            try
            {
                var json = await File.ReadAllTextAsync(templatePath);
                var updatedJson = json.Replace(
                    "\"scope\": \"single-player\",\n      \"operation\": {\n        \"type\": \"player-to-bank\"",
                    "\"scope\": \"two-players\",\n      \"operation\": {\n        \"type\": \"player-to-player\"",
                    StringComparison.Ordinal
                );
                await File.WriteAllTextAsync(templatePath, updatedJson);

                var isolatedCatalog = new FileTemplateCatalogService(tempTemplatesRoot);
                await using var connection = new SqliteConnection("Data Source=:memory:");
                await connection.OpenAsync();
                var dbOptions = new DbContextOptionsBuilder<BankersSeatDbContext>()
                    .UseSqlite(connection)
                    .Options;
                await using var dbContext = new BankersSeatDbContext(dbOptions);
                await dbContext.Database.MigrateAsync();
                var sessionService = new SqliteSessionService(dbContext, isolatedCatalog);
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

                var response = await sessionService.ExecuteTemplateActionAsync(
                    created.SessionId,
                    created.HostParticipantId,
                    created.ReconnectCredential,
                    "standard-fee",
                    new ExecuteTemplateActionRequest(
                        created.HostParticipantId,
                        joined.ParticipantId,
                        ExpectedSessionVersion: 2,
                        IdempotencyKey: "two-players-1",
                        Note: string.Empty
                    ),
                    CancellationToken.None
                );

                var host = response.Snapshot.Accounts.Single(account =>
                    account.OwnerId == created.HostParticipantId
                );
                var player = response.Snapshot.Accounts.Single(account =>
                    account.OwnerId == joined.ParticipantId
                );
                var bank = response.Snapshot.Accounts.Single(account => account.OwnerType == "bank");
                Assert.Equal(1450, host.Balance);
                Assert.Equal(1550, player.Balance);
                Assert.Equal(0, bank.Balance);
                Assert.Equal(2, response.Transaction.Postings.Count);
            }
            finally
            {
                if (Directory.Exists(tempTemplatesRoot))
                {
                    Directory.Delete(tempTemplatesRoot, recursive: true);
                }
            }
        }

        private static string CreateTempTemplatesRoot()
        {
            var tempTemplatesRoot = Path.Combine(
                Path.GetTempPath(),
                $"bankers-seat-templates-{Guid.NewGuid():N}"
            );
            var sourceTemplateDir = Path.Combine(TemplatesRoot, "samples", "generic-property-trading");
            var targetTemplateDir = Path.Combine(tempTemplatesRoot, "samples", "generic-property-trading");
            Directory.CreateDirectory(targetTemplateDir);
            File.Copy(
                Path.Combine(sourceTemplateDir, "template.json"),
                Path.Combine(targetTemplateDir, "template.json"),
                overwrite: true
            );
            return tempTemplatesRoot;
        }
}
