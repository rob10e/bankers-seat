using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using BankersSeat.Server.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;

namespace BankersSeat.Server.Application.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationResult> RegisterAsync(string email, string password, string displayName, CancellationToken ct);
    Task<AuthenticationResult> LoginAsync(string email, string password, CancellationToken ct);
    Task<RefreshTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct);
    Task RevokeRefreshTokenAsync(Guid userId, string tokenHash, CancellationToken ct);
    Task<Guid?> ValidateAndGetUserIdFromTokenAsync(string token);
}

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly BankersSeatDbContext _db;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IConfiguration _config;
    private const int BcryptWorkFactor = 12;

    public AuthenticationService(BankersSeatDbContext db, ILogger<AuthenticationService> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _config = config;
    }

    public async Task<AuthenticationResult> RegisterAsync(string email, string password, string displayName, CancellationToken ct)
    {
        email = email.ToLowerInvariant().Trim();

        var existing = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);
        if (existing != null)
        {
            return AuthenticationResult.Failure("Email already registered");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, BcryptWorkFactor);
        var userId = Guid.NewGuid();
        var user = new UserAccountEntity
        {
            Id = userId,
            Email = email,
            PasswordHashBcrypt = passwordHash,
            DisplayName = displayName.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            LastAuthenticatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync(ct);

        var accessToken = GenerateAccessToken(userId, email, displayName);
        var refreshToken = GenerateRefreshToken(userId);

        _logger.LogInformation("User registered: {Email}, UserId: {UserId}", email, userId);

        return AuthenticationResult.Success(accessToken, refreshToken);
    }

    public async Task<AuthenticationResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        email = email.ToLowerInvariant().Trim();

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHashBcrypt))
        {
            return AuthenticationResult.Failure("Invalid email or password");
        }

        user.LastAuthenticatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var accessToken = GenerateAccessToken(user.Id, user.Email, user.DisplayName);
        var refreshToken = GenerateRefreshToken(user.Id);

        _logger.LogInformation("User authenticated: {Email}, UserId: {UserId}", email, user.Id);

        return AuthenticationResult.Success(accessToken, refreshToken);
    }

    public async Task<RefreshTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var tokenHash = HashToken(refreshToken);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.IsRevoked, ct);

        if (token == null || token.ExpiresAtUtc < DateTime.UtcNow)
        {
            return RefreshTokenResult.Failure("Invalid or expired refresh token");
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == token.UserId && !u.IsDeleted, ct);
        if (user == null)
        {
            return RefreshTokenResult.Failure("User not found");
        }

        var newAccessToken = GenerateAccessToken(user.Id, user.Email, user.DisplayName);
        var newRefreshToken = GenerateRefreshToken(user.Id);

        _logger.LogInformation("Token refreshed for UserId: {UserId}", user.Id);

        return RefreshTokenResult.Success(newAccessToken, newRefreshToken);
    }

    public async Task RevokeRefreshTokenAsync(Guid userId, string tokenHash, CancellationToken ct)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == userId && t.TokenHash == tokenHash, ct);
        if (token != null)
        {
            token.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<Guid?> ValidateAndGetUserIdFromTokenAsync(string token)
    {
        try
        {
            var key = _config["Jwt:SigningKey"];
            if (string.IsNullOrEmpty(key))
                return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }

    private string GenerateAccessToken(Guid userId, string email, string displayName)
    {
        var key = _config["Jwt:SigningKey"];
        var issuer = _config["Jwt:Issuer"] ?? "bankers-seat";
        var durationMinutes = int.TryParse(_config["Jwt:AccessTokenExpirationMinutes"], out var minutes) ? minutes : 15;

        var tokenHandler = new JwtSecurityTokenHandler();
        var keyBytes = Encoding.ASCII.GetBytes(key ?? throw new InvalidOperationException("Jwt:SigningKey not configured"));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim("displayName", displayName)
            }),
            Expires = DateTime.UtcNow.AddMinutes(durationMinutes),
            Issuer = issuer,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string GenerateRefreshToken(Guid userId)
    {
        var refreshTokenBytes = new byte[64];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(refreshTokenBytes);
        }

        var refreshToken = Convert.ToBase64String(refreshTokenBytes);
        var tokenHash = HashToken(refreshToken);
        var expirationDays = int.TryParse(_config["Jwt:RefreshTokenExpirationDays"], out var days) ? days : 7;

        var tokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(expirationDays),
            CreatedAtUtc = DateTime.UtcNow,
            IsRevoked = false
        };

        _db.RefreshTokens.Add(tokenEntity);
        _db.SaveChangesAsync().GetAwaiter().GetResult();

        return refreshToken;
    }

    private static string HashToken(string token)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}

public sealed class AuthenticationResult
{
    public bool IsSuccess { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? Error { get; private set; }

    private AuthenticationResult(bool isSuccess, string? accessToken, string? refreshToken, string? error)
    {
        IsSuccess = isSuccess;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        Error = error;
    }

    public static AuthenticationResult Success(string accessToken, string refreshToken)
        => new(true, accessToken, refreshToken, null);

    public static AuthenticationResult Failure(string error)
        => new(false, null, null, error);
}

public sealed class RefreshTokenResult
{
    public bool IsSuccess { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? Error { get; private set; }

    private RefreshTokenResult(bool isSuccess, string? accessToken, string? refreshToken, string? error)
    {
        IsSuccess = isSuccess;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        Error = error;
    }

    public static RefreshTokenResult Success(string accessToken, string refreshToken)
        => new(true, accessToken, refreshToken, null);

    public static RefreshTokenResult Failure(string error)
        => new(false, null, null, error);
}
