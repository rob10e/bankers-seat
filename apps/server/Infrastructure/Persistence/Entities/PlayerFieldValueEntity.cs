namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

public sealed class PlayerFieldValueEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid ParticipantId { get; set; }

    public string FieldId { get; set; } = string.Empty;

    public string ValueJson { get; set; } = string.Empty;

    public int Version { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
