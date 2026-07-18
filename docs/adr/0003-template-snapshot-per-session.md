# ADR 0003: Template Snapshot Per Session

- Status: Accepted
- Date: 2026-07-17

## Context

Template files may be edited, upgraded, removed, or replaced while sessions are active or archived.

## Decision

Persist the complete validated template and content hash when a session is created. The session uses only that snapshot.

## Consequences

### Positive

- Reproducible gameplay.
- Stable archived history.
- Safe template updates.
- Better diagnostics.

### Negative

- Additional database storage.
- Asset retention requires immutable package handling.
- Fixing a bad template does not automatically fix existing sessions.
