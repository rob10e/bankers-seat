# Roadmap

## Current delivery status (2026-07-21)

### Phase 3 Complete ✅ (2026-07-21)

- Monorepo scaffold (`apps/web`, `apps/server`, `packages/*`, `tests/*`) with pnpm workspace and .NET solution.
- Web UI scaffold with responsive desktop/mobile navigation, settings, theme mode support, and routed screens for template catalog, host setup, join, and game workspace.
- Backend scaffold with versioned `/api/v1` controllers, in-memory session service, template catalog loading, and SignalR hub path.
- Frontend/backend integration for template listing, create session, join session, and snapshot retrieval.
- Template validator CLI and root command `pnpm templates:validate`.
- SQLite persistence baseline with EF Core `DbContext`, checked-in migration, and persisted sessions/participants/accounts/template snapshots.
- Template validation test suite and integration coverage for active-session template snapshot immutability.
- Domain money and correction invariant tests for balanced postings, overdraft behavior, duplicate correction protection, and atomic failure handling.
- GitHub Actions CI workflow enforcing template validation, workspace lint/typecheck/tests, and .NET build/integration tests.
- Docker image and Compose file for self-hosted release packaging.
- Financial command handlers for participant transfers and transaction corrections with idempotency, version checks, host authorization, and immutable ledger persistence.
- Bank-payment and bank-collection command handlers with transactional balance updates and immutable ledger postings.
- Template financial action execution handler for declarative `bank-to-player`, `player-to-bank`, `player-to-player`, and `adjust-player-balance` operations with idempotency and immutable ledger postings.
- Template field action execution support for `set-field` and `increment-field`, with persisted player field values in authorized snapshots.
- Template field action execution extension for `toggle-field` with boolean-only validation and authorized snapshot updates.
- Template composite action execution support for atomic multi-step actions combining supported financial and field operations.
- Host-authorized session lifecycle command support (`start`, `pause`, `resume`, `complete`) with expected-version and idempotency enforcement.
- SignalR session-snapshot broadcasts after accepted mutations so subscribed devices remain synchronized.
- Integration tests for transfer/correction command paths including replay, stale-version rejection, and duplicate-correction protection.
- Authorized ledger read endpoint with cursor-style pagination over persisted immutable transactions/postings.
- Authorized session export endpoint returning snapshot plus full immutable ledger history.
- Health check endpoints (`/health/live`, `/health/ready`, `/health/templates`, `/health/version`) for Docker/Kubernetes monitoring and operator diagnostics.
- Template catalog caching with admin rescan endpoint (`POST /api/v1/admin/templates/rescan`) for zero-downtime template updates in self-hosted deployments.
- Playwright multiplayer E2E test suite with 11 test scenarios covering Phase 2 exit criteria: multi-device consistency, idempotent submissions, refresh/reconnect identity preservation, transaction auditability, balance synchronization, atomic transfers, lifecycle, error handling, and session export.
- Comprehensive backup and restore documentation (docs/21-backup-and-restore.md) with 4 strategies (manual, automated daily, volume snapshots, file replacement), disaster recovery procedures, retention policy, sizing calculator, and 5 troubleshooting scenarios.
- Backup automation scripts (backup.sh, restore.sh) with cron integration, configurable retention, integrity verification, and comprehensive logging.
- Admin diagnostics API endpoint (`GET /api/v1/admin/diagnostics`) with database health, session statistics, ledger consistency validation, template validation status, and error log aggregation.
- Admin diagnostics web console (wwwroot/admin/diagnostics.html) with real-time dashboard, color-coded status indicators, auto-refresh, and operator-friendly alerts.
- Progressive Web App (PWA) installation support with manifest.json, service worker caching (network-first for APIs, cache-first for assets), offline capability, and graceful degradation.
- Service worker pre-caching of static assets, intelligent cache versioning, and periodic update checks.
- Install prompts for desktop (Chrome/Edge/Firefox) and mobile (iOS Safari, Android Chrome) with React hooks integration.
- Offline indicators and update prompts with graceful fallback to cached responses when disconnected.
- PWA documentation (docs/23-pwa-installation.md) covering installation methods, architecture, offline behavior, testing strategies, and troubleshooting.

### Phase 4 Complete ✅ (2026-07-21)

- PostgreSQL database support with dual-provider configuration (SQLite for dev, PostgreSQL for production) and connection pooling.
- User account system with JWT authentication, secure registration/login, and refresh token rotation.
- Account system with BCrypt password hashing (12-round work factor), token validation middleware, and profile endpoints.
- Saved sessions metadata (ownership, labels, creation/access times, participant counts) with pagination support.
- Room security enhancements: 8-character room codes, temporary expiring join links, and IP-based rate limiting.
- Audit logging system capturing actor, action, timestamp, IP address, and result for all sensitive operations.
- Data retention and privacy controls with configurable TTL policies, auto-cleanup, and GDPR user deletion cascading.
- Administrative support operations: session lookup, force pause/resume/delete, and complete audit trail of admin actions.
- Application Insights telemetry integration (ready for configuration) with structured logging on all services.
- Rate limiting middleware with configurable thresholds per endpoint and operator-friendly response headers.
- Phase 4 documentation (docs/24-phase4-hosted-production.md) with API contracts, security considerations, and deployment guidance.
- All Phase 4 features fully backward compatible with Phase 1–3 (guest sessions and anonymous operations still supported).

### Still pending in immediate roadmap

- No immediate Phase 4 gaps. Phase 5 — Template Ecosystem ready to begin.

## Phase 0 — Foundation and validation

### Outcomes

- ~~Repository scaffold.~~
- ~~Core documentation and ADRs.~~
- ~~JSON Schema.~~
- ~~Two generic sample templates.~~
- ~~CLI/template validation.~~
- ~~Domain model tests for money and corrections.~~
- ~~Basic CI.~~

### Exit criteria

- ~~All samples validate.~~
- ~~Domain invariants are tested.~~
- ~~Architecture boundaries are represented in the repository.~~
- ~~No UI framework dependency leaks into template contracts.~~

## Phase 1 — Local single-device banker prototype

### Features

- ~~Template catalog.~~
- ~~Host creates a session.~~
- ~~Participant setup on one device.~~
- ~~Balances.~~
- ~~Bank payment, collection, and player transfer.~~
- ~~Custom fields.~~
- ~~Ledger.~~
- ~~Corrections.~~
- ~~SQLite persistence.~~

### Purpose

Validate the template model and banker workflow before adding multiplayer complexity.

## Phase 2 — Real-time multiplayer MVP

### Features

- ~~Room code~~ and QR joining.
- ~~Multiple devices.~~
- ~~SignalR updates.~~
- ~~reconnect credentials.~~
- ~~lobby.~~
- ~~host/banker/player roles.~~
- ~~session lifecycle.~~
- ~~idempotency and versioning.~~
- ~~responsive phone UI.~~
- ~~Playwright multiplayer flows.~~

### Exit criteria

- ~~Two or more devices remain consistent through normal gameplay.~~
- ~~Duplicate submissions do not duplicate transactions.~~
- ~~Refresh/reconnect preserves identity.~~
- ~~Host correction is auditable.~~

## Phase 3 — Self-hosted release

### Features

- ~~Docker image and Compose file.~~
- ~~mounted template directories.~~
- ~~template rescan.~~
- ~~health checks.~~
- ~~backup/restore documentation.~~
- ~~administrative diagnostics.~~
- ~~export.~~
- ~~PWA installation.~~

## Phase 4 — Hosted production

### Features

- ~~PostgreSQL.~~
- ~~account system.~~
- ~~saved/recent sessions.~~
- ~~stronger room security.~~
- ~~observability.~~
- ~~retention controls.~~
- ~~operational support process.~~
- ~~scalable SignalR deployment.~~

## Phase 5 — Template ecosystem (In Progress)

### Features

- ~~Import/export package.~~
- ~~template author CLI.~~
- ~~visual preview.~~
- ~~visual template editor.~~
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
