#!/usr/bin/env bash

# Banker's Seat - Restore from Backup Script
# Usage: ./restore.sh [--backup FILE] [--method replace|restore] [--verify-only]

set -e

# Configuration
BACKUP_FILE=""
METHOD="restore"  # restore or replace
VERIFY_ONLY=false
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# Helper functions
log() {
  echo "[$TIMESTAMP] $1"
}

log_success() {
  echo -e "${GREEN}[$(date +%Y%m%d-%H%M%S)] ✓ $1${NC}"
}

log_warning() {
  echo -e "${YELLOW}[$(date +%Y%m%d-%H%M%S)] ⚠ $1${NC}"
}

log_error() {
  echo -e "${RED}[$(date +%Y%m%d-%H%M%S)] ✗ $1${NC}"
}

show_help() {
  cat << EOF
Banker's Seat Restore from Backup Script

Usage: $0 [OPTIONS]

OPTIONS:
  --help              Show this help message
  --backup FILE       Path to backup file (default: prompts to select)
  --method METHOD     Restore method: 'restore' (default) or 'replace'
                      - restore: Uses SQLite .restore command (no downtime)
                      - replace: Stops container, replaces file (requires restart)
  --verify-only       Only verify backup integrity without restoring
  --force             Skip confirmation prompt

EXAMPLES:
  # Interactive restore (prompts for backup selection)
  $0

  # Restore specific backup
  $0 --backup /path/to/backups/bankers-seat-20260720-020000.db

  # Restore with file replacement (requires container restart)
  $0 --backup /path/to/backup.db --method replace

  # Verify backup integrity without restoring
  $0 --backup /path/to/backup.db --verify-only

RESTORE METHODS:
  restore (default):  Uses SQLite's .restore command
                      - No container downtime
                      - Slower for large databases
                      - Safer (preserves current DB as fallback)

  replace:            Replaces database file directly
                      - Container must be stopped/restarted
                      - Faster for large databases
                      - Consider backup of current DB first

EOF
}

# Parse arguments
FORCE=false
while [[ $# -gt 0 ]]; do
  case $1 in
    --help)
      show_help
      exit 0
      ;;
    --backup)
      BACKUP_FILE="$2"
      shift 2
      ;;
    --method)
      METHOD="$2"
      if [ "$METHOD" != "restore" ] && [ "$METHOD" != "replace" ]; then
        log_error "Invalid method: $METHOD (use 'restore' or 'replace')"
        exit 1
      fi
      shift 2
      ;;
    --verify-only)
      VERIFY_ONLY=true
      shift
      ;;
    --force)
      FORCE=true
      shift
      ;;
    *)
      log_error "Unknown option: $1"
      show_help
      exit 1
      ;;
  esac
done

confirm() {
  if [ "$FORCE" = true ]; then
    return 0
  fi
  
  local prompt="$1"
  local response
  
  while true; do
    read -p "$prompt (yes/no): " response
    case "$response" in
      [yY][eE][sS])
        return 0
        ;;
      [nN][oO])
        return 1
        ;;
      *)
        echo "Please answer 'yes' or 'no'"
        ;;
    esac
  done
}

select_backup() {
  log "Available backups:"
  
  local backups=($(docker compose exec -T bankers-seat find /data -name "*.db" -type f 2>/dev/null | sort -r))
  
  # Remove current database from selection
  local filtered_backups=()
  for backup in "${backups[@]}"; do
    if [ "$backup" != "/data/bankers-seat.db" ]; then
      filtered_backups+=("$backup")
    fi
  done
  
  if [ ${#filtered_backups[@]} -eq 0 ]; then
    log_error "No backup files found in /data"
    exit 1
  fi
  
  for i in "${!filtered_backups[@]}"; do
    SIZE=$(docker compose exec -T bankers-seat stat -c%s "${filtered_backups[$i]}" 2>/dev/null || echo "?")
    SIZE_MB=$((SIZE / 1024 / 1024))
    DATE=$(docker compose exec -T bankers-seat stat -c%y "${filtered_backups[$i]}" 2>/dev/null | cut -d. -f1)
    echo "  $((i+1)). ${filtered_backups[$i]} (${SIZE_MB} MB, $DATE)"
  done
  
  echo ""
  read -p "Select backup number (1-${#filtered_backups[@]}): " selection
  
  if ! [[ "$selection" =~ ^[0-9]+$ ]] || [ "$selection" -lt 1 ] || [ "$selection" -gt ${#filtered_backups[@]} ]; then
    log_error "Invalid selection"
    exit 1
  fi
  
  BACKUP_FILE="${filtered_backups[$((selection-1))]}"
  log "Selected backup: $BACKUP_FILE"
}

verify_backup() {
  log "Verifying backup integrity..."
  
  INTEGRITY=$(docker compose exec -T bankers-seat sqlite3 "$BACKUP_FILE" "PRAGMA integrity_check;" 2>/dev/null)
  
  if [ "$INTEGRITY" = "ok" ]; then
    log_success "Backup integrity verified"
    return 0
  else
    log_error "Backup integrity check failed: $INTEGRITY"
    return 1
  fi
}

restore_method() {
  log "Using SQLite .restore command (no downtime)"
  
  log "Restoring from $BACKUP_FILE..."
  if ! docker compose exec -T bankers-seat sqlite3 /data/bankers-seat.db ".restore '$BACKUP_FILE'" 2>/dev/null; then
    log_error "Restore command failed"
    return 1
  fi
  
  log_success "Restore completed"
  return 0
}

replace_method() {
  log "Using file replacement method (requires container restart)"
  
  log "Stopping container..."
  if ! docker compose stop bankers-seat; then
    log_error "Failed to stop container"
    return 1
  fi
  
  log "Creating safety backup of current database..."
  SAFETY_BACKUP="/data/bankers-seat-pre-restore-$TIMESTAMP.db"
  if ! docker compose exec -T bankers-seat cp /data/bankers-seat.db "$SAFETY_BACKUP" 2>/dev/null; then
    log_warning "Failed to create safety backup (but continuing)"
  else
    log_success "Safety backup created: $SAFETY_BACKUP"
  fi
  
  log "Replacing database file..."
  if ! docker compose exec -T bankers-seat cp "$BACKUP_FILE" /data/bankers-seat.db; then
    log_error "Failed to replace database file"
    log_warning "Restoring from safety backup..."
    docker compose exec -T bankers-seat cp "$SAFETY_BACKUP" /data/bankers-seat.db
    docker compose start bankers-seat
    return 1
  fi
  
  log_success "Database file replaced"
  
  log "Restarting container..."
  if ! docker compose start bankers-seat; then
    log_error "Failed to start container"
    return 1
  fi
  
  log_success "Container restarted"
  return 0
}

verify_restoration() {
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log "Verifying restoration..."
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  
  # Check database integrity
  log "Checking database integrity..."
  INTEGRITY=$(docker compose exec -T bankers-seat sqlite3 /data/bankers-seat.db "PRAGMA integrity_check;" 2>/dev/null)
  if [ "$INTEGRITY" != "ok" ]; then
    log_error "Database integrity check failed: $INTEGRITY"
    return 1
  fi
  log_success "Database integrity verified"
  
  # Check session count
  log "Checking session count..."
  SESSION_COUNT=$(docker compose exec -T bankers-seat sqlite3 /data/bankers-seat.db "SELECT COUNT(*) FROM game_sessions;" 2>/dev/null)
  log "Sessions in database: $SESSION_COUNT"
  
  # Check ledger
  log "Checking ledger transactions..."
  TXN_COUNT=$(docker compose exec -T bankers-seat sqlite3 /data/bankers-seat.db "SELECT COUNT(*) FROM ledger_transactions;" 2>/dev/null)
  log "Total transactions: $TXN_COUNT"
  
  # Check participants
  log "Checking participants..."
  PART_COUNT=$(docker compose exec -T bankers-seat sqlite3 /data/bankers-seat.db "SELECT COUNT(*) FROM participants;" 2>/dev/null)
  log "Total participants: $PART_COUNT"
  
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log_success "Restoration verification completed"
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  
  return 0
}

main() {
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log "Banker's Seat Restore from Backup"
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  
  # Check Docker Compose
  if ! command -v docker compose &> /dev/null; then
    log_error "docker compose not found"
    exit 1
  fi
  
  if ! docker compose config > /dev/null 2>&1; then
    log_error "docker-compose.yml not found or invalid"
    exit 1
  fi
  
  # Select or validate backup file
  if [ -z "$BACKUP_FILE" ]; then
    select_backup
  fi
  
  if [ ! -z "$BACKUP_FILE" ] && ! docker compose exec -T bankers-seat test -f "$BACKUP_FILE" 2>/dev/null; then
    log_error "Backup file not found: $BACKUP_FILE"
    exit 1
  fi
  
  # Verify backup
  if ! verify_backup; then
    log_error "Backup verification failed. Aborting."
    exit 1
  fi
  
  # If verify-only mode, stop here
  if [ "$VERIFY_ONLY" = true ]; then
    log_success "Backup verification completed successfully"
    exit 0
  fi
  
  # Confirm restore
  echo ""
  log_warning "WARNING: This will overwrite the current database."
  log_warning "Current database: /data/bankers-seat.db"
  log_warning "Restore from: $BACKUP_FILE"
  log_warning "Method: $METHOD"
  echo ""
  
  if ! confirm "Proceed with restore?"; then
    log "Restore cancelled"
    exit 0
  fi
  
  # Perform restore
  if [ "$METHOD" = "restore" ]; then
    if ! restore_method; then
      log_error "Restore failed"
      exit 1
    fi
  else
    if ! replace_method; then
      log_error "Restore failed"
      exit 1
    fi
  fi
  
  # Wait for container to stabilize
  sleep 2
  
  # Verify restoration
  if ! verify_restoration; then
    log_error "Restoration verification failed"
    exit 1
  fi
  
  log_success "Restore completed successfully"
  exit 0
}

main
