namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record SessionLifecycleCommandRequest(
    int ExpectedSessionVersion,
    string IdempotencyKey
);
