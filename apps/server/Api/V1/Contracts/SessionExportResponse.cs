namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record SessionExportResponse(
    SessionSnapshotResponse Snapshot,
    IReadOnlyList<LedgerTransactionViewResponse> Transactions,
    DateTimeOffset ExportedAtUtc
);
