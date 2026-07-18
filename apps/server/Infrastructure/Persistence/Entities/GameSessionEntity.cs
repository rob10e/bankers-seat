namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

public sealed class GameSessionEntity
{
    public Guid Id { get; set; }

    public string RoomCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid HostParticipantId { get; set; }

    public Guid TemplateSnapshotId { get; set; }

    public int SessionVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
