using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Domain.Accounts;
using BankersSeat.Server.Domain.Participants;
using BankersSeat.Server.Domain.Sessions;

namespace BankersSeat.Server.Application.Sessions;

public sealed class InMemorySessionService : ISessionService
{
    private readonly ConcurrentDictionary<Guid, SessionState> sessionsById = new();
    private readonly ConcurrentDictionary<string, Guid> sessionIdByRoomCode = new(
        StringComparer.OrdinalIgnoreCase
    );
    private readonly ITemplateCatalogService templateCatalogService;
    private readonly Lock mutationLock = new();

    public InMemorySessionService(ITemplateCatalogService templateCatalogService)
    {
        this.templateCatalogService = templateCatalogService;
    }

    public async Task<CreateSessionResponse> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateCreateRequest(request);

        var templateSnapshot = await templateCatalogService.GetTemplateSnapshotAsync(
            request.TemplateId,
            request.EditionId,
            request.TemplateVersion,
            cancellationToken
        );

        if (templateSnapshot is null)
        {
            throw new InvalidOperationException("template-not-found");
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var roomCode = GenerateRoomCode();
        var hostParticipantId = Guid.NewGuid();
        var hostReconnectCredential = CreateReconnectCredential();
        var host = new Participant(
            hostParticipantId,
            sessionId,
            request.HostDisplayName.Trim(),
            ParticipantRole.Host,
            "host",
            1,
            nowUtc,
            HashSecret(hostReconnectCredential)
        );
        var hostAccount = new Account(
            Guid.NewGuid(),
            sessionId,
            "participant",
            hostParticipantId,
            templateSnapshot.StartingPlayerBalance,
            1
        );
        var gameSession = new GameSession(
            sessionId,
            roomCode,
            SessionStatus.Lobby,
            hostParticipantId,
            1,
            nowUtc,
            templateSnapshot,
            [host],
            [hostAccount]
        );

        lock (mutationLock)
        {
            sessionsById[sessionId] = new SessionState(gameSession);
            sessionIdByRoomCode[roomCode] = sessionId;
        }

        return new CreateSessionResponse(
            sessionId,
            roomCode,
            hostParticipantId,
            hostReconnectCredential,
            BuildSnapshot(gameSession),
            BuildConnectionInfo()
        );
    }

    public Task<JoinSessionResponse> JoinSessionAsync(
        JoinSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        ValidateJoinRequest(request);
        if (!sessionIdByRoomCode.TryGetValue(request.RoomCode.Trim(), out var sessionId))
        {
            throw new InvalidOperationException("session-not-found");
        }

        if (!sessionsById.TryGetValue(sessionId, out var state))
        {
            throw new InvalidOperationException("session-not-found");
        }

        var reconnectCredential = CreateReconnectCredential();
        var nowUtc = DateTimeOffset.UtcNow;

        lock (mutationLock)
        {
            var session = state.Session;
            var participant = new Participant(
                Guid.NewGuid(),
                session.Id,
                request.DisplayName.Trim(),
                ParticipantRole.Player,
                string.IsNullOrWhiteSpace(request.IdentityKey) ? "player" : request.IdentityKey.Trim(),
                session.Participants.Count + 1,
                nowUtc,
                HashSecret(reconnectCredential)
            );
            var account = new Account(
                Guid.NewGuid(),
                session.Id,
                "participant",
                participant.Id,
                session.TemplateSnapshot.StartingPlayerBalance,
                1
            );
            var updatedSession = session with
            {
                Participants = [.. session.Participants, participant],
                Accounts = [.. session.Accounts, account],
                SessionVersion = session.SessionVersion + 1
            };
            state.Session = updatedSession;

            return Task.FromResult(
                new JoinSessionResponse(
                    updatedSession.Id,
                    participant.Id,
                    reconnectCredential,
                    BuildSnapshot(updatedSession),
                    BuildConnectionInfo()
                )
            );
        }
    }

    public Task<ReconnectSessionResponse> ReconnectAsync(
        Guid sessionId,
        ReconnectSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
        var session = GetAuthorizedSession(sessionId, request.ParticipantId, request.ReconnectCredential);
        var refreshedCredential = CreateReconnectCredential();

        lock (mutationLock)
        {
            var participant = session.Participants.Single(p => p.Id == request.ParticipantId);
            var refreshedParticipant = participant with
            {
                ReconnectSecretHash = HashSecret(refreshedCredential)
            };
            var updatedParticipants = session.Participants
                .Select(p => p.Id == refreshedParticipant.Id ? refreshedParticipant : p)
                .ToList();
            var updatedSession = session with
            {
                Participants = updatedParticipants,
                SessionVersion = session.SessionVersion + 1
            };

            sessionsById[sessionId].Session = updatedSession;
            return Task.FromResult(
                new ReconnectSessionResponse(
                    sessionId,
                    request.ParticipantId,
                    refreshedCredential,
                    BuildSnapshot(updatedSession)
                )
            );
        }
    }

    public Task<SessionSnapshotResponse> GetAuthorizedSnapshotAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;
        var session = GetAuthorizedSession(sessionId, participantId, reconnectCredential);
        return Task.FromResult(BuildSnapshot(session));
    }

    public Task<SessionLedgerResponse> GetAuthorizedLedgerPageAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        long? beforeSequence,
        int take,
        CancellationToken cancellationToken
    )
    {
        _ = sessionId;
        _ = participantId;
        _ = reconnectCredential;
        _ = beforeSequence;
        _ = take;
        _ = cancellationToken;
        throw new InvalidOperationException("not-supported");
    }

    public Task<SessionExportResponse> GetAuthorizedSessionExportAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    )
    {
        _ = sessionId;
        _ = participantId;
        _ = reconnectCredential;
        _ = cancellationToken;
        throw new InvalidOperationException("not-supported");
    }

    public Task<MoneyCommandResponse> TransferBetweenParticipantsAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        TransferBetweenParticipantsRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = sessionId;
        _ = actorParticipantId;
        _ = reconnectCredential;
        _ = request;
        _ = cancellationToken;
        throw new InvalidOperationException("not-supported");
    }

    public Task<MoneyCommandResponse> BankToParticipantAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        BankToParticipantRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = sessionId;
        _ = actorParticipantId;
        _ = reconnectCredential;
        _ = request;
        _ = cancellationToken;
        throw new InvalidOperationException("not-supported");
    }

    public Task<MoneyCommandResponse> ParticipantToBankAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        ParticipantToBankRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = sessionId;
        _ = actorParticipantId;
        _ = reconnectCredential;
        _ = request;
        _ = cancellationToken;
        throw new InvalidOperationException("not-supported");
    }

    public Task<MoneyCommandResponse> ExecuteTemplateActionAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        string actionId,
        ExecuteTemplateActionRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = sessionId;
        _ = actorParticipantId;
        _ = reconnectCredential;
        _ = actionId;
        _ = request;
        _ = cancellationToken;
        throw new InvalidOperationException("not-supported");
    }

    public Task<MoneyCommandResponse> CorrectTransactionAsync(
        Guid sessionId,
        Guid actorParticipantId,
        string reconnectCredential,
        CorrectTransactionRequest request,
        CancellationToken cancellationToken
    )
    {
        _ = sessionId;
        _ = actorParticipantId;
        _ = reconnectCredential;
        _ = request;
        _ = cancellationToken;
        throw new InvalidOperationException("not-supported");
    }

    private GameSession GetAuthorizedSession(Guid sessionId, Guid participantId, string reconnectCredential)
    {
        if (!sessionsById.TryGetValue(sessionId, out var state))
        {
            throw new InvalidOperationException("session-not-found");
        }

        var participant = state.Session.Participants.SingleOrDefault(p => p.Id == participantId);
        if (participant is null)
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        var credentialHash = HashSecret(reconnectCredential);
        if (!string.Equals(credentialHash, participant.ReconnectSecretHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        return state.Session;
    }

    private static void ValidateCreateRequest(CreateSessionRequest request)
    {
        if (
            string.IsNullOrWhiteSpace(request.TemplateId)
            || string.IsNullOrWhiteSpace(request.EditionId)
            || string.IsNullOrWhiteSpace(request.TemplateVersion)
            || string.IsNullOrWhiteSpace(request.HostDisplayName)
        )
        {
            throw new InvalidOperationException("invalid-request");
        }
    }

    private static void ValidateJoinRequest(JoinSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RoomCode) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new InvalidOperationException("invalid-request");
        }
    }

    private static SessionConnectionInfoResponse BuildConnectionInfo()
    {
        return new SessionConnectionInfoResponse("/hubs/game", 1);
    }

    private static SessionSnapshotResponse BuildSnapshot(GameSession session)
    {
        return new SessionSnapshotResponse(
            session.Id,
            session.RoomCode,
            session.Status.ToString().ToLowerInvariant(),
            session.SessionVersion,
            session.CreatedAtUtc,
            session.HostParticipantId,
            new TemplateSnapshotViewResponse(
                session.TemplateSnapshot.Id,
                session.TemplateSnapshot.Identity.TemplateId,
                session.TemplateSnapshot.Identity.EditionId,
                session.TemplateSnapshot.Identity.TemplateVersion,
                session.TemplateSnapshot.SchemaVersion,
                session.TemplateSnapshot.ContentHash
            ),
            session.Participants
                .OrderBy(p => p.JoinOrder)
                .Select(p => new ParticipantViewResponse(
                    p.Id,
                    p.DisplayName,
                    p.Role.ToString().ToLowerInvariant(),
                    p.IdentityKey,
                    p.JoinOrder
                ))
                .ToList(),
            session.Accounts
                .Select(account => new AccountViewResponse(
                    account.Id,
                    account.OwnerId,
                    account.OwnerType,
                    account.Balance
                ))
                .ToList(),
            [],
            DateTimeOffset.UtcNow
        );
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> buffer = stackalloc byte[6];
        var roomCode = string.Empty;

        do
        {
            RandomNumberGenerator.Fill(buffer);
            roomCode = string.Concat(buffer.ToArray().Select(value => chars[value % chars.Length]));
        } while (sessionIdByRoomCode.ContainsKey(roomCode));

        return roomCode;
    }

    private static string CreateReconnectCredential()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashSecret(string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class SessionState
    {
        public SessionState(GameSession session)
        {
            Session = session;
        }

        public GameSession Session { get; set; }
    }
}
