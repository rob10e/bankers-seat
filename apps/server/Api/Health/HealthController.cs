using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Health;
using Microsoft.AspNetCore.Mvc;

namespace BankersSeat.Server.Api.Health;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly IHealthService healthService;

    public HealthController(IHealthService healthService)
    {
        this.healthService = healthService;
    }

    [HttpGet("live")]
    [ProducesResponseType<HealthLiveResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthLiveResponse>> GetLiveStatus()
    {
        var response = await healthService.GetLiveStatusAsync();
        return Ok(response);
    }

    [HttpGet("ready")]
    [ProducesResponseType<HealthReadyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthReadyResponse>> GetReadyStatus(
        CancellationToken cancellationToken
    )
    {
        var response = await healthService.GetReadyStatusAsync(cancellationToken);
        var statusCode = response.Status == "healthy" ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return StatusCode(statusCode, response);
    }

    [HttpGet("templates")]
    [ProducesResponseType<HealthTemplatesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthTemplatesResponse>> GetTemplatesStatus(
        CancellationToken cancellationToken
    )
    {
        var response = await healthService.GetTemplatesStatusAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("version")]
    [ProducesResponseType<HealthVersionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthVersionResponse>> GetVersionStatus(
        CancellationToken cancellationToken
    )
    {
        var response = await healthService.GetVersionStatusAsync(cancellationToken);
        return Ok(response);
    }
}
