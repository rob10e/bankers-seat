namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

public sealed class TemplateSnapshotEntity
{
    public Guid Id { get; set; }

    public string TemplateId { get; set; } = string.Empty;

    public string EditionId { get; set; } = string.Empty;

    public string TemplateVersion { get; set; } = string.Empty;

    public int SchemaVersion { get; set; }

    public string ContentHash { get; set; } = string.Empty;

    public string TemplateJson { get; set; } = string.Empty;

    public long StartingPlayerBalance { get; set; }

    public bool AllowPlayerOverdraft { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
