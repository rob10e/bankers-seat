namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record MoneyCommandResponse(
    SessionSnapshotResponse Snapshot,
    LedgerTransactionViewResponse Transaction,
    bool IdempotentReplay
);

public sealed record LedgerTransactionViewResponse(
    Guid TransactionId,
    long Sequence,
    string Kind,
    Guid ActorParticipantId,
    Guid? CorrectsTransactionId,
    string Note,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<LedgerPostingViewResponse> Postings
);

public sealed record LedgerPostingViewResponse(
    Guid AccountId,
    long Amount,
    long BalanceAfter
);
