namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record CorrectTransactionRequest(
    Guid TransactionId,
    int ExpectedSessionVersion,
    string IdempotencyKey,
    string Reason
);
