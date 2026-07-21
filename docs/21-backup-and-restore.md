# Backup and Restore Guide

This guide covers backup and restore procedures for Banker's Seat self-hosted deployments using Docker Compose with SQLite persistence.

## Quick Start

### Backup (Recommended)
```bash
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db ".backup '/data/backup-$(date +%Y%m%d-%H%M%S).db'"
```

### Restore
```bash
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db ".restore '/data/backup-YYYYMMDD-HHMMSS.db'"
```

## Deployment Architecture

The default `docker-compose.yml` mounts two persistent volumes:

```yaml
volumes:
  - bankers-seat-data:/data        # SQLite database + backups
  - ./templates:/templates:ro       # Template files (read-only)
```

- **`/data`** — SQLite database (`bankers-seat.db`) and manual/automated backups
- **`/templates`** — Mounted template directory (games and editions)

## Backup Strategies

### Strategy 1: Manual Backup (Immediate)

For one-off or ad-hoc backups before maintenance or critical operations:

```bash
# Copy backup via the container
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db \
  ".backup '/data/backup-pre-maintenance-$(date +%Y%m%d-%H%M%S).db'"

# Verify backup exists
docker compose exec bankers-seat ls -lh /data/*.db
```

**Pros**: Simple, immediate, no setup
**Cons**: Manual; easy to forget

### Strategy 2: Automated Daily Backups

Use a scheduled backup script with log rotation:

**Create `backup.sh`:**

```bash
#!/usr/bin/env bash
set -e

BACKUP_DIR="/data/backups"
RETENTION_DAYS=30
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="$BACKUP_DIR/bankers-seat-$TIMESTAMP.db"

# Ensure backup directory exists
mkdir -p "$BACKUP_DIR"

# Create backup
docker compose exec -T bankers-seat sqlite3 /data/bankers-seat.db \
  ".backup '$BACKUP_FILE'"

echo "✓ Backup created: $BACKUP_FILE"

# Verify backup
docker compose exec -T bankers-seat sqlite3 "$BACKUP_FILE" \
  "PRAGMA integrity_check;" > /dev/null && \
  echo "✓ Backup integrity verified"

# Cleanup old backups (older than retention days)
find "$BACKUP_DIR" -name "*.db" -mtime +$RETENTION_DAYS -delete && \
  echo "✓ Cleaned up backups older than $RETENTION_DAYS days"
```

**Make executable:**
```bash
chmod +x backup.sh
```

**Add to `crontab` for daily 2 AM backups:**

```bash
crontab -e
```

Add the line:
```cron
0 2 * * * cd /path/to/bankers-seat && ./backup.sh >> ./backup.log 2>&1
```

**Log output location:**
```bash
tail -f /path/to/bankers-seat/backup.log
```

**Pros**: Automated, consistent, retention policy
**Cons**: Requires cron/scheduler setup, need disk space for retention

### Strategy 3: Continuous Volume Snapshots

For production deployments with volume-level snapshots (Kubernetes, cloud VMs):

**Docker volume backup:**

```bash
# List volumes
docker volume ls | grep bankers-seat

# Backup volume to tar
docker run --rm \
  -v bankers-seat_bankers-seat-data:/data \
  -v $(pwd):/backup \
  alpine \
  tar czf /backup/bankers-seat-data-$(date +%Y%m%d-%H%M%S).tar.gz -C /data .
```

**Restore from tar:**

```bash
# Stop container
docker compose stop

# Clear existing data
docker compose exec bankers-seat rm -f /data/bankers-seat.db

# Restore volume
docker run --rm \
  -v bankers-seat_bankers-seat-data:/data \
  -v $(pwd):/backup \
  alpine \
  tar xzf /backup/bankers-seat-data-YYYYMMDD-HHMMSS.tar.gz -C /data

# Restart
docker compose start
```

**Pros**: Complete filesystem snapshot, fast restore
**Cons**: Larger file size, requires volume access

## Restoration Procedures

### Before Restoring

1. **Verify backup integrity:**
   ```bash
   docker compose exec bankers-seat sqlite3 /data/backup-name.db \
     "PRAGMA integrity_check;"
   ```
   Expected output: `ok`

2. **Document current database size:**
   ```bash
   docker compose exec bankers-seat ls -lh /data/bankers-seat.db
   ```

3. **Ensure sufficient disk space:**
   ```bash
   docker compose exec bankers-seat df -h /data
   ```

### Restore from Backup

**Option A: Restore with Running Container (Safest)**

```bash
# Stop accepting new connections (optional but recommended for data consistency)
# Consider pausing sessions if your application supports it

# List available backups
docker compose exec bankers-seat ls -lh /data/*.db

# Restore from backup (creates a new database file from backup snapshot)
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db \
  ".restore '/data/backup-name.db'"

# Verify integrity
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db \
  "PRAGMA integrity_check;"

# Restart application (or just reload connected clients)
docker compose restart bankers-seat
```

**Option B: Restore by File Replacement (Fast, Requires Downtime)**

```bash
# Stop the application
docker compose stop bankers-seat

# Backup current database (safety copy)
docker compose exec bankers-seat cp /data/bankers-seat.db \
  /data/bankers-seat-pre-restore-$(date +%Y%m%d-%H%M%S).db

# Replace with backup
docker compose exec bankers-seat cp /data/backup-name.db /data/bankers-seat.db

# Verify integrity
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db \
  "PRAGMA integrity_check;"

# Restart
docker compose start bankers-seat
```

### Verify Restoration

After restore, verify the restoration:

```bash
# Check database is accessible
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db \
  "SELECT COUNT(*) FROM game_sessions;"

# Check specific session (replace SESSION_ID)
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db \
  "SELECT id, status, created_at FROM game_sessions WHERE id = 'SESSION_ID';"

# Check ledger for session
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db \
  "SELECT COUNT(*) FROM ledger_transactions WHERE session_id = 'SESSION_ID';"

# Check participant balances
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db \
  "SELECT participant_id, balance FROM accounts WHERE session_id = 'SESSION_ID';"
```

### Verify Data Consistency

Run consistency checks after any restore:

```bash
# Comprehensive PRAGMA check
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db << 'EOF'
PRAGMA integrity_check;
PRAGMA foreign_key_check;
PRAGMA quick_check;
EOF
```

All should return `ok`.

## Disaster Recovery

### Complete Database Loss

If the database file is corrupted or lost:

1. **Stop the container:**
   ```bash
   docker compose stop
   ```

2. **Clear the corrupted database:**
   ```bash
   docker volume rm bankers-seat_bankers-seat-data
   ```
   Or manually delete `/data/bankers-seat.db`

3. **Restart the container** (migrations will recreate empty schema):
   ```bash
   docker compose up -d
   ```

4. **Restore from backup** (see Restoration Procedures above)

### Partial Data Corruption

If specific tables are corrupted but most data is intact:

```bash
# Export intact tables to a clean database
docker compose exec bankers-seat sqlite3 /data/bankers-seat.db << 'EOF'
-- Create backup first
VACUUM INTO '/data/vacuum-repair.db';
EOF

# If VACUUM succeeds, replace
docker compose exec bankers-seat cp /data/vacuum-repair.db /data/bankers-seat.db
docker compose restart bankers-seat
```

### Failed Migration

If the application fails to start due to a migration error:

1. **Restore from last known-good backup**
2. **Use container logs for diagnosis:**
   ```bash
   docker compose logs bankers-seat | tail -100
   ```

3. **Contact support with:**
   - Error messages from logs
   - Backup file for testing
   - Environment configuration (non-sensitive)

## Retention Policy

### Recommended Retention

- **Active sessions**: Never delete during active game
- **Completed sessions**: Retain for ≥30 days after completion
- **Archived**: Long-term storage for audit/dispute resolution
- **Backups**: Retain full daily backups for ≥14 days

### Storage Calculation

Typical sizing (adjust based on usage):

- **Per active session**: ~50–200 KB (varies with transaction count and field count)
- **Per backup**: ~Size of database + ~20% overhead
- **Recommended disk**: 3× peak expected database size

Example:
- 100 active sessions: ~50 MB database
- 20 completed sessions: ~30 MB archived
- 14 daily backups: ~14 × 100 MB = 1.4 GB backups
- **Total space needed**: ~2 GB with safety margin

## Common Issues

### "database is locked"

**Cause**: Another process is accessing the database
**Solution**:
```bash
# Restart container to reset locks
docker compose restart bankers-seat
```

### "Backup incomplete" or corruption detected

**Cause**: Database was actively written during backup
**Solution**:
```bash
# Verify with integrity check
docker compose exec bankers-seat sqlite3 /data/backup-name.db \
  "PRAGMA integrity_check;"

# If failed, discard and retry backup
rm /data/backup-name.db
```

### "restore failed" during `.restore` command

**Cause**: Backup file is corrupted or incompatible version
**Solution**:
1. Verify backup integrity first
2. Check backup file size (should be similar to current DB)
3. Use file replacement method instead (Option B above)

### Container won't start after restore

**Cause**: Database schema mismatch or corruption
**Steps**:
```bash
# Check logs
docker compose logs bankers-seat

# Verify backup integrity
docker compose exec bankers-seat sqlite3 /data/backup.db \
  "PRAGMA integrity_check;"

# If needed, restore from older backup
docker compose stop
# Replace file or run .restore again
docker compose start
```

## Backup Testing

**Always test backups before relying on them for recovery.**

### Monthly Backup Test

```bash
#!/usr/bin/env bash
# Test the most recent backup

LATEST_BACKUP=$(ls -t /data/backup*.db 2>/dev/null | head -1)

if [ -z "$LATEST_BACKUP" ]; then
  echo "No backup found"
  exit 1
fi

echo "Testing backup: $LATEST_BACKUP"

# Verify integrity
docker compose exec bankers-seat sqlite3 "$LATEST_BACKUP" \
  "PRAGMA integrity_check;" || {
  echo "✗ Backup integrity check failed"
  exit 1
}

# Verify essential tables exist
docker compose exec bankers-seat sqlite3 "$LATEST_BACKUP" << 'EOF' || {
  echo "✗ Essential tables missing"
  exit 1
}
SELECT COUNT(*) FROM game_sessions;
SELECT COUNT(*) FROM ledger_transactions;
SELECT COUNT(*) FROM accounts;
EOF

echo "✓ Backup test passed"
```

## Monitoring Backups

### Check Backup Status

```bash
# List backups with size and age
docker compose exec bankers-seat bash -c \
  "ls -lh /data/backup*.db | awk '{print \$9, \$5, \$6, \$7, \$8}'"
```

### Alert Conditions

Monitor for:
- No backup created in 24 hours
- Backup size significantly smaller than current database
- Integrity check failures
- Disk space below 10% of `/data` volume

## Advanced: Point-in-Time Recovery

For detailed requirements, see `docs/10-data-storage.md`.

SQLite does not natively support WAL-based point-in-time recovery. For time-based recovery:

1. Implement application-level snapshot versioning
2. Keep multiple dated backups (daily, weekly, monthly)
3. Restore from nearest backup + manually rebuild via ledger if needed

## Support

For backup/restore issues:

1. Verify database integrity: `PRAGMA integrity_check;`
2. Check container logs: `docker compose logs bankers-seat`
3. Ensure sufficient disk space: `df -h /data`
4. Test with a copy of the backup before production restore

Contact support with:
- Error output from backup/restore commands
- Database size and age
- Backup file (if applicable)
- Docker Compose version: `docker compose version`
