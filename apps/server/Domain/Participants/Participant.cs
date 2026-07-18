namespace BankersSeat.Server.Domain.Participants;

public sealed record Participant(
    Guid Id,
    Guid SessionId,
    string DisplayName,
    ParticipantRole Role,
    string IdentityKey,
    int JoinOrder,
    DateTimeOffset CreatedAtUtc,
    string ReconnectSecretHash
);
