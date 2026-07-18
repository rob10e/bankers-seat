# Data Storage

## Database choice

### Local and self-hosted default

SQLite provides low-friction deployment and backup for a single server instance.

### Hosted production

PostgreSQL is preferred for concurrent hosted use, operational tooling, and future scale.

The persistence abstraction must preserve transactional semantics across both.

## Proposed tables

- `game_sessions`
- `template_snapshots`
- `participants`
- `participant_credentials`
- `accounts`
- `ledger_transactions`
- `ledger_postings`
- `player_field_values`
- `field_changes`
- `idempotency_records`
- `outbox_events`
- `session_exports`
- `template_catalog_records`
- `template_validation_errors`

## Important indexes

- Unique room code for active/non-expired rooms.
- Session ID plus session version.
- Session ID plus ledger sequence.
- Session ID plus participant ID.
- Session ID plus actor ID plus idempotency key.
- Template ID plus edition ID plus template version.
- Session status plus updated time for cleanup jobs.

## Data types

- IDs: UUID, ULID, or database-native UUID used consistently.
- Money: signed 64-bit integer.
- Versions/sequences: signed 64-bit integer.
- Times: UTC timestamp.
- Template/field values: JSON with application validation.
- Secrets: salted hash or protected token record, never plaintext where avoidable.

## Transaction strategy

A domain command uses one database transaction. Account rows or the session aggregate use concurrency protection. On conflict, retry only when safe and bounded.

## Ledger retention

The ledger is part of game trust and should not be silently pruned. Archived session retention may be configurable, but deletion must be explicit and documented.

## Idempotency retention

Keep idempotency records at least for the realistic retry window and preferably for the entire active session. For completed sessions, compacting may retain command key and result hash.

## Outbox

Use an outbox table when reliable publication matters:

1. Persist state and event envelope in the same transaction.
2. Background publisher emits events.
3. Mark outbox record published.
4. Duplicate event delivery remains safe because clients use event IDs and session versions.

The first local MVP may publish directly after commit, but the data model should not block adopting an outbox.

## Template storage

Persist the full template snapshot in the database. Do not store only a file path.

Asset strategy options:

- Immutable content-addressed asset package.
- Copied session asset package.
- Template package retained by content hash.

The recommended approach is content-addressed assets shared by sessions and protected from mutation.

## Migrations

- Use checked-in migrations.
- Test upgrade from the previous supported release.
- Backup before destructive migrations.
- Keep migrations forward-only in production.
- Document rollback as application rollback plus database restore when schema reversal is unsafe.

## Backup

### SQLite

- Use SQLite's online backup mechanism or stop writes briefly.
- Copy the database and content-addressed asset store.
- Verify backup integrity.

### PostgreSQL

- Use managed backups or `pg_dump` plus asset backup.
- Test restore procedures.
- Encrypt backups at rest.

## Export

A session JSON export should include:

- Export schema version.
- Session metadata.
- Template identity and content hash.
- Participants.
- Final balances.
- Field values.
- Ledger and corrections.
- Created/exported timestamps.

Reconnect secrets, hashes, access tokens, internal logs, and private administrative metadata must be excluded.
