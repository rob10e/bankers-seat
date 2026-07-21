namespace BankersSeat.Server.Api.V1.Contracts;

public sealed class RegisterRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string DisplayName { get; set; }
}

public sealed class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public sealed class RefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}

public sealed class AuthenticationResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Error { get; set; }
}

public sealed class UserProfileResponse
{
    public required Guid Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime LastAuthenticatedAtUtc { get; set; }
}

public sealed class SavedSessionInfo
{
    public required Guid SessionId { get; set; }
    public required string RoomCode { get; set; }
    public required string Label { get; set; }
    public required string TemplateName { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime LastAccessedAtUtc { get; set; }
    public int ParticipantCount { get; set; }
}

public sealed class SavedSessionsResponse
{
    public required IEnumerable<SavedSessionInfo> Sessions { get; set; }
    public int Total { get; set; }
}

public sealed class CreateJoinLinkRequest
{
    public int ExpirationMinutes { get; set; } = 60;
}

public sealed class JoinLinkResponse
{
    public required Guid Id { get; set; }
    public required string LinkToken { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
    public required string JoinUrl { get; set; }
}

public sealed class RoomCodeResponse
{
    public required string RoomCode { get; set; }
}

public sealed class AuditLogResponse
{
    public required Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public required string Action { get; set; }
    public required string Details { get; set; }
    public string? IpAddress { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public string? Result { get; set; }
}

public sealed class AdminSessionInfoResponse
{
    public required Guid SessionId { get; set; }
    public required string RoomCode { get; set; }
    public required string Status { get; set; }
    public Guid? OwnerUserId { get; set; }
    public int ParticipantCount { get; set; }
    public required DateTimeOffset CreatedAtUtc { get; set; }
    public required DateTimeOffset LastAccessedAtUtc { get; set; }
    public int TransactionCount { get; set; }
}
