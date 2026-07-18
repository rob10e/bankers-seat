namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record BankToParticipantRequest(
    Guid ToParticipantId,
    long Amount,
    int ExpectedSessionVersion,
    string IdempotencyKey,
    string Note
);
