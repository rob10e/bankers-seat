namespace BankersSeat.Server.Domain.Accounts;

public sealed record Account(
    Guid Id,
    Guid SessionId,
    string OwnerType,
    Guid OwnerId,
    long Balance,
    int Version
);
