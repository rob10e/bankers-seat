namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record SessionLedgerResponse(
    IReadOnlyList<LedgerTransactionViewResponse> Transactions,
    long? NextBeforeSequence
);
