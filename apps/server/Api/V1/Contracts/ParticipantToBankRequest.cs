namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record ParticipantToBankRequest(
    Guid FromParticipantId,
    long Amount,
    int ExpectedSessionVersion,
    string IdempotencyKey,
    string Note
);
