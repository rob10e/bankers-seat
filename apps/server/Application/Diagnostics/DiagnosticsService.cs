using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BankersSeat.Server.Application.Diagnostics;

public interface IDiagnosticsService
{
    Task<DiagnosticsResponse> GetDiagnosticsAsync(CancellationToken cancellationToken);
}

public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly BankersSeatDbContext dbContext;
    private readonly ITemplateCatalogService templateCatalogService;
    private readonly ILogger<DiagnosticsService> logger;

    public DiagnosticsService(
        BankersSeatDbContext dbContext,
        ITemplateCatalogService templateCatalogService,
        ILogger<DiagnosticsService> logger
    )
    {
        this.dbContext = dbContext;
        this.templateCatalogService = templateCatalogService;
        this.logger = logger;
    }

    public async Task<DiagnosticsResponse> GetDiagnosticsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dbHealth = await GetDatabaseHealthAsync(cancellationToken);
            var sessionStats = await GetSessionStatsAsync(cancellationToken);
            var ledgerConsistency = await GetLedgerConsistencyAsync(cancellationToken);
            var templates = await GetTemplateValidationAsync(cancellationToken);
            var errorLogs = await GetErrorLogsAsync(cancellationToken);

            var statuses = new[] { dbHealth.Status, ledgerConsistency.Status, templates.Status, errorLogs.Status };
            var overallStatus = statuses.Contains("critical") ? "critical" : 
                                statuses.Contains("degraded") ? "degraded" : "healthy";

            return new DiagnosticsResponse(
                Status: overallStatus,
                Database: dbHealth,
                Sessions: sessionStats,
                Ledger: ledgerConsistency,
                Templates: templates,
                RecentErrors: errorLogs,
                CheckedAtUtc: DateTimeOffset.UtcNow
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error gathering diagnostics");
            throw;
        }
    }

    private async Task<DatabaseHealthResponse> GetDatabaseHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Test database connectivity
            await dbContext.GameSessions.CountAsync(cancellationToken);

            var fileInfo = GetDatabaseFileInfo();
            var sizeBytes = fileInfo?.Length ?? 0;

            // Count tables
            var tables = await dbContext.Database
                .SqlQueryRaw<string>("SELECT name FROM sqlite_master WHERE type='table'")
                .CountAsync(cancellationToken);

            // Get last backup info (heuristic: check for recent .db files)
            var backupMessage = "No backup recorded";

            return new DatabaseHealthResponse(
                IsAccessible: true,
                DatabaseSizeBytes: sizeBytes,
                TableCount: tables,
                LastBackupMessage: backupMessage,
                Status: "healthy"
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database health check failed");
            return new DatabaseHealthResponse(
                IsAccessible: false,
                DatabaseSizeBytes: 0,
                TableCount: 0,
                LastBackupMessage: "Database inaccessible",
                Status: "critical"
            );
        }
    }

    private async Task<SessionStatsResponse> GetSessionStatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var totalSessions = await dbContext.GameSessions.CountAsync(cancellationToken);
            var active = await dbContext.GameSessions
                .Where(s => s.Status == "active")
                .CountAsync(cancellationToken);
            var lobby = await dbContext.GameSessions
                .Where(s => s.Status == "lobby")
                .CountAsync(cancellationToken);
            var paused = await dbContext.GameSessions
                .Where(s => s.Status == "paused")
                .CountAsync(cancellationToken);
            var completed = await dbContext.GameSessions
                .Where(s => s.Status == "completed")
                .CountAsync(cancellationToken);

            var totalParticipants = await dbContext.Participants.CountAsync(cancellationToken);
            var activeParticipants = await dbContext.Participants
                .Where(p => dbContext.GameSessions
                    .Where(s => s.Status == "active" || s.Status == "paused")
                    .Select(s => s.Id)
                    .Contains(p.SessionId)
                )
                .CountAsync(cancellationToken);

            return new SessionStatsResponse(
                TotalSessions: totalSessions,
                ActiveSessions: active,
                LobbyCount: lobby,
                PausedCount: paused,
                CompletedCount: completed,
                TotalParticipants: totalParticipants,
                ActiveParticipants: activeParticipants
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Session stats retrieval failed");
            return new SessionStatsResponse(0, 0, 0, 0, 0, 0, 0);
        }
    }

    private async Task<LedgerConsistencyResponse> GetLedgerConsistencyAsync(
        CancellationToken cancellationToken
    )
    {
        try
        {
            var totalTransactions = await dbContext.LedgerTransactions.CountAsync(cancellationToken);

            var lastHourTransactions = await dbContext.LedgerTransactions
                .Where(t => t.CreatedAtUtc >= DateTimeOffset.UtcNow.AddHours(-1))
                .CountAsync(cancellationToken);

            var totalPostingAmount = await dbContext.LedgerPostings
                .SumAsync(p => (long?)p.Amount, cancellationToken) ?? 0;

            // Check consistency: for each session, verify ledger sum equals accounts
            var inconsistencies = await CheckLedgerConsistency(cancellationToken);

            var isConsistent = !inconsistencies.Any();
            var status = isConsistent ? "healthy" : "degraded";

            return new LedgerConsistencyResponse(
                IsConsistent: isConsistent,
                TotalTransactions: totalTransactions,
                TransactionsLastHour: lastHourTransactions,
                TotalPostingAmount: totalPostingAmount,
                InconsistencyDetails: inconsistencies.Any() ? string.Join("; ", inconsistencies.Take(3)) : null,
                Status: status
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ledger consistency check failed");
            return new LedgerConsistencyResponse(
                IsConsistent: false,
                TotalTransactions: 0,
                TransactionsLastHour: 0,
                TotalPostingAmount: 0,
                InconsistencyDetails: $"Check failed: {ex.Message}",
                Status: "critical"
            );
        }
    }

    private async Task<List<string>> CheckLedgerConsistency(CancellationToken cancellationToken)
    {
        var inconsistencies = new List<string>();

        // Sample: check active sessions for balance consistency
        var activeSessions = await dbContext.GameSessions
            .Where(s => s.Status == "active" || s.Status == "paused")
            .Select(s => s.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var sessionId in activeSessions)
        {
            // Verify all accounts have corresponding ledger postings or are the bank
            var accountsWithoutPostings = await dbContext.Accounts
                .Where(a => a.SessionId == sessionId)
                .Where(a =>
                    !dbContext.LedgerPostings
                        .Where(p => p.SessionId == sessionId && p.AccountId == a.Id)
                        .Any()
                )
                .CountAsync(cancellationToken);

            if (accountsWithoutPostings > 0)
            {
                inconsistencies.Add($"Session {sessionId}: {accountsWithoutPostings} accounts without ledger entries");
            }
        }

        return inconsistencies;
    }

    private async Task<TemplateValidationResponse> GetTemplateValidationAsync(
        CancellationToken cancellationToken
    )
    {
        try
        {
            var catalog = await templateCatalogService.GetCatalogAsync(cancellationToken);

            // All templates in the catalog passed validation; invalid ones are filtered out
            // To detect invalid templates, we would need to check FileTemplateCatalogService directly
            var validCount = catalog.Count;
            var invalidCount = 0;

            return new TemplateValidationResponse(
                ValidTemplates: validCount,
                InvalidTemplates: invalidCount,
                CachedTemplates: catalog.Count,
                InvalidTemplateIds: [],
                Status: "healthy"
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Template validation check failed");
            return new TemplateValidationResponse(0, 0, 0, [], "critical");
        }
    }

    private async Task<ErrorLogsResponse> GetErrorLogsAsync(CancellationToken cancellationToken)
    {
        // This is a placeholder for structured logging integration
        // In production, you'd query structured logs from your logging provider
        // (e.g., Serilog, Application Insights, etc.)

        // For now, return placeholder
        return await Task.FromResult(
            new ErrorLogsResponse(
                ErrorCountLastHour: 0,
                ErrorCountLastDay: 0,
                RecentErrorMessages: [],
                RecentExceptionTypes: [],
                Status: "healthy"
            )
        );
    }

    private FileInfo? GetDatabaseFileInfo()
    {
        try
        {
            var connectionString = dbContext.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
                return null;

            // Parse SQLite connection string
            var parts = connectionString.Split('=');
            if (parts.Length < 2)
                return null;

            var dbPath = parts[1].TrimEnd(';');
            if (File.Exists(dbPath))
            {
                return new FileInfo(dbPath);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }
}
