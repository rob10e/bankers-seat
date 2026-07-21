namespace BankersSeat.Server.Domain.Accounts;

public sealed class UserAccount
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string PasswordHashBcrypt { get; init; }
    public required string DisplayName { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime LastAuthenticatedAtUtc { get; init; }
    public bool IsDeleted { get; set; }

    public static UserAccount Create(Guid id, string email, string passwordHashBcrypt, string displayName)
    {
        return new UserAccount
        {
            Id = id,
            Email = email.ToLowerInvariant(),
            PasswordHashBcrypt = passwordHashBcrypt,
            DisplayName = displayName,
            CreatedAtUtc = DateTime.UtcNow,
            LastAuthenticatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
