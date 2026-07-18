# ADR 0001: Server-Authoritative Ledger

- Status: Accepted
- Date: 2026-07-17

## Context

Multiple devices can submit transactions concurrently. Client-side authority would allow tampering, double spending, inconsistent ordering, and difficult recovery.

## Decision

The server is authoritative for all shared session state. Every accepted financial mutation creates an immutable ledger transaction and postings within one database transaction.

## Consequences

### Positive

- Stronger integrity.
- Consistent authorization.
- Reliable audit trail.
- Central concurrency handling.
- Easier reconnect/resync.

### Negative

- Shared gameplay requires connectivity.
- Server implementation is more complex than client-only state.
- Offline multiplayer requires a separate future design.
