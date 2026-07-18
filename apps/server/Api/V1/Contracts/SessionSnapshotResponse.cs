namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record SessionSnapshotResponse(
    Guid SessionId,
    string RoomCode,
    string Status,
    int SessionVersion,
    DateTimeOffset CreatedAtUtc,
    Guid HostParticipantId,
    TemplateSnapshotViewResponse Template,
    IReadOnlyList<ParticipantViewResponse> Participants,
    IReadOnlyList<AccountViewResponse> Accounts,
    IReadOnlyList<PlayerFieldValueViewResponse> PlayerFieldValues,
    DateTimeOffset ServerTimeUtc
);

public sealed record TemplateSnapshotViewResponse(
    Guid SnapshotId,
    string TemplateId,
    string EditionId,
    string TemplateVersion,
    int SchemaVersion,
    string ContentHash
);

public sealed record ParticipantViewResponse(
    Guid ParticipantId,
    string DisplayName,
    string Role,
    string IdentityKey,
    int JoinOrder
);

public sealed record AccountViewResponse(
    Guid AccountId,
    Guid OwnerId,
    string OwnerType,
    long Balance
);

public sealed record PlayerFieldValueViewResponse(
    Guid ParticipantId,
    string FieldId,
    string ValueJson
);
