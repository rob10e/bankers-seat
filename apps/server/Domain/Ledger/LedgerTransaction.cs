namespace BankersSeat.Server.Domain.Ledger;

public enum LedgerTransactionKind
{
    Transfer = 0,
    Correction = 1
}

public sealed record LedgerPosting(Guid AccountId, long Amount, long BalanceAfter);

public sealed record LedgerTransaction(
    Guid Id,
    Guid SessionId,
    long Sequence,
    Guid ActorParticipantId,
    LedgerTransactionKind Kind,
    Guid? CorrectsTransactionId,
    string Note,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<LedgerPosting> Postings
);

public sealed record MoneyMutationResult(
    IReadOnlyDictionary<Guid, long> UpdatedBalances,
    LedgerTransaction Transaction
);
