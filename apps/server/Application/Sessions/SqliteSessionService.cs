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
        var bankPolicy = ParseBankPolicy(templateSnapshot.TemplateJson, templateSnapshot.AllowPlayerOverdraft);

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
        var bankAccountEntity = new AccountEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            OwnerType = "bank",
            OwnerId = sessionId,
            Balance = bankPolicy.StartingBankBalance,
            Version = 1
        };
        var hostFieldValues = BuildDefaultPlayerFieldValueEntities(
            sessionId,
            hostParticipantId,
            templateSnapshot.TemplateJson,
            nowUtc
        );

        dbContext.TemplateSnapshots.Add(snapshotEntity);
        dbContext.GameSessions.Add(sessionEntity);
        dbContext.Participants.Add(participantEntity);
        dbContext.Accounts.Add(accountEntity);
        dbContext.Accounts.Add(bankAccountEntity);
        dbContext.PlayerFieldValues.AddRange(hostFieldValues);

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
        var playerFieldValues = BuildDefaultPlayerFieldValueEntities(
            sessionEntity.Id,
            participant.Id,
            templateSnapshot.TemplateJson,
            DateTimeOffset.UtcNow
        );

        sessionEntity.SessionVersion += 1;
        dbContext.Participants.Add(participant);
        dbContext.Accounts.Add(account);
        dbContext.PlayerFieldValues.AddRange(playerFieldValues);
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

    public async Task<MoneyCommandResponse> BankToParticipantAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        BankToParticipantRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateBankToParticipantRequest(request);

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
            "bank-to-participant",
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

        var policy = await GetBankPolicyAsync(session, cancellationToken);
        var bankAccount = await FindBankAccountAsync(sessionId, cancellationToken);
        var playerAccount = await FindParticipantAccountAsync(sessionId, request.ToParticipantId, cancellationToken);
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
                bankAccount.Id,
                playerAccount.Id,
                request.Amount,
                policy.IsUnlimitedBank,
                nextSequence,
                DateTimeOffset.UtcNow,
                string.IsNullOrWhiteSpace(request.Note) ? "Bank payment" : request.Note.Trim()
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
                "bank-to-participant",
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

    public async Task<MoneyCommandResponse> ParticipantToBankAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        ParticipantToBankRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateParticipantToBankRequest(request);

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
            "participant-to-bank",
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

        var policy = await GetBankPolicyAsync(session, cancellationToken);
        var bankAccount = await FindBankAccountAsync(sessionId, cancellationToken);
        var playerAccount = await FindParticipantAccountAsync(sessionId, request.FromParticipantId, cancellationToken);
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
                playerAccount.Id,
                bankAccount.Id,
                request.Amount,
                policy.AllowPlayerOverdraft,
                nextSequence,
                DateTimeOffset.UtcNow,
                string.IsNullOrWhiteSpace(request.Note) ? "Bank collection" : request.Note.Trim()
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
                "participant-to-bank",
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

    public async Task<MoneyCommandResponse> ExecuteTemplateActionAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        string actionId,
        ExecuteTemplateActionRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateExecuteTemplateActionRequest(actionId, request);
        var normalizedActionId = actionId.Trim();

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
            "execute-template-action",
            new { ActionId = normalizedActionId, Request = request },
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

        var snapshot = await dbContext.TemplateSnapshots.SingleAsync(
            record => record.Id == session.TemplateSnapshotId,
            cancellationToken
        );
        var action = ResolveTemplateAction(snapshot.TemplateJson, normalizedActionId);
        var policy = ParseBankPolicy(snapshot.TemplateJson, snapshot.AllowPlayerOverdraft);
        var nextSequence = await GetNextSequenceAsync(sessionId, cancellationToken);
        var note = string.IsNullOrWhiteSpace(request.Note)
            ? $"Action: {action.Label}"
            : request.Note.Trim();
        var nowUtc = DateTimeOffset.UtcNow;
        LedgerTransactionEntity transactionEntity;
        LedgerTransactionViewResponse responseTransaction;

        if (
            action.OperationType is ActionOperationType.BankToPlayer
                or ActionOperationType.PlayerToBank
                or ActionOperationType.PlayerToPlayer
                or ActionOperationType.AdjustPlayerBalance
        )
        {
            var currentBalances = await dbContext.Accounts
                .Where(record => record.SessionId == sessionId)
                .ToDictionaryAsync(record => record.Id, record => record.Balance, cancellationToken);
            var instructions = await ResolveActionTransferInstructionsAsync(
                sessionId,
                action,
                request,
                policy,
                cancellationToken
            );

            MoneyMutationResult mutation;
            try
            {
                mutation = MoneyMutationEngine.ApplyTransferBatch(
                    currentBalances,
                    sessionId,
                    actorParticipantId,
                    instructions,
                    nextSequence,
                    nowUtc,
                    note
                );
            }
            catch (DomainRuleViolationException exception)
            {
                throw new InvalidOperationException(exception.Code);
            }

            await ApplyBalancesAsync(sessionId, mutation.UpdatedBalances, cancellationToken);
            transactionEntity = AddLedgerTransactionEntity(mutation.Transaction);
            AddLedgerPostingEntities(sessionId, mutation.Transaction);
            responseTransaction = ToLedgerTransactionResponse(mutation.Transaction);
        }
        else if (action.OperationType == ActionOperationType.Composite)
        {
            if (action.Steps.Count == 0)
            {
                throw new InvalidOperationException("invalid-template-snapshot");
            }

            var financialInstructions = new List<TransferInstruction>();
            var fieldSteps = new List<ResolvedTemplateAction>();
            foreach (var step in action.Steps)
            {
                var stepAction = new ResolvedTemplateAction(
                    step.OperationType,
                    step.Amount,
                    action.Label,
                    action.Scope,
                    step.Operation,
                    []
                );
                if (
                    step.OperationType is ActionOperationType.BankToPlayer
                        or ActionOperationType.PlayerToBank
                        or ActionOperationType.PlayerToPlayer
                        or ActionOperationType.AdjustPlayerBalance
                )
                {
                    var stepInstructions = await ResolveActionTransferInstructionsAsync(
                        sessionId,
                        stepAction,
                        request,
                        policy,
                        cancellationToken
                    );
                    financialInstructions.AddRange(stepInstructions);
                }
                else if (
                    step.OperationType is ActionOperationType.SetField
                        or ActionOperationType.ToggleField
                        or ActionOperationType.IncrementField
                )
                {
                    fieldSteps.Add(stepAction);
                }
                else
                {
                    throw new InvalidOperationException("unsupported-template-action");
                }
            }

            foreach (var fieldStep in fieldSteps)
            {
                await ApplyFieldActionAsync(
                    sessionId,
                    snapshot.TemplateJson,
                    fieldStep,
                    request,
                    nowUtc,
                    cancellationToken
                );
            }

            if (financialInstructions.Count > 0)
            {
                var currentBalances = await dbContext.Accounts
                    .Where(record => record.SessionId == sessionId)
                    .ToDictionaryAsync(record => record.Id, record => record.Balance, cancellationToken);
                MoneyMutationResult mutation;
                try
                {
                    mutation = MoneyMutationEngine.ApplyTransferBatch(
                        currentBalances,
                        sessionId,
                        actorParticipantId,
                        financialInstructions,
                        nextSequence,
                        nowUtc,
                        note
                    );
                }
                catch (DomainRuleViolationException exception)
                {
                    throw new InvalidOperationException(exception.Code);
                }

                await ApplyBalancesAsync(sessionId, mutation.UpdatedBalances, cancellationToken);
                var actionTransaction = mutation.Transaction with { Kind = LedgerTransactionKind.Action };
                transactionEntity = AddLedgerTransactionEntity(actionTransaction);
                AddLedgerPostingEntities(sessionId, actionTransaction);
                responseTransaction = ToLedgerTransactionResponse(actionTransaction);
            }
            else
            {
                var actionTransaction = new LedgerTransaction(
                    Guid.NewGuid(),
                    sessionId,
                    nextSequence,
                    actorParticipantId,
                    LedgerTransactionKind.Action,
                    null,
                    note,
                    nowUtc,
                    []
                );
                transactionEntity = AddLedgerTransactionEntity(actionTransaction);
                responseTransaction = ToLedgerTransactionResponse(actionTransaction);
            }
        }
        else if (
            action.OperationType is ActionOperationType.SetField
                or ActionOperationType.ToggleField
                or ActionOperationType.IncrementField
        )
        {
            await ApplyFieldActionAsync(
                sessionId,
                snapshot.TemplateJson,
                action,
                request,
                nowUtc,
                cancellationToken
            );
            var actionTransaction = new LedgerTransaction(
                Guid.NewGuid(),
                sessionId,
                nextSequence,
                actorParticipantId,
                LedgerTransactionKind.Action,
                null,
                note,
                nowUtc,
                []
            );
            transactionEntity = AddLedgerTransactionEntity(actionTransaction);
            responseTransaction = ToLedgerTransactionResponse(actionTransaction);
        }
        else
        {
            throw new InvalidOperationException("unsupported-template-action");
        }

        session.SessionVersion += 1;
        var resultToken = transactionEntity.Id.ToString("N");
        dbContext.IdempotencyRecords.Add(
            CreateIdempotencyRecord(
                sessionId,
                actorParticipantId,
                request.IdempotencyKey.Trim(),
                "execute-template-action",
                new { ActionId = normalizedActionId, Request = request },
                resultToken
            )
        );

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var updatedSnapshot = await BuildSnapshotAsync(sessionId, cancellationToken);
        return new MoneyCommandResponse(updatedSnapshot, responseTransaction, IdempotentReplay: false);
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

    private static void ValidateBankToParticipantRequest(BankToParticipantRequest request)
    {
        if (
            request.ToParticipantId == Guid.Empty
            || request.ExpectedSessionVersion <= 0
            || request.Amount <= 0
            || string.IsNullOrWhiteSpace(request.IdempotencyKey)
        )
        {
            throw new InvalidOperationException("invalid-request");
        }
    }

    private static void ValidateParticipantToBankRequest(ParticipantToBankRequest request)
    {
        if (
            request.FromParticipantId == Guid.Empty
            || request.ExpectedSessionVersion <= 0
            || request.Amount <= 0
            || string.IsNullOrWhiteSpace(request.IdempotencyKey)
        )
        {
            throw new InvalidOperationException("invalid-request");
        }
    }

    private static void ValidateExecuteTemplateActionRequest(
        string actionId,
        ExecuteTemplateActionRequest request
    )
    {
        if (
            string.IsNullOrWhiteSpace(actionId)
            || request.ExpectedSessionVersion <= 0
            || string.IsNullOrWhiteSpace(request.IdempotencyKey)
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

    private async Task<AccountEntity> FindBankAccountAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(
            record =>
                record.SessionId == sessionId
                && record.OwnerType == "bank"
                && record.OwnerId == sessionId,
            cancellationToken
        );
        if (account is null)
        {
            throw new InvalidOperationException("account-not-found");
        }

        return account;
    }

    private async Task<BankPolicy> GetBankPolicyAsync(
        GameSessionEntity session,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await dbContext.TemplateSnapshots.SingleAsync(
            record => record.Id == session.TemplateSnapshotId,
            cancellationToken
        );
        return ParseBankPolicy(snapshot.TemplateJson, snapshot.AllowPlayerOverdraft);
    }

    private static BankPolicy ParseBankPolicy(string templateJson, bool allowPlayerOverdraft)
    {
        using var document = JsonDocument.Parse(templateJson);
        var root = document.RootElement;
        if (
            !root.TryGetProperty("bank", out var bank)
            || !bank.TryGetProperty("bankMode", out var bankModeProperty)
            || bankModeProperty.ValueKind != JsonValueKind.String
        )
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        var bankMode = bankModeProperty.GetString();
        var isUnlimited = string.Equals(bankMode, "unlimited", StringComparison.Ordinal);
        long startingBankBalance = 0;
        if (
            bank.TryGetProperty("startingBankBalance", out var startingBankBalanceProperty)
            && startingBankBalanceProperty.ValueKind == JsonValueKind.Number
            && startingBankBalanceProperty.TryGetInt64(out var parsedStartingBalance)
        )
        {
            startingBankBalance = parsedStartingBalance;
        }

        return new BankPolicy(isUnlimited, allowPlayerOverdraft, startingBankBalance);
    }

    private static IReadOnlyDictionary<string, PlayerFieldDefinition> ParsePlayerFieldDefinitions(
        string templateJson
    )
    {
        using var document = JsonDocument.Parse(templateJson);
        var root = document.RootElement;
        if (
            !root.TryGetProperty("playerFields", out var playerFields)
            || playerFields.ValueKind != JsonValueKind.Array
        )
        {
            return new Dictionary<string, PlayerFieldDefinition>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, PlayerFieldDefinition>(StringComparer.Ordinal);
        foreach (var field in playerFields.EnumerateArray())
        {
            if (
                !field.TryGetProperty("id", out var idProperty)
                || idProperty.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(idProperty.GetString())
                || !field.TryGetProperty("type", out var typeProperty)
                || typeProperty.ValueKind != JsonValueKind.String
            )
            {
                throw new InvalidOperationException("invalid-template-snapshot");
            }

            var id = idProperty.GetString()!;
            var type = typeProperty.GetString()!;
            long? minimum = null;
            long? maximum = null;
            int? maximumLength = null;
            var enumOptions = new HashSet<string>(StringComparer.Ordinal);
            if (
                field.TryGetProperty("minimum", out var minimumProperty)
                && minimumProperty.ValueKind == JsonValueKind.Number
                && minimumProperty.TryGetInt64(out var parsedMinimum)
            )
            {
                minimum = parsedMinimum;
            }

            if (
                field.TryGetProperty("maximum", out var maximumProperty)
                && maximumProperty.ValueKind == JsonValueKind.Number
                && maximumProperty.TryGetInt64(out var parsedMaximum)
            )
            {
                maximum = parsedMaximum;
            }

            if (
                field.TryGetProperty("maximumLength", out var maxLengthProperty)
                && maxLengthProperty.ValueKind == JsonValueKind.Number
                && maxLengthProperty.TryGetInt32(out var parsedMaxLength)
            )
            {
                maximumLength = parsedMaxLength;
            }

            if (field.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array)
            {
                foreach (var option in options.EnumerateArray())
                {
                    if (
                        option.TryGetProperty("value", out var valueProperty)
                        && valueProperty.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(valueProperty.GetString())
                    )
                    {
                        enumOptions.Add(valueProperty.GetString()!);
                    }
                }
            }

            result[id] = new PlayerFieldDefinition(id, type, minimum, maximum, maximumLength, enumOptions);
        }

        return result;
    }

    private static IReadOnlyList<PlayerFieldValueEntity> BuildDefaultPlayerFieldValueEntities(
        Guid sessionId,
        Guid participantId,
        string templateJson,
        DateTimeOffset updatedAtUtc
    )
    {
        using var document = JsonDocument.Parse(templateJson);
        var root = document.RootElement;
        if (
            !root.TryGetProperty("playerFields", out var playerFields)
            || playerFields.ValueKind != JsonValueKind.Array
        )
        {
            return [];
        }

        var entities = new List<PlayerFieldValueEntity>();
        foreach (var field in playerFields.EnumerateArray())
        {
            if (
                !field.TryGetProperty("id", out var idProperty)
                || idProperty.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(idProperty.GetString())
                || !field.TryGetProperty("default", out var defaultProperty)
            )
            {
                throw new InvalidOperationException("invalid-template-snapshot");
            }

            entities.Add(
                new PlayerFieldValueEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    ParticipantId = participantId,
                    FieldId = idProperty.GetString()!,
                    ValueJson = defaultProperty.GetRawText(),
                    Version = 1,
                    UpdatedAtUtc = updatedAtUtc
                }
            );
        }

        return entities;
    }

    private static ResolvedTemplateAction ResolveTemplateAction(string templateJson, string actionId)
    {
        using var document = JsonDocument.Parse(templateJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("action-not-found");
        }

        foreach (var action in actions.EnumerateArray())
        {
            if (
                !action.TryGetProperty("id", out var actionIdProperty)
                || actionIdProperty.ValueKind != JsonValueKind.String
                || !string.Equals(actionIdProperty.GetString(), actionId, StringComparison.Ordinal)
            )
            {
                continue;
            }

            if (
                !action.TryGetProperty("label", out var labelProperty)
                || labelProperty.ValueKind != JsonValueKind.String
                || !action.TryGetProperty("scope", out var scopeProperty)
                || scopeProperty.ValueKind != JsonValueKind.String
                || !action.TryGetProperty("operation", out var operation)
                || operation.ValueKind != JsonValueKind.Object
                || !operation.TryGetProperty("type", out var typeProperty)
                || typeProperty.ValueKind != JsonValueKind.String
            )
            {
                throw new InvalidOperationException("invalid-template-snapshot");
            }

            var operationType = typeProperty.GetString();
            var label = labelProperty.GetString()!;
            var scope = scopeProperty.GetString()!;
            return operationType switch
            {
                "bank-to-player" => new ResolvedTemplateAction(
                    ActionOperationType.BankToPlayer,
                    ReadActionAmount(operation),
                    label,
                    scope,
                    operation.Clone(),
                    []
                ),
                "player-to-bank" => new ResolvedTemplateAction(
                    ActionOperationType.PlayerToBank,
                    ReadActionAmount(operation),
                    label,
                    scope,
                    operation.Clone(),
                    []
                ),
                "player-to-player" => new ResolvedTemplateAction(
                    ActionOperationType.PlayerToPlayer,
                    ReadActionAmount(operation),
                    label,
                    scope,
                    operation.Clone(),
                    []
                ),
                "set-field" => new ResolvedTemplateAction(
                    ActionOperationType.SetField,
                    null,
                    label,
                    scope,
                    operation.Clone(),
                    []
                ),
                "toggle-field" => new ResolvedTemplateAction(
                    ActionOperationType.ToggleField,
                    null,
                    label,
                    scope,
                    operation.Clone(),
                    []
                ),
                "increment-field" => new ResolvedTemplateAction(
                    ActionOperationType.IncrementField,
                    null,
                    label,
                    scope,
                    operation.Clone(),
                    []
                ),
                "adjust-player-balance" => new ResolvedTemplateAction(
                    ActionOperationType.AdjustPlayerBalance,
                    ReadSignedAdjustAmount(operation),
                    label,
                    scope,
                    operation.Clone(),
                    []
                ),
                "composite" => new ResolvedTemplateAction(
                    ActionOperationType.Composite,
                    null,
                    label,
                    scope,
                    operation.Clone(),
                    ResolveCompositeSteps(operation)
                ),
                _ => throw new InvalidOperationException("unsupported-template-action")
            };
        }

        throw new InvalidOperationException("action-not-found");
    }

    private async Task<IReadOnlyList<TransferInstruction>> ResolveAdjustBalanceInstructionsAsync(
            Guid sessionId,
            ResolvedTemplateAction action,
            ExecuteTemplateActionRequest request,
            BankPolicy policy,
            CancellationToken cancellationToken
        )
        {
            if (!action.Amount.HasValue || action.Amount.Value == long.MinValue)
            {
                throw new InvalidOperationException("invalid-template-snapshot");
            }

            var amount = action.Amount.Value;
            var magnitude = amount > 0 ? amount : -amount;
            var positiveAdjust = amount > 0;
            var bankAccount = await FindBankAccountAsync(sessionId, cancellationToken);

            if (string.Equals(action.Scope, "single-player", StringComparison.Ordinal))
            {
                if (!request.PrimaryParticipantId.HasValue || request.PrimaryParticipantId.Value == Guid.Empty)
                {
                    throw new InvalidOperationException("invalid-request");
                }

                var participantAccount = await FindParticipantAccountAsync(
                    sessionId,
                    request.PrimaryParticipantId.Value,
                    cancellationToken
                );
                if (positiveAdjust)
                {
                    return [new TransferInstruction(
                        bankAccount.Id,
                        participantAccount.Id,
                        magnitude,
                        policy.IsUnlimitedBank
                    )];
                }

                return [new TransferInstruction(
                    participantAccount.Id,
                    bankAccount.Id,
                    magnitude,
                    policy.AllowPlayerOverdraft
                )];
            }

            if (!string.Equals(action.Scope, "all-players", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("unsupported-template-action");
            }

            var participantAccounts = await dbContext.Accounts
                .Where(record => record.SessionId == sessionId && record.OwnerType == "participant")
                .OrderBy(record => record.OwnerId)
                .ToListAsync(cancellationToken);
            if (participantAccounts.Count == 0)
            {
                throw new InvalidOperationException("invalid-request");
            }

            if (positiveAdjust)
            {
                return participantAccounts.Select(account => new TransferInstruction(
                    bankAccount.Id,
                    account.Id,
                    magnitude,
                    policy.IsUnlimitedBank
                )).ToList();
            }

            return participantAccounts.Select(account => new TransferInstruction(
                account.Id,
                bankAccount.Id,
                magnitude,
                policy.AllowPlayerOverdraft
            )).ToList();
        }

    private static IReadOnlyList<ResolvedTemplateActionStep> ResolveCompositeSteps(JsonElement operation)
    {
        if (!operation.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        var resolved = new List<ResolvedTemplateActionStep>();
        foreach (var step in steps.EnumerateArray())
        {
            if (
                step.ValueKind != JsonValueKind.Object
                || !step.TryGetProperty("type", out var typeProperty)
                || typeProperty.ValueKind != JsonValueKind.String
            )
            {
                throw new InvalidOperationException("invalid-template-snapshot");
            }

            var stepType = typeProperty.GetString();
            resolved.Add(stepType switch
            {
                "bank-to-player" => new ResolvedTemplateActionStep(
                    ActionOperationType.BankToPlayer,
                    ReadActionAmount(step),
                    step.Clone()
                ),
                "player-to-bank" => new ResolvedTemplateActionStep(
                    ActionOperationType.PlayerToBank,
                    ReadActionAmount(step),
                    step.Clone()
                ),
                "player-to-player" => new ResolvedTemplateActionStep(
                    ActionOperationType.PlayerToPlayer,
                    ReadActionAmount(step),
                    step.Clone()
                ),
                "adjust-player-balance" => new ResolvedTemplateActionStep(
                    ActionOperationType.AdjustPlayerBalance,
                    ReadSignedAdjustAmount(step),
                    step.Clone()
                ),
                "set-field" => new ResolvedTemplateActionStep(
                    ActionOperationType.SetField,
                    null,
                    step.Clone()
                ),
                "toggle-field" => new ResolvedTemplateActionStep(
                    ActionOperationType.ToggleField,
                    null,
                    step.Clone()
                ),
                "increment-field" => new ResolvedTemplateActionStep(
                    ActionOperationType.IncrementField,
                    null,
                    step.Clone()
                ),
                _ => throw new InvalidOperationException("unsupported-template-action")
            });
        }

        if (resolved.Count == 0)
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        return resolved;
    }

    private static long ReadActionAmount(JsonElement operation)
    {
        if (
            !operation.TryGetProperty("amount", out var amountProperty)
            || amountProperty.ValueKind != JsonValueKind.Number
            || !amountProperty.TryGetInt64(out var amount)
            || amount <= 0
        )
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        return amount;
    }

    private static long ReadSignedAdjustAmount(JsonElement operation)
    {
        if (
            !operation.TryGetProperty("amount", out var amountProperty)
            || amountProperty.ValueKind != JsonValueKind.Number
            || !amountProperty.TryGetInt64(out var amount)
            || amount == 0
        )
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        return amount;
    }

    private async Task<IReadOnlyList<TransferInstruction>> ResolveActionTransferInstructionsAsync(
        Guid sessionId,
        ResolvedTemplateAction action,
        ExecuteTemplateActionRequest request,
        BankPolicy policy,
        CancellationToken cancellationToken
    )
    {
        if (action.OperationType == ActionOperationType.AdjustPlayerBalance)
        {
            return await ResolveAdjustBalanceInstructionsAsync(
                sessionId,
                action,
                request,
                policy,
                cancellationToken
            );
        }

        if (!action.Amount.HasValue)
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        if (string.Equals(action.Scope, "single-player", StringComparison.Ordinal))
        {
            return await ResolveSinglePlayerActionInstructionsAsync(
                sessionId,
                action,
                request,
                policy,
                cancellationToken
            );
        }

        if (string.Equals(action.Scope, "two-players", StringComparison.Ordinal))
        {
            if (action.OperationType != ActionOperationType.PlayerToPlayer)
            {
                throw new InvalidOperationException("unsupported-template-action");
            }

            return await ResolveSinglePlayerActionInstructionsAsync(
                sessionId,
                action,
                request,
                policy,
                cancellationToken
            );
        }

        if (!string.Equals(action.Scope, "all-players", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unsupported-template-action");
        }

        if (action.OperationType == ActionOperationType.PlayerToPlayer)
        {
            throw new InvalidOperationException("unsupported-template-action");
        }

        var bankAccount = await FindBankAccountAsync(sessionId, cancellationToken);
        var participantAccounts = await dbContext.Accounts
            .Where(record => record.SessionId == sessionId && record.OwnerType == "participant")
            .OrderBy(record => record.OwnerId)
            .ToListAsync(cancellationToken);
        if (participantAccounts.Count == 0)
        {
            throw new InvalidOperationException("invalid-request");
        }

        return action.OperationType switch
        {
            ActionOperationType.BankToPlayer => participantAccounts
                .Select(account => new TransferInstruction(
                    bankAccount.Id,
                    account.Id,
                    action.Amount.Value,
                    policy.IsUnlimitedBank
                ))
                .ToList(),
            ActionOperationType.PlayerToBank => participantAccounts
                .Select(account => new TransferInstruction(
                    account.Id,
                    bankAccount.Id,
                    action.Amount.Value,
                    policy.AllowPlayerOverdraft
                ))
                .ToList(),
            _ => throw new InvalidOperationException("unsupported-template-action")
        };
    }

    private async Task<IReadOnlyList<TransferInstruction>> ResolveSinglePlayerActionInstructionsAsync(
        Guid sessionId,
        ResolvedTemplateAction action,
        ExecuteTemplateActionRequest request,
        BankPolicy policy,
        CancellationToken cancellationToken
    )
    {
        AccountEntity fromAccount;
        AccountEntity toAccount;
        bool allowFromOverdraft;

        switch (action.OperationType)
        {
            case ActionOperationType.BankToPlayer:
                if (!request.PrimaryParticipantId.HasValue || request.PrimaryParticipantId.Value == Guid.Empty)
                {
                    throw new InvalidOperationException("invalid-request");
                }

                fromAccount = await FindBankAccountAsync(sessionId, cancellationToken);
                toAccount = await FindParticipantAccountAsync(
                    sessionId,
                    request.PrimaryParticipantId.Value,
                    cancellationToken
                );
                allowFromOverdraft = policy.IsUnlimitedBank;
                break;
            case ActionOperationType.PlayerToBank:
                if (!request.PrimaryParticipantId.HasValue || request.PrimaryParticipantId.Value == Guid.Empty)
                {
                    throw new InvalidOperationException("invalid-request");
                }

                fromAccount = await FindParticipantAccountAsync(
                    sessionId,
                    request.PrimaryParticipantId.Value,
                    cancellationToken
                );
                toAccount = await FindBankAccountAsync(sessionId, cancellationToken);
                allowFromOverdraft = policy.AllowPlayerOverdraft;
                break;
            case ActionOperationType.PlayerToPlayer:
                if (
                    !request.PrimaryParticipantId.HasValue
                    || request.PrimaryParticipantId.Value == Guid.Empty
                    || !request.SecondaryParticipantId.HasValue
                    || request.SecondaryParticipantId.Value == Guid.Empty
                )
                {
                    throw new InvalidOperationException("invalid-request");
                }

                fromAccount = await FindParticipantAccountAsync(
                    sessionId,
                    request.PrimaryParticipantId.Value,
                    cancellationToken
                );
                toAccount = await FindParticipantAccountAsync(
                    sessionId,
                    request.SecondaryParticipantId.Value,
                    cancellationToken
                );
                allowFromOverdraft = policy.AllowPlayerOverdraft;
                break;
            default:
                throw new InvalidOperationException("unsupported-template-action");
        }

        if (!action.Amount.HasValue)
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        return [new TransferInstruction(fromAccount.Id, toAccount.Id, action.Amount.Value, allowFromOverdraft)];
    }

    private async Task ApplyFieldActionAsync(
        Guid sessionId,
        string templateJson,
        ResolvedTemplateAction action,
        ExecuteTemplateActionRequest request,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken
    )
    {
        if (
            !action.Operation.TryGetProperty("fieldId", out var fieldIdProperty)
            || fieldIdProperty.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(fieldIdProperty.GetString())
        )
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        var fieldId = fieldIdProperty.GetString()!;
        var definitions = ParsePlayerFieldDefinitions(templateJson);
        if (!definitions.TryGetValue(fieldId, out var definition))
        {
            throw new InvalidOperationException("action-field-reference-missing");
        }

        if (
            action.OperationType == ActionOperationType.IncrementField
            && definition.Type is not ("integer" or "counter" or "currency")
        )
        {
            throw new InvalidOperationException("unsupported-template-action");
        }

        if (action.OperationType == ActionOperationType.ToggleField && definition.Type != "boolean")
        {
            throw new InvalidOperationException("unsupported-template-action");
        }

        var targetParticipantIds = await ResolveFieldActionParticipantIdsAsync(
            sessionId,
            action.Scope,
            request,
            cancellationToken
        );
        if (targetParticipantIds.Count == 0)
        {
            throw new InvalidOperationException("invalid-request");
        }

        var fieldValues = await dbContext.PlayerFieldValues
            .Where(record =>
                record.SessionId == sessionId
                && record.FieldId == fieldId
                && targetParticipantIds.Contains(record.ParticipantId)
            )
            .ToDictionaryAsync(record => record.ParticipantId, cancellationToken);

        foreach (var participantId in targetParticipantIds)
        {
            if (!fieldValues.TryGetValue(participantId, out var entity))
            {
                throw new InvalidOperationException("field-value-not-found");
            }

            var updatedValueJson = action.OperationType switch
            {
                ActionOperationType.SetField => ResolveSetFieldValueJson(action.Operation, definition),
                ActionOperationType.ToggleField => ResolveToggledFieldValueJson(entity.ValueJson),
                ActionOperationType.IncrementField => ResolveIncrementedFieldValueJson(
                    action.Operation,
                    definition,
                    entity.ValueJson
                ),
                _ => throw new InvalidOperationException("unsupported-template-action")
            };

            entity.ValueJson = updatedValueJson;
            entity.Version += 1;
            entity.UpdatedAtUtc = updatedAtUtc;
        }
    }

    private async Task<IReadOnlyList<Guid>> ResolveFieldActionParticipantIdsAsync(
        Guid sessionId,
        string scope,
        ExecuteTemplateActionRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.Equals(scope, "single-player", StringComparison.Ordinal))
        {
            if (!request.PrimaryParticipantId.HasValue || request.PrimaryParticipantId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("invalid-request");
            }

            return [request.PrimaryParticipantId.Value];
        }

        if (string.Equals(scope, "all-players", StringComparison.Ordinal))
        {
            return await dbContext.Participants
                .Where(record => record.SessionId == sessionId)
                .OrderBy(record => record.JoinOrder)
                .Select(record => record.Id)
                .ToListAsync(cancellationToken);
        }

        throw new InvalidOperationException("unsupported-template-action");
    }

    private static string ResolveSetFieldValueJson(JsonElement operation, PlayerFieldDefinition definition)
    {
        if (!operation.TryGetProperty("value", out var value))
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        return ValidateAndNormalizeFieldValueJson(definition, value);
    }

    private static string ResolveIncrementedFieldValueJson(
        JsonElement operation,
        PlayerFieldDefinition definition,
        string currentValueJson
    )
    {
        if (
            !operation.TryGetProperty("amount", out var amountProperty)
            || amountProperty.ValueKind != JsonValueKind.Number
            || !amountProperty.TryGetInt64(out var amount)
        )
        {
            throw new InvalidOperationException("invalid-template-snapshot");
        }

        using var currentDocument = JsonDocument.Parse(currentValueJson);
        var currentValue = currentDocument.RootElement;
        if (currentValue.ValueKind != JsonValueKind.Number || !currentValue.TryGetInt64(out var current))
        {
            throw new InvalidOperationException("field-value-invalid");
        }

        var updated = checked(current + amount);
        if (definition.Minimum.HasValue && updated < definition.Minimum.Value)
        {
            throw new InvalidOperationException("field-value-out-of-range");
        }

        if (definition.Maximum.HasValue && updated > definition.Maximum.Value)
        {
            throw new InvalidOperationException("field-value-out-of-range");
        }

        return JsonSerializer.Serialize(updated);
    }

    private static string ResolveToggledFieldValueJson(string currentValueJson)
    {
        using var currentDocument = JsonDocument.Parse(currentValueJson);
        var currentValue = currentDocument.RootElement;
        if (
            currentValue.ValueKind != JsonValueKind.True
            && currentValue.ValueKind != JsonValueKind.False
        )
        {
            throw new InvalidOperationException("field-value-invalid");
        }

        return currentValue.ValueKind == JsonValueKind.True ? "false" : "true";
    }

    private static string ValidateAndNormalizeFieldValueJson(
        PlayerFieldDefinition definition,
        JsonElement value
    )
    {
        return definition.Type switch
        {
            "boolean" when value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False =>
                value.GetRawText(),
            "text" => ValidateTextFieldValue(definition, value),
            "enum" => ValidateEnumFieldValue(definition, value),
            "integer" or "counter" or "currency" => ValidateNumericFieldValue(definition, value),
            _ => throw new InvalidOperationException("unsupported-template-action")
        };
    }

    private static string ValidateTextFieldValue(PlayerFieldDefinition definition, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("field-value-type-mismatch");
        }

        var text = value.GetString() ?? string.Empty;
        if (definition.MaximumLength.HasValue && text.Length > definition.MaximumLength.Value)
        {
            throw new InvalidOperationException("field-value-out-of-range");
        }

        return JsonSerializer.Serialize(text);
    }

    private static string ValidateEnumFieldValue(PlayerFieldDefinition definition, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("field-value-type-mismatch");
        }

        var selected = value.GetString() ?? string.Empty;
        if (!definition.EnumOptions.Contains(selected))
        {
            throw new InvalidOperationException("field-value-invalid-option");
        }

        return JsonSerializer.Serialize(selected);
    }

    private static string ValidateNumericFieldValue(PlayerFieldDefinition definition, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var numeric))
        {
            throw new InvalidOperationException("field-value-type-mismatch");
        }

        if (definition.Minimum.HasValue && numeric < definition.Minimum.Value)
        {
            throw new InvalidOperationException("field-value-out-of-range");
        }

        if (definition.Maximum.HasValue && numeric > definition.Maximum.Value)
        {
            throw new InvalidOperationException("field-value-out-of-range");
        }

        return JsonSerializer.Serialize(numeric);
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
            "action" => LedgerTransactionKind.Action,
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
        var playerFieldValues = await dbContext.PlayerFieldValues
            .Where(record => record.SessionId == sessionId)
            .OrderBy(record => record.ParticipantId)
            .ThenBy(record => record.FieldId)
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
            playerFieldValues.Select(record => new PlayerFieldValueViewResponse(
                record.ParticipantId,
                record.FieldId,
                record.ValueJson
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

    private sealed record BankPolicy(
        bool IsUnlimitedBank,
        bool AllowPlayerOverdraft,
        long StartingBankBalance
    );

    private sealed record ResolvedTemplateAction(
        ActionOperationType OperationType,
        long? Amount,
        string Label,
        string Scope,
        JsonElement Operation,
        IReadOnlyList<ResolvedTemplateActionStep> Steps
    );

    private sealed record ResolvedTemplateActionStep(
        ActionOperationType OperationType,
        long? Amount,
        JsonElement Operation
    );

    private sealed record PlayerFieldDefinition(
        string Id,
        string Type,
        long? Minimum,
        long? Maximum,
        int? MaximumLength,
        ISet<string> EnumOptions
    );

    private enum ActionOperationType
    {
        BankToPlayer,
        PlayerToBank,
        PlayerToPlayer,
        AdjustPlayerBalance,
        SetField,
        ToggleField,
        IncrementField,
        Composite
    }
}
