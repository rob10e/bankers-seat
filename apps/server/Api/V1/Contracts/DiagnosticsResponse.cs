namespace BankersSeat.Server.Api.V1.Contracts;

public sealed record DiagnosticsResponse(
    string Status,
    DatabaseHealthResponse Database,
    SessionStatsResponse Sessions,
    LedgerConsistencyResponse Ledger,
    TemplateValidationResponse Templates,
    ErrorLogsResponse RecentErrors,
    DateTimeOffset CheckedAtUtc
);

public sealed record DatabaseHealthResponse(
    bool IsAccessible,
    long DatabaseSizeBytes,
    int TableCount,
    string? LastBackupMessage,
    string Status
);

public sealed record SessionStatsResponse(
    int TotalSessions,
    int ActiveSessions,
    int LobbyCount,
    int PausedCount,
    int CompletedCount,
    int TotalParticipants,
    int ActiveParticipants
);

public sealed record LedgerConsistencyResponse(
    bool IsConsistent,
    int TotalTransactions,
    int TransactionsLastHour,
    long TotalPostingAmount,
    string? InconsistencyDetails,
    string Status
);

public sealed record TemplateValidationResponse(
    int ValidTemplates,
    int InvalidTemplates,
    int CachedTemplates,
    string[] InvalidTemplateIds,
    string Status
);

public sealed record ErrorLogsResponse(
    int ErrorCountLastHour,
    int ErrorCountLastDay,
    string[] RecentErrorMessages,
    string[] RecentExceptionTypes,
    string Status
);
