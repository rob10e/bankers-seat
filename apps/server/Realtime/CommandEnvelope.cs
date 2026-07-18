namespace BankersSeat.Server.Realtime;

public sealed record CommandEnvelope(
    int ProtocolVersion,
    string CommandId,
    string IdempotencyKey,
    Guid SessionId,
    int ExpectedSessionVersion,
    string Type,
    object Payload
);

public sealed record EventEnvelope(
    int ProtocolVersion,
    string EventId,
    Guid SessionId,
    int SessionVersion,
    string Type,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    object Payload
);
