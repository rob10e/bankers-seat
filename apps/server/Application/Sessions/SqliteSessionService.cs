using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Domain.Ledger;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankersSeat.Server.Application.Sessions;

public sealed class SqliteSessionService : ISessionService
{
    private readonly BankersSeatDbContext dbContext;
    private readonly ITemplateCatalogService templateCatalogService;

    public SqliteSessionService(
        BankersSeatDbContext dbContext,
        ITemplateCatalogService templateCatalogService
    )
    {
        this.dbContext = dbContext;
        this.templateCatalogService = templateCatalogService;
    }

    public async Task<CreateSessionResponse> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateCreateRequest(request);

        var templateSnapshot = await templateCatalogService.GetTemplateSnapshotAsync(
            request.TemplateId,
            request.EditionId,
            request.TemplateVersion,
            cancellationToken
        );

        if (templateSnapshot is null)
        {
            throw new InvalidOperationException("template-not-found");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var nowUtc = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var hostParticipantId = Guid.NewGuid();
        var reconnectCredential = CreateReconnectCredential();
        var reconnectHash = HashSecret(reconnectCredential);

        var roomCode = await GenerateUniqueRoomCodeAsync(cancellationToken);
        var snapshotEntity = new TemplateSnapshotEntity
        {
            Id = templateSnapshot.Id,
            TemplateId = templateSnapshot.Identity.TemplateId,
            EditionId = templateSnapshot.Identity.EditionId,
            TemplateVersion = templateSnapshot.Identity.TemplateVersion,
            SchemaVersion = templateSnapshot.SchemaVersion,
            ContentHash = templateSnapshot.ContentHash,
            TemplateJson = templateSnapshot.TemplateJson,
            StartingPlayerBalance = templateSnapshot.StartingPlayerBalance,
            AllowPlayerOverdraft = templateSnapshot.AllowPlayerOverdraft,
            CreatedAtUtc = nowUtc
        };
        var sessionEntity = new GameSessionEntity
        {
            Id = sessionId,
            RoomCode = roomCode,
            Status = "lobby",
            HostParticipantId = hostParticipantId,
            TemplateSnapshotId = snapshotEntity.Id,
            SessionVersion = 1,
            CreatedAtUtc = nowUtc
        };
        var participantEntity = new ParticipantEntity
        {
            Id = hostParticipantId,
            SessionId = sessionId,
            DisplayName = request.HostDisplayName.Trim(),
            Role = "host",
            IdentityKey = "host",
            JoinOrder = 1,
            CreatedAtUtc = nowUtc,
            ReconnectSecretHash = reconnectHash
        };
        var accountEntity = new AccountEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            OwnerType = "participant",
            OwnerId = hostParticipantId,
            Balance = templateSnapshot.StartingPlayerBalance,
            Version = 1
        };

        dbContext.TemplateSnapshots.Add(snapshotEntity);
        dbContext.GameSessions.Add(sessionEntity);
        dbContext.Participants.Add(participantEntity);
        dbContext.Accounts.Add(accountEntity);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = await BuildSnapshotAsync(sessionEntity.Id, cancellationToken);
        return new CreateSessionResponse(
            sessionEntity.Id,
            sessionEntity.RoomCode,
            hostParticipantId,
            reconnectCredential,
            snapshot,
            BuildConnectionInfo()
        );
    }

    public async Task<JoinSessionResponse> JoinSessionAsync(
        JoinSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateJoinRequest(request);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var normalizedRoomCode = request.RoomCode.Trim().ToUpperInvariant();
        var sessionEntity = await dbContext.GameSessions
            .SingleOrDefaultAsync(session => session.RoomCode == normalizedRoomCode, cancellationToken);
        if (sessionEntity is null)
        {
            throw new InvalidOperationException("session-not-found");
        }

        var templateSnapshot = await dbContext.TemplateSnapshots
            .SingleAsync(snapshot => snapshot.Id == sessionEntity.TemplateSnapshotId, cancellationToken);
        var joinOrder = await dbContext.Participants
            .Where(participant => participant.SessionId == sessionEntity.Id)
            .CountAsync(cancellationToken);

        var reconnectCredential = CreateReconnectCredential();
        var participant = new ParticipantEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionEntity.Id,
            DisplayName = request.DisplayName.Trim(),
            Role = "player",
            IdentityKey = string.IsNullOrWhiteSpace(request.IdentityKey)
                ? "player"
                : request.IdentityKey.Trim(),
            JoinOrder = joinOrder + 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ReconnectSecretHash = HashSecret(reconnectCredential)
        };
        var account = new AccountEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionEntity.Id,
            OwnerType = "participant",
            OwnerId = participant.Id,
            Balance = templateSnapshot.StartingPlayerBalance,
            Version = 1
        };

        sessionEntity.SessionVersion += 1;
        dbContext.Participants.Add(participant);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = await BuildSnapshotAsync(sessionEntity.Id, cancellationToken);
        return new JoinSessionResponse(
            sessionEntity.Id,
            participant.Id,
            reconnectCredential,
            snapshot,
            BuildConnectionInfo()
        );
    }

    public async Task<ReconnectSessionResponse> ReconnectAsync(
        Guid sessionId,
        ReconnectSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var session = await dbContext.GameSessions.SingleOrDefaultAsync(
            record => record.Id == sessionId,
            cancellationToken
        );
        if (session is null)
        {
            throw new InvalidOperationException("session-not-found");
        }

        var participant = await dbContext.Participants.SingleOrDefaultAsync(
            record => record.Id == request.ParticipantId && record.SessionId == sessionId,
            cancellationToken
        );
        if (participant is null)
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        if (!string.Equals(HashSecret(request.ReconnectCredential), participant.ReconnectSecretHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        var refreshedCredential = CreateReconnectCredential();
        participant.ReconnectSecretHash = HashSecret(refreshedCredential);
        session.SessionVersion += 1;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = await BuildSnapshotAsync(sessionId, cancellationToken);
        return new ReconnectSessionResponse(sessionId, participant.Id, refreshedCredential, snapshot);
    }

    public async Task<SessionSnapshotResponse> GetAuthorizedSnapshotAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeParticipantReadAsync(
            sessionId,
            participantId,
            reconnectCredential,
            cancellationToken
        );

        return await BuildSnapshotAsync(sessionId, cancellationToken);
    }

    public async Task<SessionLedgerResponse> GetAuthorizedLedgerPageAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        long? beforeSequence,
        int take,
        CancellationToken cancellationToken
    )
    {
        if (take is < 1 or > 200)
        {
            throw new InvalidOperationException("invalid-request");
        }

        await AuthorizeParticipantReadAsync(
            sessionId,
            participantId,
            reconnectCredential,
            cancellationToken
        );

        var query = dbContext.LedgerTransactions
            .Where(record => record.SessionId == sessionId)
            .OrderByDescending(record => record.Sequence);
        if (beforeSequence.HasValue)
        {
            query = query.Where(record => record.Sequence < beforeSequence.Value)
                .OrderByDescending(record => record.Sequence);
        }

        var rows = await query.Take(take + 1).ToListAsync(cancellationToken);
        var pageRows = rows.Take(take).ToList();
        var pageTransactionIds = pageRows.Select(record => record.Id).ToHashSet();
        var postings = await dbContext.LedgerPostings
            .Where(record => record.SessionId == sessionId && pageTransactionIds.Contains(record.TransactionId))
            .OrderBy(record => record.TransactionId)
            .ThenBy(record => record.Id)
            .ToListAsync(cancellationToken);
        var postingsByTransaction = postings
            .GroupBy(record => record.TransactionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var transactions = pageRows.Select(record =>
            ToLedgerTransactionResponse(
                record,
                postingsByTransaction.TryGetValue(record.Id, out var grouped) ? grouped : []
            )
        ).ToList();
        long? nextBeforeSequence = rows.Count > take
            ? pageRows.LastOrDefault()?.Sequence
            : null;

        return new SessionLedgerResponse(transactions, nextBeforeSequence);
    }

    public async Task<SessionExportResponse> GetAuthorizedSessionExportAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    )
    {
        await AuthorizeParticipantReadAsync(
            sessionId,
            participantId,
            reconnectCredential,
            cancellationToken
        );

        var snapshot = await BuildSnapshotAsync(sessionId, cancellationToken);
        var transactions = await dbContext.LedgerTransactions
            .Where(record => record.SessionId == sessionId)
            .OrderBy(record => record.Sequence)
            .ToListAsync(cancellationToken);
        var transactionIds = transactions.Select(record => record.Id).ToHashSet();
        var postings = await dbContext.LedgerPostings
            .Where(record => record.SessionId == sessionId && transactionIds.Contains(record.TransactionId))
            .OrderBy(record => record.TransactionId)
            .ThenBy(record => record.Id)
            .ToListAsync(cancellationToken);
        var postingsByTransaction = postings
            .GroupBy(record => record.TransactionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var payload = transactions.Select(record =>
            ToLedgerTransactionResponse(
                record,
                postingsByTransaction.TryGetValue(record.Id, out var grouped) ? grouped : []
            )
        ).ToList();

        return new SessionExportResponse(snapshot, payload, DateTimeOffset.UtcNow);
    }

    public async Task<MoneyCommandResponse> TransferBetweenParticipantsAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        TransferBetweenParticipantsRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateTransferRequest(request);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var (session, _) = await AuthorizeMutationAsync(
            sessionId,
            actorParticipantId,
            reconnectCredential,
            cancellationToken
        );
        var idempotency = await TryLoadIdempotencyRecordAsync(
            sessionId,
            actorParticipantId,
            request.IdempotencyKey,
            "transfer-between-participants",
            request,
            cancellationToken
        );
        if (idempotency is not null)
        {
            var replayResponse = await BuildReplayResponseAsync(sessionId, idempotency, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replayResponse;
        }

        if (session.SessionVersion != request.ExpectedSessionVersion)
        {
            throw new InvalidOperationException("stale-session-version");
        }

        var fromAccount = await FindParticipantAccountAsync(
            sessionId,
            request.FromParticipantId,
            cancellationToken
        );
        var toAccount = await FindParticipantAccountAsync(sessionId, request.ToParticipantId, cancellationToken);
        var template = await dbContext.TemplateSnapshots.SingleAsync(
            record => record.Id == session.TemplateSnapshotId,
            cancellationToken
        );
        var nextSequence = await GetNextSequenceAsync(sessionId, cancellationToken);
        var currentBalances = await dbContext.Accounts
            .Where(record => record.SessionId == sessionId)
            .ToDictionaryAsync(record => record.Id, record => record.Balance, cancellationToken);

        MoneyMutationResult mutation;
        try
        {
            mutation = MoneyMutationEngine.ApplyTransfer(
                currentBalances,
                sessionId,
                actorParticipantId,
                fromAccount.Id,
                toAccount.Id,
                request.Amount,
                template.AllowPlayerOverdraft,
                nextSequence,
                DateTimeOffset.UtcNow,
                string.IsNullOrWhiteSpace(request.Note) ? "Transfer" : request.Note.Trim()
            );
        }
        catch (DomainRuleViolationException exception)
        {
            throw new InvalidOperationException(exception.Code);
        }

        await ApplyBalancesAsync(sessionId, mutation.UpdatedBalances, cancellationToken);
        var transactionEntity = AddLedgerTransactionEntity(mutation.Transaction);
        AddLedgerPostingEntities(sessionId, mutation.Transaction);

        session.SessionVersion += 1;
        var resultToken = transactionEntity.Id.ToString("N");
        dbContext.IdempotencyRecords.Add(
            CreateIdempotencyRecord(
                sessionId,
                actorParticipantId,
                request.IdempotencyKey.Trim(),
                "transfer-between-participants",
                request,
                resultToken
            )
        );

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = await BuildSnapshotAsync(sessionId, cancellationToken);
        return new MoneyCommandResponse(
            snapshot,
            ToLedgerTransactionResponse(mutation.Transaction),
            IdempotentReplay: false
        );
    }

    public async Task<MoneyCommandResponse> CorrectTransactionAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        CorrectTransactionRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateCorrectionRequest(request);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var (session, _) = await AuthorizeMutationAsync(
            sessionId,
            actorParticipantId,
            reconnectCredential,
            cancellationToken
        );
        var idempotency = await TryLoadIdempotencyRecordAsync(
            sessionId,
            actorParticipantId,
            request.IdempotencyKey,
            "correct-transaction",
            request,
            cancellationToken
        );
        if (idempotency is not null)
        {
            var replayResponse = await BuildReplayResponseAsync(sessionId, idempotency, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return replayResponse;
        }

        if (session.SessionVersion != request.ExpectedSessionVersion)
        {
            throw new InvalidOperationException("stale-session-version");
        }

        var original = await dbContext.LedgerTransactions.SingleOrDefaultAsync(
            record => record.SessionId == sessionId && record.Id == request.TransactionId,
            cancellationToken
        );
        if (original is null)
        {
            throw new InvalidOperationException("transaction-not-found");
        }

        var originalPostings = await dbContext.LedgerPostings
            .Where(record => record.TransactionId == original.Id)
            .OrderBy(record => record.Id)
            .ToListAsync(cancellationToken);
        if (originalPostings.Count == 0)
        {
            throw new InvalidOperationException("transaction-not-found");
        }

        var correctedIds = await dbContext.LedgerTransactions
            .Where(record => record.SessionId == sessionId && record.CorrectsTransactionId != null)
            .Select(record => record.CorrectsTransactionId!.Value)
            .ToListAsync(cancellationToken);
        var nextSequence = await GetNextSequenceAsync(sessionId, cancellationToken);
        var currentBalances = await dbContext.Accounts
            .Where(record => record.SessionId == sessionId)
            .ToDictionaryAsync(record => record.Id, record => record.Balance, cancellationToken);
        var originalDomain = new LedgerTransaction(
            original.Id,
            original.SessionId,
            original.Sequence,
            original.ActorParticipantId,
            ParseLedgerKind(original.Kind),
            original.CorrectsTransactionId,
            original.Note,
            original.CreatedAtUtc,
            originalPostings
                .Select(posting => new LedgerPosting(posting.AccountId, posting.Amount, posting.BalanceAfter))
                .ToList()
        );

        MoneyMutationResult mutation;
        try
        {
            mutation = MoneyMutationEngine.ApplyCorrection(
                currentBalances,
                sessionId,
                actorParticipantId,
                originalDomain,
                correctedIds.ToHashSet(),
                nextSequence,
                DateTimeOffset.UtcNow,
                request.Reason.Trim()
            );
        }
        catch (DomainRuleViolationException exception)
        {
            throw new InvalidOperationException(exception.Code);
        }

        await ApplyBalancesAsync(sessionId, mutation.UpdatedBalances, cancellationToken);
        var transactionEntity = AddLedgerTransactionEntity(mutation.Transaction);
        AddLedgerPostingEntities(sessionId, mutation.Transaction);

        session.SessionVersion += 1;
        var resultToken = transactionEntity.Id.ToString("N");
        dbContext.IdempotencyRecords.Add(
            CreateIdempotencyRecord(
                sessionId,
                actorParticipantId,
                request.IdempotencyKey.Trim(),
                "correct-transaction",
                request,
                resultToken
            )
        );

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = await BuildSnapshotAsync(sessionId, cancellationToken);
        return new MoneyCommandResponse(
            snapshot,
            ToLedgerTransactionResponse(mutation.Transaction),
            IdempotentReplay: false
        );
    }

    private static void ValidateCreateRequest(CreateSessionRequest request)
    {
        if (
            string.IsNullOrWhiteSpace(request.TemplateId)
            || string.IsNullOrWhiteSpace(request.EditionId)
            || string.IsNullOrWhiteSpace(request.TemplateVersion)
            || string.IsNullOrWhiteSpace(request.HostDisplayName)
        )
        {
            throw new InvalidOperationException("invalid-request");
        }
    }

    private static void ValidateJoinRequest(JoinSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RoomCode) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new InvalidOperationException("invalid-request");
        }
    }

    private static void ValidateTransferRequest(TransferBetweenParticipantsRequest request)
    {
        if (
            request.FromParticipantId == Guid.Empty
            || request.ToParticipantId == Guid.Empty
            || request.ExpectedSessionVersion <= 0
            || request.Amount <= 0
            || string.IsNullOrWhiteSpace(request.IdempotencyKey)
        )
        {
            throw new InvalidOperationException("invalid-request");
        }
    }

    private static void ValidateCorrectionRequest(CorrectTransactionRequest request)
    {
        if (
            request.TransactionId == Guid.Empty
            || request.ExpectedSessionVersion <= 0
            || string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || string.IsNullOrWhiteSpace(request.Reason)
        )
        {
            throw new InvalidOperationException("invalid-request");
        }
    }

    private async Task<(GameSessionEntity Session, ParticipantEntity Actor)> AuthorizeMutationAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    )
    {
        var session = await dbContext.GameSessions.SingleOrDefaultAsync(
            record => record.Id == sessionId,
            cancellationToken
        );
        if (session is null)
        {
            throw new InvalidOperationException("session-not-found");
        }

        var actor = await dbContext.Participants.SingleOrDefaultAsync(
            record => record.Id == actorParticipantId && record.SessionId == sessionId,
            cancellationToken
        );
        if (actor is null)
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        if (!string.Equals(HashSecret(reconnectCredential), actor.ReconnectSecretHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        if (!string.Equals(actor.Role, "host", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        return (session, actor);
    }

    private async Task AuthorizeParticipantReadAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    )
    {
        var participant = await dbContext.Participants.SingleOrDefaultAsync(
            record => record.Id == participantId && record.SessionId == sessionId,
            cancellationToken
        );
        if (participant is null)
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        if (!string.Equals(HashSecret(reconnectCredential), participant.ReconnectSecretHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unauthorized-command");
        }
    }

    private async Task<IdempotencyRecordEntity?> TryLoadIdempotencyRecordAsync<TRequest>(
        Guid sessionId,
        Guid actorParticipantId,
        string idempotencyKey,
        string commandType,
        TRequest request,
        CancellationToken cancellationToken
    )
    {
        var normalizedKey = idempotencyKey.Trim();
        var existing = await dbContext.IdempotencyRecords.SingleOrDefaultAsync(
            record =>
                record.SessionId == sessionId
                && record.ActorParticipantId == actorParticipantId
                && record.Key == normalizedKey,
            cancellationToken
        );
        if (existing is null)
        {
            return null;
        }

        var requestHash = ComputeRequestHash(request);
        if (
            !string.Equals(existing.CommandType, commandType, StringComparison.Ordinal)
            || !string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("duplicate-idempotency-key");
        }

        return existing;
    }

    private async Task<MoneyCommandResponse> BuildReplayResponseAsync(
        Guid sessionId,
        IdempotencyRecordEntity idempotency,
        CancellationToken cancellationToken
    )
    {
        if (!Guid.TryParseExact(idempotency.ResultHash, "N", out var transactionId))
        {
            throw new InvalidOperationException("idempotency-result-missing");
        }

        var transactionEntity = await dbContext.LedgerTransactions.SingleOrDefaultAsync(
            record => record.SessionId == sessionId && record.Id == transactionId,
            cancellationToken
        );
        if (transactionEntity is null)
        {
            throw new InvalidOperationException("idempotency-result-missing");
        }

        var postings = await dbContext.LedgerPostings
            .Where(record => record.TransactionId == transactionId)
            .OrderBy(record => record.Id)
            .ToListAsync(cancellationToken);
        var snapshot = await BuildSnapshotAsync(sessionId, cancellationToken);

        return new MoneyCommandResponse(
            snapshot,
            new LedgerTransactionViewResponse(
                transactionEntity.Id,
                transactionEntity.Sequence,
                transactionEntity.Kind,
                transactionEntity.ActorParticipantId,
                transactionEntity.CorrectsTransactionId,
                transactionEntity.Note,
                transactionEntity.CreatedAtUtc,
                postings.Select(posting => new LedgerPostingViewResponse(
                    posting.AccountId,
                    posting.Amount,
                    posting.BalanceAfter
                )).ToList()
            ),
            IdempotentReplay: true
        );
    }

    private async Task<AccountEntity> FindParticipantAccountAsync(
        Guid sessionId,
        Guid participantId,
        CancellationToken cancellationToken
    )
    {
        var participantExists = await dbContext.Participants.AnyAsync(
            record => record.SessionId == sessionId && record.Id == participantId,
            cancellationToken
        );
        if (!participantExists)
        {
            throw new InvalidOperationException("participant-not-found");
        }

        var account = await dbContext.Accounts.SingleOrDefaultAsync(
            record =>
                record.SessionId == sessionId
                && record.OwnerType == "participant"
                && record.OwnerId == participantId,
            cancellationToken
        );
        if (account is null)
        {
            throw new InvalidOperationException("account-not-found");
        }

        return account;
    }

    private async Task<long> GetNextSequenceAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var maxSequence = await dbContext.LedgerTransactions
            .Where(record => record.SessionId == sessionId)
            .Select(record => (long?)record.Sequence)
            .MaxAsync(cancellationToken);
        return (maxSequence ?? 0) + 1;
    }

    private async Task ApplyBalancesAsync(
        Guid sessionId,
        IReadOnlyDictionary<Guid, long> balances,
        CancellationToken cancellationToken
    )
    {
        var accounts = await dbContext.Accounts
            .Where(record => record.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        foreach (var account in accounts)
        {
            if (!balances.TryGetValue(account.Id, out var updatedBalance))
            {
                continue;
            }

            account.Balance = updatedBalance;
            account.Version += 1;
        }
    }

    private LedgerTransactionEntity AddLedgerTransactionEntity(LedgerTransaction transaction)
    {
        var entity = new LedgerTransactionEntity
        {
            Id = transaction.Id,
            SessionId = transaction.SessionId,
            Sequence = transaction.Sequence,
            ActorParticipantId = transaction.ActorParticipantId,
            Kind = transaction.Kind.ToString().ToLowerInvariant(),
            CorrectsTransactionId = transaction.CorrectsTransactionId,
            Note = transaction.Note,
            CreatedAtUtc = transaction.CreatedAtUtc
        };
        dbContext.LedgerTransactions.Add(entity);
        return entity;
    }

    private void AddLedgerPostingEntities(Guid sessionId, LedgerTransaction transaction)
    {
        foreach (var posting in transaction.Postings)
        {
            dbContext.LedgerPostings.Add(
                new LedgerPostingEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    TransactionId = transaction.Id,
                    AccountId = posting.AccountId,
                    Amount = posting.Amount,
                    BalanceAfter = posting.BalanceAfter
                }
            );
        }
    }

    private static IdempotencyRecordEntity CreateIdempotencyRecord<TRequest>(
        Guid sessionId,
        Guid actorParticipantId,
        string idempotencyKey,
        string commandType,
        TRequest request,
        string resultToken
    )
    {
        return new IdempotencyRecordEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ActorParticipantId = actorParticipantId,
            Key = idempotencyKey,
            CommandType = commandType,
            RequestHash = ComputeRequestHash(request),
            ResultHash = resultToken,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static string ComputeRequestHash<T>(T request)
    {
        var json = JsonSerializer.Serialize(request);
        return HashSecret(json);
    }

    private static LedgerTransactionKind ParseLedgerKind(string kind)
    {
        return kind switch
        {
            "transfer" => LedgerTransactionKind.Transfer,
            "correction" => LedgerTransactionKind.Correction,
            _ => throw new InvalidOperationException("invalid-ledger-kind")
        };
    }

    private static LedgerTransactionViewResponse ToLedgerTransactionResponse(LedgerTransaction transaction)
    {
        return new LedgerTransactionViewResponse(
            transaction.Id,
            transaction.Sequence,
            transaction.Kind.ToString().ToLowerInvariant(),
            transaction.ActorParticipantId,
            transaction.CorrectsTransactionId,
            transaction.Note,
            transaction.CreatedAtUtc,
            transaction.Postings.Select(posting => new LedgerPostingViewResponse(
                posting.AccountId,
                posting.Amount,
                posting.BalanceAfter
            )).ToList()
        );
    }

    private static LedgerTransactionViewResponse ToLedgerTransactionResponse(
        LedgerTransactionEntity transaction,
        IReadOnlyList<LedgerPostingEntity> postings
    )
    {
        return new LedgerTransactionViewResponse(
            transaction.Id,
            transaction.Sequence,
            transaction.Kind,
            transaction.ActorParticipantId,
            transaction.CorrectsTransactionId,
            transaction.Note,
            transaction.CreatedAtUtc,
            postings.Select(posting => new LedgerPostingViewResponse(
                posting.AccountId,
                posting.Amount,
                posting.BalanceAfter
            )).ToList()
        );
    }

    private async Task<string> GenerateUniqueRoomCodeAsync(CancellationToken cancellationToken)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var buffer = new byte[6];

        while (true)
        {
            RandomNumberGenerator.Fill(buffer);
            var candidate = string.Concat(buffer.Select(value => chars[value % chars.Length]));
            var exists = await dbContext.GameSessions.AnyAsync(
                session => session.RoomCode == candidate,
                cancellationToken
            );
            if (!exists)
            {
                return candidate;
            }
        }
    }

    private async Task<SessionSnapshotResponse> BuildSnapshotAsync(
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        var session = await dbContext.GameSessions.SingleOrDefaultAsync(
            record => record.Id == sessionId,
            cancellationToken
        );
        if (session is null)
        {
            throw new InvalidOperationException("session-not-found");
        }

        var snapshot = await dbContext.TemplateSnapshots.SingleAsync(
            record => record.Id == session.TemplateSnapshotId,
            cancellationToken
        );
        var participants = await dbContext.Participants
            .Where(record => record.SessionId == sessionId)
            .OrderBy(record => record.JoinOrder)
            .ToListAsync(cancellationToken);
        var accounts = await dbContext.Accounts
            .Where(record => record.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        return new SessionSnapshotResponse(
            session.Id,
            session.RoomCode,
            session.Status,
            session.SessionVersion,
            session.CreatedAtUtc,
            session.HostParticipantId,
            new TemplateSnapshotViewResponse(
                snapshot.Id,
                snapshot.TemplateId,
                snapshot.EditionId,
                snapshot.TemplateVersion,
                snapshot.SchemaVersion,
                snapshot.ContentHash
            ),
            participants.Select(record => new ParticipantViewResponse(
                record.Id,
                record.DisplayName,
                record.Role,
                record.IdentityKey,
                record.JoinOrder
            )).ToList(),
            accounts.Select(record => new AccountViewResponse(
                record.Id,
                record.OwnerId,
                record.OwnerType,
                record.Balance
            )).ToList(),
            DateTimeOffset.UtcNow
        );
    }

    private static SessionConnectionInfoResponse BuildConnectionInfo()
    {
        return new SessionConnectionInfoResponse("/hubs/game", 1);
    }

    private static string CreateReconnectCredential()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashSecret(string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
