using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace BankersSeat.Server.Api.V1;

[ApiController]
[Route("api/v1/sessions")]
public sealed class SessionsController : ControllerBase
{
    private const string ParticipantIdHeader = "X-Participant-Id";
    private const string ReconnectCredentialHeader = "X-Reconnect-Credential";
    private readonly ISessionService sessionService;

    public SessionsController(ISessionService sessionService)
    {
        this.sessionService = sessionService;
    }

    [HttpPost]
    [ProducesResponseType<CreateSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateSessionResponse>> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await sessionService.CreateSessionAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return ToProblemDetails(exception);
        }
    }

    [HttpPost("join")]
    [ProducesResponseType<JoinSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JoinSessionResponse>> JoinSession(
        [FromBody] JoinSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await sessionService.JoinSessionAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return ToProblemDetails(exception);
        }
    }

    [HttpPost("{sessionId:guid}/reconnect")]
    [ProducesResponseType<ReconnectSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReconnectSessionResponse>> ReconnectSession(
        [FromRoute] Guid sessionId,
        [FromBody] ReconnectSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await sessionService.ReconnectAsync(sessionId, request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return ToProblemDetails(exception);
        }
    }

    [HttpGet("{sessionId:guid}/snapshot")]
    [ProducesResponseType<SessionSnapshotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionSnapshotResponse>> GetSnapshot(
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        if (
            !Request.Headers.TryGetValue(ParticipantIdHeader, out var participantIdRaw)
            || !Guid.TryParse(participantIdRaw.SingleOrDefault(), out var participantId)
            || !Request.Headers.TryGetValue(ReconnectCredentialHeader, out var reconnectCredentialRaw)
            || string.IsNullOrWhiteSpace(reconnectCredentialRaw.SingleOrDefault())
        )
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request.",
                detail: "Snapshot requests must include participant and reconnect credentials.",
                extensions: new Dictionary<string, object?> { ["code"] = "invalid-request" }
            );
        }

        var reconnectCredential = reconnectCredentialRaw.Single()!;
        try
        {
            var snapshot = await sessionService.GetAuthorizedSnapshotAsync(
                sessionId,
                participantId,
                reconnectCredential,
                cancellationToken
            );
            return Ok(snapshot);
        }
        catch (InvalidOperationException exception)
        {
            return ToProblemDetails(exception);
        }
    }

    private ActionResult ToProblemDetails(InvalidOperationException exception)
    {
        var code = exception.Message;
        var (statusCode, title) = code switch
        {
            "template-not-found" => (StatusCodes.Status404NotFound, "Template not found."),
            "session-not-found" => (StatusCodes.Status404NotFound, "Session not found."),
            "unauthorized-command" => (StatusCodes.Status401Unauthorized, "Unauthorized command."),
            _ => (StatusCodes.Status400BadRequest, "Invalid request.")
        };

        return Problem(
            statusCode: statusCode,
            title: title,
            detail: $"Request rejected with code '{code}'.",
            extensions: new Dictionary<string, object?> { ["code"] = code }
        );
    }
}
