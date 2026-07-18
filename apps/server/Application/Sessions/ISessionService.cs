using BankersSeat.Server.Api.V1.Contracts;

namespace BankersSeat.Server.Application.Sessions;

public interface ISessionService
{
    Task<CreateSessionResponse> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken cancellationToken
    );

    Task<JoinSessionResponse> JoinSessionAsync(
        JoinSessionRequest request,
        CancellationToken cancellationToken
    );

    Task<ReconnectSessionResponse> ReconnectAsync(
        Guid sessionId,
        ReconnectSessionRequest request,
        CancellationToken cancellationToken
    );

    Task<SessionSnapshotResponse> GetAuthorizedSnapshotAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    );

    Task<SessionLedgerResponse> GetAuthorizedLedgerPageAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        long? beforeSequence,
        int take,
        CancellationToken cancellationToken
    );

    Task<MoneyCommandResponse> TransferBetweenParticipantsAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        TransferBetweenParticipantsRequest request,
        CancellationToken cancellationToken
    );

    Task<MoneyCommandResponse> CorrectTransactionAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        CorrectTransactionRequest request,
        CancellationToken cancellationToken
    );
}
