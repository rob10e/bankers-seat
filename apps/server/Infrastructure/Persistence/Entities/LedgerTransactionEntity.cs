namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

public sealed class LedgerTransactionEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public long Sequence { get; set; }

    public Guid ActorParticipantId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public Guid? CorrectsTransactionId { get; set; }

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
