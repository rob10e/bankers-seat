using BankersSeat.Server.Application.Authentication;
using BankersSeat.Server.Api.V1.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BankersSeat.Server.Api.V1;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(IAuthenticationService authService, ILogger<AuthenticationController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthenticationResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Email, password, and displayName are required");

        if (request.Password.Length < 8)
            return BadRequest("Password must be at least 8 characters");

        var result = await _authService.RegisterAsync(request.Email, request.Password, request.DisplayName, ct);

        if (!result.IsSuccess)
            return BadRequest(new AuthenticationResponse { Success = false, Error = result.Error });

        _logger.LogInformation("User registered: {Email}", request.Email);

        return Ok(new AuthenticationResponse
        {
            Success = true,
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _authService.LoginAsync(request.Email, request.Password, ct);

        if (!result.IsSuccess)
            return Unauthorized(new AuthenticationResponse { Success = false, Error = result.Error });

        _logger.LogInformation("User authenticated: {Email}", request.Email);

        return Ok(new AuthenticationResponse
        {
            Success = true,
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken
        });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken, ct);

        if (!result.IsSuccess)
            return Unauthorized(new AuthenticationResponse { Success = false, Error = result.Error });

        return Ok(new AuthenticationResponse
        {
            Success = true,
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken
        });
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserProfileResponse> GetProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var emailClaim = User.FindFirst(ClaimTypes.Email)?.Value;
        var displayNameClaim = User.FindFirst("displayName")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        return Ok(new UserProfileResponse
        {
            Id = userId,
            Email = emailClaim ?? "",
            DisplayName = displayNameClaim ?? "",
            CreatedAtUtc = DateTime.UtcNow,
            LastAuthenticatedAtUtc = DateTime.UtcNow
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public ActionResult Logout()
    {
        _logger.LogInformation("User logged out");
        return Ok();
    }
}
