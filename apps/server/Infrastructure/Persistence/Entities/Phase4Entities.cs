namespace BankersSeat.Server.Infrastructure.Persistence.Entities;

public sealed class UserAccountEntity
{
    public required Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHashBcrypt { get; set; }
    public required string DisplayName { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime LastAuthenticatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class RefreshTokenEntity
{
    public required Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}

public sealed class AuditLogEntity
{
    public required Guid Id { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? ActorParticipantId { get; set; }
    public required string Action { get; set; }
    public required string Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public string? Result { get; set; }
}

public sealed class SessionMetadataEntity
{
    public required Guid SessionId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public required string Label { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime LastAccessedAtUtc { get; set; }
    public int ParticipantCount { get; set; }
}

public sealed class JoinLinkEntity
{
    public required Guid Id { get; set; }
    public required Guid SessionId { get; set; }
    public required string LinkToken { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public int UseCount { get; set; }
    public bool IsRevoked { get; set; }
}

public sealed class SessionTtlPolicyEntity
{
    public required Guid SessionId { get; set; }
    public int RetentionDays { get; set; }
    public bool AutoDeleteOnComplete { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
}
