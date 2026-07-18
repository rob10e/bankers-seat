# Risks and Open Decisions

## Recommended defaults

| Decision | Recommended default |
|---|---|
| Backend | ASP.NET Core |
| Real-time transport | SignalR |
| Web build | Vite |
| UI | MUI |
| Database local | SQLite |
| Database hosted | PostgreSQL |
| Mobile | Capacitor |
| Money representation | Signed 64-bit integer base units |
| Template contract | JSON Schema plus semantic validation |
| State authority | Server |
| Corrections | Compensating ledger entries |
| Template loading | Server directory scan |
| Active template behavior | Persisted snapshot |
| Remote template assets | Disabled |

## Risk: Template flexibility becomes a scripting language

### Impact

Security risk, untestable behavior, and a complicated support surface.

### Mitigation

Keep a closed set of declarative operations. Add new operation types only with schema, domain, authorization, and testing support. Do not add arbitrary expression evaluation.

## Risk: Copyrighted game content

### Impact

Removal requests, marketplace risk, or commercial restrictions.

### Mitigation

Ship original generic examples. Separate licensed content. Require rights attestations for public submissions. Add moderation and removal processes before a public catalog.

## Risk: Client/server divergence

### Impact

Players see different balances or actions.

### Mitigation

Server authority, ordered versions, resync protocol, deterministic view projection, and multiplayer end-to-end tests.

## Risk: Double submission

### Impact

Duplicate payments.

### Mitigation

Disable duplicate UI action, use idempotency keys, persist idempotency results, and test rapid retries.

## Risk: Host leaves

### Impact

Game becomes unmanageable.

### Mitigation

Reconnect grace period, co-banker role, explicit host transfer, and audited recovery policy.

## Risk: Active session changes after template update

### Impact

Rules or balances unexpectedly change mid-game.

### Mitigation

Persist complete template snapshot and content hash at session creation.

## Risk: Browser storage loss

### Impact

Player cannot reconnect as the same identity.

### Mitigation

Provide host-assisted identity recovery, optional PIN/account later, and hybrid secure storage.

## Risk: Server restart during a command

### Impact

Unknown client result and possible retry.

### Mitigation

Atomic persistence, idempotency record, post-commit events, client status resolution, and eventually outbox.

## Risk: Finite bank accounting complexity

### Impact

Unexpected bank shortages and conservation bugs.

### Mitigation

Make finite bank an explicit mode, model bank as an account, and property-test conservation.

## Open product decisions

These can be deferred while using the recommended defaults:

1. Final product and repository name.
2. Whether anonymous self-hosted mode or account-based hosted mode launches first.
3. Whether players may directly edit their balance outside explicit actions.
4. Whether initial custom fields can be private.
5. Whether finite-bank mode is in MVP.
6. Whether a co-banker role is included in MVP.
7. How long completed sessions are retained by default.
8. Whether template imports are administrator-only at first.
9. Whether generic “house rule” overrides are allowed.
10. Which analytics, if any, are collected.

## Decisions to avoid deferring

- Integer money.
- Server authority.
- Immutable ledger.
- Template snapshot.
- No executable templates.
- Edition in template identity.
- authorization on every mutation.
