namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

public sealed class LedgerPostingEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid TransactionId { get; set; }

    public Guid AccountId { get; set; }

    public long Amount { get; set; }

    public long BalanceAfter { get; set; }
}
