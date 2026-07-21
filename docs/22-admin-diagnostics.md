# Administrator Diagnostics Guide

This guide covers the administrative diagnostics tools available in Banker's Seat for self-hosted deployments.

## Overview

The diagnostics system provides real-time visibility into:

- **Database Health** — Connectivity, size, table count, backup status
- **Session Statistics** — Active games, players, lobby status
- **Ledger Consistency** — Transaction count, posting validation, anomaly detection
- **Template Validation** — Valid/invalid template count, catalog status
- **Error Monitoring** — Recent errors and exceptions (structured logging integration)

## Accessing Diagnostics

### Web Console (Recommended)

Open your browser and navigate to:

```
http://your-server:8080/admin/diagnostics.html
```

The console provides a real-time dashboard with:
- Color-coded status indicators (green/yellow/red)
- Auto-refresh every 30 seconds (toggle with button)
- Session activity overview
- Quick detection of issues

**Access:** No authentication required in development. In production, apply authentication per your deployment policy.

### API Endpoint

For programmatic access or monitoring system integration:

```bash
curl http://localhost:8080/api/v1/admin/diagnostics
```

**Response (HTTP 200):**
```json
{
  "status": "healthy",
  "database": {
    "isAccessible": true,
    "databaseSizeBytes": 1048576,
    "tableCount": 13,
    "lastBackupMessage": "No backup recorded",
    "status": "healthy"
  },
  "sessions": {
    "totalSessions": 42,
    "activeSessions": 3,
    "lobbyCount": 1,
    "pausedCount": 0,
    "completedCount": 38,
    "totalParticipants": 187,
    "activeParticipants": 9
  },
  "ledger": {
    "isConsistent": true,
    "totalTransactions": 512,
    "transactionsLastHour": 8,
    "totalPostingAmount": 0,
    "inconsistencyDetails": null,
    "status": "healthy"
  },
  "templates": {
    "validTemplates": 5,
    "invalidTemplates": 0,
    "cachedTemplates": 5,
    "invalidTemplateIds": [],
    "status": "healthy"
  },
  "recentErrors": {
    "errorCountLastHour": 0,
    "errorCountLastDay": 0,
    "recentErrorMessages": [],
    "recentExceptionTypes": [],
    "status": "healthy"
  },
  "checkedAtUtc": "2026-07-21T10:00:00Z"
}
```

## Understanding Status Codes

Each diagnostic section reports a status:

### 🟢 Healthy
All checks passed. System operating normally.

```json
{
  "status": "healthy"
}
```

### 🟡 Degraded
Some checks failed but system continues operating. Investigate the specific section for details.

Examples:
- One template failed validation but others are valid
- Ledger consistency check found anomalies in one session but not others
- Database is accessible but recent backups not detected

```json
{
  "status": "degraded",
  "inconsistencyDetails": "Session abc123: 2 accounts missing ledger entries"
}
```

### 🔴 Critical
System component is unavailable or critical check failed.

Examples:
- Database is not accessible
- Ledger consistency check failed entirely
- Fatal error retrieving template catalog

```json
{
  "status": "critical",
  "inconsistencyDetails": "Database connection failed: connection timeout"
}
```

## Diagnostic Sections

### Database Health

Monitors SQLite database status and operations.

**Checks:**
- Database file is accessible and readable
- Database size (helps identify bloat or growth issues)
- Schema table count (verifies migrations)
- Last backup timestamp (informs backup automation status)

**Action if degraded/critical:**
1. Verify disk space: `df -h /data`
2. Check database permissions: `ls -la /data/bankers-seat.db`
3. Review container logs: `docker compose logs bankers-seat`
4. See backup/restore guide for recovery procedures

### Session Statistics

Counts active and completed sessions with breakdown by status.

**Tracked:**
- **Total Sessions** — Lifetime count (never decreases)
- **Active Sessions** — Games in progress
- **Lobby Count** — Waiting to start
- **Paused Count** — Temporarily paused
- **Completed Count** — Finished games
- **Active Participants** — Players in active/paused sessions

**Action if unexpected:**
- Compare with historical data (use API polling)
- Check for orphaned sessions: sessions stuck in lobby/paused for days
- Correlate with server restart times (should drop to near zero)

### Ledger Consistency

Validates financial transaction integrity.

**Checks:**
- Each account has corresponding ledger entries
- No orphaned postings without transactions
- Aggregate balance calculations match stored accounts

**Action if inconsistent:**
1. Query specific sessions: `docker compose exec bankers-seat sqlite3 /data/bankers-seat.db "SELECT session_id, COUNT(*) FROM ledger_postings GROUP BY session_id WHERE session_id = 'SESSION_ID'"`
2. Inspect accounts table: `SELECT * FROM accounts WHERE session_id = 'SESSION_ID'`
3. Review application logs for recent errors during the problem period
4. If data is corrupted, restore from backup (see backup guide)

### Template Validation

Reports template catalog health.

**Tracked:**
- **Valid Templates** — Pass schema and semantic validation
- **Invalid Templates** — Schema violations or missing required fields
- **Cached Templates** — Total in catalog
- **Invalid Template IDs** — Specific failures

**Action if invalid templates detected:**
1. Inspect template file: `cat /templates/samples/template-name/template.json`
2. Run validation CLI: `pnpm templates:validate`
3. Fix JSON errors (syntax, missing fields)
4. Rescan catalog: `curl -X POST http://localhost:8080/api/v1/admin/templates/rescan`
5. Verify: `curl http://localhost:8080/api/v1/admin/diagnostics | jq .templates`

### Recent Errors

Tracks errors and exceptions (placeholder for structured logging integration).

**Future implementation:**
This section will integrate with structured logging (Serilog, Application Insights, etc.) to surface recent errors with context, helping diagnose issues without manual log review.

**Current behavior:**
Returns 0 errors. Integrate your logging provider for full capability.

## Monitoring and Alerting

### Using with Monitoring Tools

Query the API endpoint periodically and alert on state changes:

**Prometheus/Grafana example:**

```yaml
- job_name: 'bankers_seat'
  static_configs:
    - targets: ['localhost:8080']
  metrics_path: '/api/v1/admin/diagnostics'
```

Parse JSON and extract metrics:
- `database.status != "healthy"` → Page operator (critical)
- `sessions.activeSessions > threshold` → Notify (informational)
- `ledger.isConsistent == false` → Alert (high priority)

### Recommended Alert Thresholds

| Condition | Priority | Action |
|-----------|----------|--------|
| `database.status = "critical"` | 🔴 Critical | Page on-call immediately |
| `ledger.isConsistent = false` | 🔴 Critical | Page on-call immediately |
| `templates.invalidTemplates > 0` | 🟡 Warning | Email alert, review at next maintenance |
| `sessions.activeSessions > 50` | 🔵 Info | Log for capacity planning |
| No diagnostics response within 30s | 🔴 Critical | Server down/hung |

## Troubleshooting

### "Database is locked" during diagnostics check

**Cause:** Concurrent access during backup/restore or high write load

**Solution:**
1. Wait 30 seconds and retry (locks are short-lived)
2. If persistent, restart container: `docker compose restart bankers-seat`
3. Check for long-running backup: `docker compose exec bankers-seat lsof /data/bankers-seat.db`

### "Ledger inconsistencies detected" but sessions appear normal

**Cause:** Rare race condition during concurrent writes (migrated from older versions)

**Solution:**
1. Identify the session: Review `inconsistencyDetails` in response
2. Inspect session: `curl http://localhost:8080/api/v1/sessions/SESSION_ID/snapshot`
3. Check ledger: `curl http://localhost:8080/api/v1/sessions/SESSION_ID/ledger`
4. If data is mismatched, restore from backup before the anomaly
5. Report to support with detailed diagnostics output

### "Invalid templates" keep appearing after rescan

**Cause:** Template file has persistent validation errors

**Solution:**
1. Get template ID from diagnostics response
2. View template: `cat /templates/samples/TEMPLATE_ID/template.json | jq`
3. Run validation: `pnpm templates:validate`
4. Fix errors (common issues: missing required fields, invalid JSON)
5. Rescan: `curl -X POST http://localhost:8080/api/v1/admin/templates/rescan`

### Diagnostics API returns 404 or 500

**Cause:** Admin endpoint not registered or service failure

**Solution:**
1. Verify container is running: `docker compose ps`
2. Check logs: `docker compose logs bankers-seat | grep -i diagnostic`
3. Verify dependency injection: Check `Program.cs` includes `AddScoped<IDiagnosticsService, DiagnosticsService>()`
4. Restart container: `docker compose restart bankers-seat`

## Integration with Structured Logging

To enable error tracking in the `recentErrors` section:

1. **Add Serilog** (or another structured logger) to `Program.cs`:
   ```csharp
   builder.Host.UseSerilog();
   ```

2. **Implement error log retrieval** in `DiagnosticsService.GetErrorLogsAsync()`
   - Query Serilog sink or centralized logging provider
   - Return errors from last hour and day

3. **Configure retention** per your logging provider

## Best Practices

### Daily Operator Checklist

At the start of each day:

1. **Access diagnostics console**: Open `/admin/diagnostics.html`
2. **Review session activity**: Confirm expected player count
3. **Check ledger**: Verify no consistency issues
4. **Validate templates**: Confirm no recent failures
5. **Backup status**: Confirm last backup was recent (see backup guide)

### Before Each Game Session

1. Open diagnostics console
2. Verify `database.status = "healthy"`
3. Confirm `ledger.isConsistent = true`
4. Check that template for your game shows `status = "valid"`

### After Deployment or Updates

1. Run full diagnostics check
2. Verify all sections report healthy/valid status
3. Monitor closely for first 10 minutes
4. Alert development team of any degraded status

### Regular Maintenance

- **Weekly**: Check database size growth (use `database.databaseSizeBytes`)
- **Monthly**: Validate backup restoration with restore script (see backup guide)
- **Quarterly**: Clean up completed sessions older than 90 days (manual archival/deletion)

## Support and Escalation

### Gathering Diagnostics for Support

When contacting support, include:

1. **Full diagnostics output**:
   ```bash
   curl http://localhost:8080/api/v1/admin/diagnostics | jq
   ```

2. **Recent logs** (last 100 lines):
   ```bash
   docker compose logs bankers-seat | tail -100
   ```

3. **Database size and backup status**:
   ```bash
   docker compose exec bankers-seat du -sh /data/*
   ```

4. **Session and ledger sample** (if applicable):
   ```bash
   curl 'http://localhost:8080/api/v1/sessions/SESSION_ID/snapshot' \
     -H "X-Actor-Id: PARTICIPANT_ID" \
     -H "X-Actor-Credential: CREDENTIAL"
   ```

5. **Reproduction steps** if issue is reproducible

## See Also

- `docs/14-deployment-and-observability.md` — Health checks and monitoring strategy
- `docs/21-backup-and-restore.md` — Backup and disaster recovery
- `README.md` — Docker operations quick start
