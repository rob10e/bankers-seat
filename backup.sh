#!/usr/bin/env bash

# Banker's Seat - Automated Backup Script
# Place this script in your deployment directory and add to crontab for daily backups
# Usage: ./backup.sh [--help] [--retention-days N] [--backup-dir PATH]

set -e

# Configuration
BACKUP_DIR="${BACKUP_DIR:-.}/backups"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
LOG_FILE="${LOG_FILE:-.}/backup.log"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="$BACKUP_DIR/bankers-seat-$TIMESTAMP.db"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Helper functions
log() {
  echo "[$TIMESTAMP] $1" | tee -a "$LOG_FILE"
}

log_success() {
  echo -e "${GREEN}[$(date +%Y%m%d-%H%M%S)] ✓ $1${NC}" | tee -a "$LOG_FILE"
}

log_warning() {
  echo -e "${YELLOW}[$(date +%Y%m%d-%H%M%S)] ⚠ $1${NC}" | tee -a "$LOG_FILE"
}

log_error() {
  echo -e "${RED}[$(date +%Y%m%d-%H%M%S)] ✗ $1${NC}" | tee -a "$LOG_FILE"
}

# Show help
show_help() {
  cat << EOF
Banker's Seat Automated Backup Script

Usage: $0 [OPTIONS]

OPTIONS:
  --help                Show this help message
  --backup-dir PATH     Backup directory (default: ./backups)
  --retention-days N    Keep backups for N days (default: 30)
  --no-verify          Skip integrity verification
  --log-file PATH       Log file location (default: ./backup.log)

EXAMPLES:
  # Standard backup (uses defaults)
  $0

  # Custom retention (60 days)
  $0 --retention-days 60

  # Custom backup directory
  $0 --backup-dir /mnt/backup

  # Skip verification (faster)
  $0 --no-verify

CRON SETUP:
  # Add to crontab for daily 2 AM backups:
  0 2 * * * cd /path/to/bankers-seat && $0 >> $LOG_FILE 2>&1

  # Or with environment variables:
  0 2 * * * cd /path/to/bankers-seat && BACKUP_DIR=/mnt/backup $0

ENVIRONMENT VARIABLES:
  BACKUP_DIR         Backup directory (override with --backup-dir)
  RETENTION_DAYS     Retention days (override with --retention-days)
  LOG_FILE           Log file location
EOF
}

# Parse arguments
VERIFY_BACKUP=true
while [[ $# -gt 0 ]]; do
  case $1 in
    --help)
      show_help
      exit 0
      ;;
    --backup-dir)
      BACKUP_DIR="$2"
      shift 2
      ;;
    --retention-days)
      RETENTION_DAYS="$2"
      shift 2
      ;;
    --no-verify)
      VERIFY_BACKUP=false
      shift
      ;;
    --log-file)
      LOG_FILE="$2"
      shift 2
      ;;
    *)
      log_error "Unknown option: $1"
      show_help
      exit 1
      ;;
  esac
done

# Main backup procedure
main() {
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log "Starting Banker's Seat backup"
  log "Backup directory: $BACKUP_DIR"
  log "Retention: $RETENTION_DAYS days"
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

  # Check if Docker Compose is available
  if ! command -v docker compose &> /dev/null; then
    log_error "docker compose not found. Is Docker installed?"
    exit 1
  fi

  # Check if container is running
  if ! docker compose ps bankers-seat 2>/dev/null | grep -q "Up"; then
    log_warning "Container 'bankers-seat' is not running. Attempting to check if compose file exists..."
    if ! docker compose config > /dev/null 2>&1; then
      log_error "docker-compose.yml not found or invalid. Run this script from the deployment directory."
      exit 1
    fi
  fi

  # Create backup directory
  if ! mkdir -p "$BACKUP_DIR"; then
    log_error "Failed to create backup directory: $BACKUP_DIR"
    exit 1
  fi
  log "Backup directory ready: $BACKUP_DIR"

  # Get database size before backup
  DB_SIZE=$(docker compose exec -T bankers-seat stat -c%s /data/bankers-seat.db 2>/dev/null || echo "unknown")
  if [ "$DB_SIZE" != "unknown" ]; then
    DB_SIZE_MB=$((DB_SIZE / 1024 / 1024))
    log "Current database size: ${DB_SIZE_MB} MB"
  fi

  # Create backup
  log "Creating backup: $BACKUP_FILE"
  if ! docker compose exec -T bankers-seat sqlite3 /data/bankers-seat.db ".backup '$BACKUP_FILE'" 2>/dev/null; then
    log_error "Backup creation failed"
    exit 1
  fi
  log_success "Backup created"

  # Verify backup file exists
  if ! docker compose exec -T bankers-seat test -f "$BACKUP_FILE" 2>/dev/null; then
    log_error "Backup file not found after creation: $BACKUP_FILE"
    exit 1
  fi

  # Get backup size
  BACKUP_SIZE=$(docker compose exec -T bankers-seat stat -c%s "$BACKUP_FILE" 2>/dev/null || echo "unknown")
  if [ "$BACKUP_SIZE" != "unknown" ]; then
    BACKUP_SIZE_MB=$((BACKUP_SIZE / 1024 / 1024))
    log "Backup size: ${BACKUP_SIZE_MB} MB"
  fi

  # Verify backup integrity (if enabled)
  if [ "$VERIFY_BACKUP" = true ]; then
    log "Verifying backup integrity..."
    INTEGRITY_RESULT=$(docker compose exec -T bankers-seat sqlite3 "$BACKUP_FILE" "PRAGMA integrity_check;" 2>/dev/null)
    if [ "$INTEGRITY_RESULT" = "ok" ]; then
      log_success "Backup integrity verified"
    else
      log_error "Backup integrity check failed: $INTEGRITY_RESULT"
      exit 1
    fi
  fi

  # Cleanup old backups
  log "Cleaning up backups older than $RETENTION_DAYS days..."
  OLD_BACKUPS=$(docker compose exec -T bankers-seat find "$BACKUP_DIR" -name "*.db" -mtime +$RETENTION_DAYS 2>/dev/null | wc -l)
  if [ "$OLD_BACKUPS" -gt 0 ]; then
    docker compose exec -T bankers-seat find "$BACKUP_DIR" -name "*.db" -mtime +$RETENTION_DAYS -delete 2>/dev/null
    log_success "Deleted $OLD_BACKUPS old backup(s)"
  else
    log "No old backups to delete"
  fi

  # List current backups
  log "Current backups in $BACKUP_DIR:"
  docker compose exec -T bankers-seat ls -lh "$BACKUP_DIR"/*.db 2>/dev/null | awk '{print "  " $9 " (" $5 ")"}' || log "  (none yet)"

  # Summary
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log_success "Backup completed successfully"
  log "Location: $BACKUP_FILE"
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
}

# Run main function
main
exit 0
