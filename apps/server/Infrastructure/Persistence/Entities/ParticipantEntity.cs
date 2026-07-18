namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

public sealed class ParticipantEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string IdentityKey { get; set; } = string.Empty;

    public int JoinOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string ReconnectSecretHash { get; set; } = string.Empty;
}
