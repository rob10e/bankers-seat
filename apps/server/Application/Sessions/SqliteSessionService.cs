using System.Security.Cryptography;
using System.Text;
using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;
using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankersSeat.Server.Application.Sessions;

public sealed class SqliteSessionService : ISessionService
{
    private readonly BankersSeatDbContext dbContext;
    private readonly ITemplateCatalogService templateCatalogService;

    public SqliteSessionService(
        BankersSeatDbContext dbContext,
        ITemplateCatalogService templateCatalogService
    )
    {
        this.dbContext = dbContext;
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var nowUtc = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var hostParticipantId = Guid.NewGuid();
        var reconnectCredential = CreateReconnectCredential();
        var reconnectHash = HashSecret(reconnectCredential);

        var roomCode = await GenerateUniqueRoomCodeAsync(cancellationToken);
        var snapshotEntity = new TemplateSnapshotEntity
        {
            Id = templateSnapshot.Id,
            TemplateId = templateSnapshot.Identity.TemplateId,
            EditionId = templateSnapshot.Identity.EditionId,
            TemplateVersion = templateSnapshot.Identity.TemplateVersion,
            SchemaVersion = templateSnapshot.SchemaVersion,
            ContentHash = templateSnapshot.ContentHash,
            TemplateJson = templateSnapshot.TemplateJson,
            StartingPlayerBalance = templateSnapshot.StartingPlayerBalance,
            AllowPlayerOverdraft = templateSnapshot.AllowPlayerOverdraft,
            CreatedAtUtc = nowUtc
        };
        var sessionEntity = new GameSessionEntity
        {
            Id = sessionId,
            RoomCode = roomCode,
            Status = "lobby",
            HostParticipantId = hostParticipantId,
            TemplateSnapshotId = snapshotEntity.Id,
            SessionVersion = 1,
            CreatedAtUtc = nowUtc
        };
        var participantEntity = new ParticipantEntity
        {
            Id = hostParticipantId,
            SessionId = sessionId,
            DisplayName = request.HostDisplayName.Trim(),
            Role = "host",
            IdentityKey = "host",
            JoinOrder = 1,
            CreatedAtUtc = nowUtc,
            ReconnectSecretHash = reconnectHash
        };
        var accountEntity = new AccountEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            OwnerType = "participant",
            OwnerId = hostParticipantId,
            Balance = templateSnapshot.StartingPlayerBalance,
            Version = 1
        };

        dbContext.TemplateSnapshots.Add(snapshotEntity);
        dbContext.GameSessions.Add(sessionEntity);
        dbContext.Participants.Add(participantEntity);
        dbContext.Accounts.Add(accountEntity);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = await BuildSnapshotAsync(sessionEntity.Id, cancellationToken);
        return new CreateSessionResponse(
            sessionEntity.Id,
            sessionEntity.RoomCode,
            hostParticipantId,
            reconnectCredential,
            snapshot,
            BuildConnectionInfo()
        );
    }

    public async Task<JoinSessionResponse> JoinSessionAsync(
        JoinSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        ValidateJoinRequest(request);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var normalizedRoomCode = request.RoomCode.Trim().ToUpperInvariant();
        var sessionEntity = await dbContext.GameSessions
            .SingleOrDefaultAsync(session => session.RoomCode == normalizedRoomCode, cancellationToken);
        if (sessionEntity is null)
        {
            throw new InvalidOperationException("session-not-found");
        }

        var templateSnapshot = await dbContext.TemplateSnapshots
            .SingleAsync(snapshot => snapshot.Id == sessionEntity.TemplateSnapshotId, cancellationToken);
        var joinOrder = await dbContext.Participants
            .Where(participant => participant.SessionId == sessionEntity.Id)
            .CountAsync(cancellationToken);

        var reconnectCredential = CreateReconnectCredential();
        var participant = new ParticipantEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionEntity.Id,
            DisplayName = request.DisplayName.Trim(),
            Role = "player",
            IdentityKey = string.IsNullOrWhiteSpace(request.IdentityKey)
                ? "player"
                : request.IdentityKey.Trim(),
            JoinOrder = joinOrder + 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ReconnectSecretHash = HashSecret(reconnectCredential)
        };
        var account = new AccountEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionEntity.Id,
            OwnerType = "participant",
            OwnerId = participant.Id,
            Balance = templateSnapshot.StartingPlayerBalance,
            Version = 1
        };

        sessionEntity.SessionVersion += 1;
        dbContext.Participants.Add(participant);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = await BuildSnapshotAsync(sessionEntity.Id, cancellationToken);
        return new JoinSessionResponse(
            sessionEntity.Id,
            participant.Id,
            reconnectCredential,
            snapshot,
            BuildConnectionInfo()
        );
    }

    public async Task<ReconnectSessionResponse> ReconnectAsync(
        Guid sessionId,
        ReconnectSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var session = await dbContext.GameSessions.SingleOrDefaultAsync(
            record => record.Id == sessionId,
            cancellationToken
        );
        if (session is null)
        {
            throw new InvalidOperationException("session-not-found");
        }

        var participant = await dbContext.Participants.SingleOrDefaultAsync(
            record => record.Id == request.ParticipantId && record.SessionId == sessionId,
            cancellationToken
        );
        if (participant is null)
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        if (!string.Equals(HashSecret(request.ReconnectCredential), participant.ReconnectSecretHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        var refreshedCredential = CreateReconnectCredential();
        participant.ReconnectSecretHash = HashSecret(refreshedCredential);
        session.SessionVersion += 1;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var snapshot = await BuildSnapshotAsync(sessionId, cancellationToken);
        return new ReconnectSessionResponse(sessionId, participant.Id, refreshedCredential, snapshot);
    }

    public async Task<SessionSnapshotResponse> GetAuthorizedSnapshotAsync(
        Guid sessionId,
        Guid participantId,
        string reconnectCredential,
        CancellationToken cancellationToken
    )
    {
        var participant = await dbContext.Participants.SingleOrDefaultAsync(
            record => record.Id == participantId && record.SessionId == sessionId,
            cancellationToken
        );
        if (participant is null)
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        if (!string.Equals(HashSecret(reconnectCredential), participant.ReconnectSecretHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("unauthorized-command");
        }

        return await BuildSnapshotAsync(sessionId, cancellationToken);
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

    private async Task<string> GenerateUniqueRoomCodeAsync(CancellationToken cancellationToken)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var buffer = new byte[6];

        while (true)
        {
            RandomNumberGenerator.Fill(buffer);
            var candidate = string.Concat(buffer.Select(value => chars[value % chars.Length]));
            var exists = await dbContext.GameSessions.AnyAsync(
                session => session.RoomCode == candidate,
                cancellationToken
            );
            if (!exists)
            {
                return candidate;
            }
        }
    }

    private async Task<SessionSnapshotResponse> BuildSnapshotAsync(
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        var session = await dbContext.GameSessions.SingleOrDefaultAsync(
            record => record.Id == sessionId,
            cancellationToken
        );
        if (session is null)
        {
            throw new InvalidOperationException("session-not-found");
        }

        var snapshot = await dbContext.TemplateSnapshots.SingleAsync(
            record => record.Id == session.TemplateSnapshotId,
            cancellationToken
        );
        var participants = await dbContext.Participants
            .Where(record => record.SessionId == sessionId)
            .OrderBy(record => record.JoinOrder)
            .ToListAsync(cancellationToken);
        var accounts = await dbContext.Accounts
            .Where(record => record.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        return new SessionSnapshotResponse(
            session.Id,
            session.RoomCode,
            session.Status,
            session.SessionVersion,
            session.CreatedAtUtc,
            session.HostParticipantId,
            new TemplateSnapshotViewResponse(
                snapshot.Id,
                snapshot.TemplateId,
                snapshot.EditionId,
                snapshot.TemplateVersion,
                snapshot.SchemaVersion,
                snapshot.ContentHash
            ),
            participants.Select(record => new ParticipantViewResponse(
                record.Id,
                record.DisplayName,
                record.Role,
                record.IdentityKey,
                record.JoinOrder
            )).ToList(),
            accounts.Select(record => new AccountViewResponse(
                record.Id,
                record.OwnerId,
                record.OwnerType,
                record.Balance
            )).ToList(),
            DateTimeOffset.UtcNow
        );
    }

    private static SessionConnectionInfoResponse BuildConnectionInfo()
    {
        return new SessionConnectionInfoResponse("/hubs/game", 1);
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
}
