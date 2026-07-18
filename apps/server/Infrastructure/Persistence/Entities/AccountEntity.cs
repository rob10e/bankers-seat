namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

public sealed class AccountEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public string OwnerType { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }

    public long Balance { get; set; }

    public int Version { get; set; }
}
