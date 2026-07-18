# Roadmap

## Current delivery status (2026-07-18)

### Completed now

- Monorepo scaffold (`apps/web`, `apps/server`, `packages/*`, `tests/*`) with pnpm workspace and .NET solution.
- Web UI scaffold with responsive desktop/mobile navigation, settings, theme mode support, and routed screens for template catalog, host setup, join, and game workspace.
- Backend scaffold with versioned `/api/v1` controllers, in-memory session service, template catalog loading, and SignalR hub path.
- Frontend/backend integration for template listing, create session, join session, and snapshot retrieval.
- Template validator CLI and root command `pnpm templates:validate`.
- SQLite persistence baseline with EF Core `DbContext`, checked-in migration, and persisted sessions/participants/accounts/template snapshots.
- Template validation test suite and integration coverage for active-session template snapshot immutability.
- Domain money and correction invariant tests for balanced postings, overdraft behavior, duplicate correction protection, and atomic failure handling.
- GitHub Actions CI workflow enforcing template validation, workspace lint/typecheck/tests, and .NET build/integration tests.
- Financial command handlers for participant transfers and transaction corrections with idempotency, version checks, host authorization, and immutable ledger persistence.
- Integration tests for transfer/correction command paths including replay, stale-version rejection, and duplicate-correction protection.
- Authorized ledger read endpoint with cursor-style pagination over persisted immutable transactions/postings.

### Still pending in immediate roadmap

- Additional financial command handlers beyond transfer/correction (bank payments, collections, template action execution).

## Phase 0 — Foundation and validation

### Outcomes

- Repository scaffold.
- Core documentation and ADRs.
- JSON Schema.
- Two generic sample templates.
- CLI/template validation.
- Domain model tests for money and corrections.
- Basic CI.

### Exit criteria

- All samples validate.
- Domain invariants are tested.
- Architecture boundaries are represented in the repository.
- No UI framework dependency leaks into template contracts.

## Phase 1 — Local single-device banker prototype

### Features

- Template catalog.
- Host creates a session.
- Participant setup on one device.
- Balances.
- Bank payment, collection, and player transfer.
- Custom fields.
- Ledger.
- Corrections.
- SQLite persistence.

### Purpose

Validate the template model and banker workflow before adding multiplayer complexity.

## Phase 2 — Real-time multiplayer MVP

### Features

- Room code and QR joining.
- Multiple devices.
- SignalR updates.
- reconnect credentials.
- lobby.
- host/banker/player roles.
- session lifecycle.
- idempotency and versioning.
- responsive phone UI.
- Playwright multiplayer flows.

### Exit criteria

- Two or more devices remain consistent through normal gameplay.
- Duplicate submissions do not duplicate transactions.
- Refresh/reconnect preserves identity.
- Host correction is auditable.

## Phase 3 — Self-hosted release

### Features

- Docker image and Compose file.
- mounted template directories.
- template rescan.
- health checks.
- backup/restore documentation.
- administrative diagnostics.
- export.
- PWA installation.

## Phase 4 — Hosted production

### Features

- PostgreSQL.
- account system.
- saved/recent sessions.
- stronger room security.
- observability.
- retention controls.
- operational support process.
- scalable SignalR deployment.

## Phase 5 — Template ecosystem

### Features

- Import/export package.
- template author CLI.
- visual preview.
- visual template editor.
- template diff/migration helper.
- private sharing.
- marketplace governance and licensing workflow.

## Phase 6 — Hybrid mobile

### Features

- Capacitor shell.
- native share.
- QR scanner.
- haptics.
- secure credential storage.
- deep links.
- app-store packaging.
- mobile crash reporting and update strategy.

## Phase 7 — Advanced enhancements

Potential features:

- Offline single-device mode.
- Local-network discovery.
- voice-accessibility actions.
- remote display/spectator screen.
- printable game summary.
- home-screen widgets.
- optional house rules.
- template localization.
- NFC or physical accessory integration.

## MVP backlog priorities

### Must

- Template discovery/validation.
- Edition support.
- Template snapshot.
- lobby/join/reconnect.
- balances/transfers.
- custom fields.
- ledger/corrections.
- permissions.
- responsive UI.
- core testing.

### Should

- QR join.
- banker console.
- batch payday.
- session export.
- PWA.
- template diagnostics.

### Could

- finite bank.
- host transfer.
- co-banker.
- icons for custom fields.
- action favorites.
- recent rooms.

### Not in MVP

- marketplace.
- arbitrary rule scripting.
- user accounts.
- native app stores.
- full offline multiplayer.
- proprietary official templates without licensing.
