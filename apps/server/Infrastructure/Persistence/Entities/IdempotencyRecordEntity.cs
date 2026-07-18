namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

public sealed class IdempotencyRecordEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid ActorParticipantId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string CommandType { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public string ResultHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
