# Testing Strategy

## Current baseline coverage (2026-07-20)

- Web smoke test covering routed app shell render (`apps/web/src/app/app.test.tsx`).
- Template validation unit tests covering schema, semantic rules, discovery, and JSON parsing (`packages/template-tools/src/validation.test.ts`).
- Domain money mutation and correction invariant tests covering overdraft policy, balanced postings, duplicate correction rejection, and atomic failure behavior (`tests/integration/server/money-correction-domain-tests.cs`).
- Integration tests for template catalog discovery, session snapshot persistence, and active-session snapshot stability after source template edits (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for transfer/correction command handlers covering success, stale session version, insufficient funds, unauthorized actor, idempotent replay, duplicate idempotency key mismatch, and duplicate correction rejection (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for bank-payment and bank-collection command paths, including account-balance effects on player and bank accounts (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for template financial action execution, including action-driven bank/player balance changes and unsupported operation rejection (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for template action scope handling, including `all-players` bank payments and `two-players` player-to-player transfers (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for player field defaults and template `increment-field`/`toggle-field` action execution behavior (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for template `composite` action execution with atomic multi-step financial + field effects (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for `adjust-player-balance` action execution (top-level and composite-step usage) (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for host-authorized session lifecycle transitions (`start`, `pause`, `resume`, `complete`), idempotent replay behavior, and join rejection after lobby (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for SignalR session-snapshot broadcasts after accepted mutations (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for authorized ledger retrieval with newest-first pagination and unauthorized access rejection (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for authorized session export retrieval and unauthorized export rejection (`tests/integration/server/session-scaffold-tests.cs`).
- Integration tests for health check endpoints covering live, ready, templates, and version probes (`tests/integration/server/health-checks-tests.cs`).
- Integration tests for template catalog caching and rescan behavior covering cache refresh, idempotency, and catalog consistency (`tests/integration/server/template-rescan-tests.cs`).
- Template schema and semantic validation CLI via `pnpm templates:validate`.
- **Playwright multiplayer E2E tests covering Phase 2 exit criteria:**
  - Multi-device consistency during active gameplay
  - Idempotent transaction submission (duplicate protection)
  - Session persistence and identity preservation after page refresh
  - Transaction correction auditability with full ledger trail
  - Bank payments with cross-device balance synchronization
  - Atomic player-to-player transfers
  - Session lifecycle (create, join, start, complete, export)
  - Error handling (invalid room codes, stale session versions, insufficient funds)
  - (`tests/e2e/tests/multiplayer.spec.ts`)

## Quality objectives

The most important property is that players can trust balances and history. Test coverage prioritizes domain invariants, concurrency, template safety, reconnect behavior, and critical gameplay flows.

## Test pyramid

### Unit tests

#### Domain

- Transfers.
- Bank payments.
- Collections.
- Overdraft policy.
- Finite bank supply.
- Batch operations.
- Composite action atomicity.
- Corrections.
- session lifecycle.
- permissions.
- idempotency decision behavior.
- field validation.

#### Template validation

- Valid minimal template.
- Valid full template.
- Missing required fields.
- Unknown operation.
- duplicate IDs.
- invalid defaults.
- unsafe paths.
- excessive limits.
- edition/version conflicts.

#### Client

- Reducers and derived view models.
- Currency formatting.
- Command builders.
- Permission-aware rendering.
- Error mapping.
- reconnect state machine.

## Integration tests

- Database transaction atomicity.
- Concurrency conflicts.
- Idempotency persistence.
- Ledger posting and balance consistency.
- Template snapshot persistence.
- File-system catalog discovery.
- Asset path containment.
- SignalR authentication and authorization.
- Event publication after commit.
- Session restore after server restart.

Use production-like persistence for a subset of tests, including PostgreSQL in CI when practical.

## End-to-end tests

Use Playwright for:

1. Host creates a session.
2. Two players join.
3. Host starts.
4. Bank pays a player.
5. Player transfers another player.
6. Custom field changes.
7. A client disconnects and reconnects.
8. Host corrects a transaction.
9. All clients show matching state.
10. Host completes and exports the game.

Also cover:

- Invalid room.
- Full room.
- Locked lobby.
- insufficient funds.
- stale browser tab.
- duplicate submit.
- mobile viewport.
- keyboard-only operation.

## Property-based tests

High-value properties:

- Sum of finite-bank account balances remains constant after internal transfers.
- Transfer debit and credit always match.
- Replaying the same idempotent command does not change state twice.
- Any sequence of accepted commands yields ledger balances equal to stored balances.
- A correction plus original has the expected net effect.
- Active session behavior is unchanged after source template modification.

## Concurrency tests

Simulate:

- Two payments from the same low-balance account.
- Rapid double-tap command.
- Host and player editing the same field.
- Multiple server nodes attempting to process the same idempotency key.
- Reconnect during an in-flight command.
- Out-of-order event delivery to the client.

## Schema compatibility tests

Maintain fixtures for every supported schema version.

Test:

- Current app accepts supported versions.
- Unsupported future version fails clearly.
- Deprecated version produces warning or migration guidance.
- Sample templates validate.
- Documentation examples remain valid JSON.

## Security tests

- Unauthorized command.
- role escalation attempt.
- room-code brute-force rate limit.
- path traversal.
- malicious SVG or HTML payload.
- oversized JSON.
- zip-slip in future package import.
- duplicate/ambiguous template identity.
- secret leakage in logs.
- private field data not sent to unauthorized clients.

## Accessibility tests

- Automated axe checks.
- Keyboard navigation.
- Focus restoration after dialogs.
- Screen-reader labels.
- reduced-motion preference.
- zoom and large text.
- contrast.
- touch target size.

Automated tests do not replace manual testing with assistive technology.

## CI quality gates

- Formatting.
- ESLint.
- TypeScript type check.
- .NET build with warnings treated appropriately.
- Unit tests.
- Integration tests.
- Template validation.
- Playwright smoke tests.
- Dependency/security scan.
- Markdown link check.
- JSON Schema example validation.

## Test data policy

Use original generic game data. Do not commit proprietary game artwork or copied rule text as fixtures.
