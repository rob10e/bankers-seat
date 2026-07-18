namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record TransferBetweenParticipantsRequest(
    Guid FromParticipantId,
    Guid ToParticipantId,
    long Amount,
    int ExpectedSessionVersion,
    string IdempotencyKey,
    string Note
);
