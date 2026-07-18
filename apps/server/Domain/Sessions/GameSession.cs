using BankersSeat.Server.Domain.Accounts;
using BankersSeat.Server.Domain.Participants;
using BankersSeat.Server.Domain.Templates;

namespace BankersSeat.Server.Domain.Sessions;

public sealed record GameSession(
    Guid Id,
    string RoomCode,
    SessionStatus Status,
    Guid HostParticipantId,
    int SessionVersion,
    DateTimeOffset CreatedAtUtc,
    TemplateSnapshot TemplateSnapshot,
    IReadOnlyList<Participant> Participants,
    IReadOnlyList<Account> Accounts
);
