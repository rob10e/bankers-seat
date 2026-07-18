namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record ExecuteTemplateActionRequest(
    Guid? PrimaryParticipantId,
    Guid? SecondaryParticipantId,
    int ExpectedSessionVersion,
    string IdempotencyKey,
    string Note
);
