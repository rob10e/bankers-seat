# Playwright Multiplayer E2E Tests

## Overview

This test suite covers Phase 2 exit criteria for Banker's Seat multiplayer functionality:

- ✅ Two or more devices remain consistent through normal gameplay
- ✅ Duplicate submissions do not duplicate transactions
- ✅ Refresh/reconnect preserves identity
- ✅ Host correction is auditable

## Test Coverage

### Core Multiplayer Scenarios

1. **Multi-device consistency** — Host creates session, two players join, host starts game, all devices see matching state
2. **Balance consistency across devices** — Bank payments update consistently on all connected clients
3. **Atomic transfers** — Transfers between players are atomic with consistent view across all devices
4. **Idempotent submissions** — Duplicate transactions with same idempotency key don't double-charge
5. **Transaction corrections** — Host can correct transactions with full audit trail
6. **Session persistence** — Page refresh during active game preserves participant identity and balance
7. **Financial constraints** — Overdraft policy is enforced correctly
8. **Session lifecycle** — Session completion and export preserves full audit trail
9. **Error handling** — Invalid room codes and stale versions are rejected appropriately

### Test Structure

```
tests/e2e/
├── helpers/
│   └── session-api.ts          # API client utilities for session management
├── tests/
│   └── multiplayer.spec.ts     # Main test suite
├── playwright.config.ts        # Playwright configuration
└── package.json
```

## API Helpers

The `session-api.ts` module provides:

- `createSession()` — Create a new game session
- `joinSession()` — Join an existing session
- `getSnapshot()` — Get current session state
- `startSession()` — Start an active session
- `bankPayment()` — Make a bank payment
- `transfer()` — Transfer between participants
- `completeSession()` — Complete a session
- `correctTransaction()` — Create a correction entry
- `getLedger()` — Query the immutable transaction ledger

All API calls respect host authorization, session versioning, and idempotency keys.

## Running Tests

### Prerequisites

1. Start the backend server:
   ```bash
   cd apps/server
   dotnet run --launch-profile http
   ```

2. The frontend will auto-start on port 5173 when tests run (configured in `playwright.config.ts`)

### Run All Tests

```bash
pnpm test:e2e
```

### Run Specific Test

```bash
pnpm test:e2e -- --grep "Bank payment"
```

### Run with UI

```bash
pnpm test:e2e -- --ui
```

### Debug Mode

```bash
pnpm test:e2e -- --debug
```

## Key Design Decisions

### Sequential Execution
Tests run with `workers: 1` to avoid port conflicts and ensure clean database state between tests.

### API-First Testing
Tests interact with the backend API directly rather than through the UI, ensuring we're testing the contract and business logic rather than UI implementation details.

### Multi-Page Scenarios
Tests use multiple `page` objects to simulate concurrent clients connecting to the same session, verifying real-time synchronization.

### Idempotency Keys
Every mutating command includes a unique idempotency key (`crypto.randomUUID()` or timestamp-based) to verify replay protection.

### Type Safety
TypeScript types are shared with the backend contracts (`SessionSnapshot`, `ParticipantCredentials`, etc.) to catch API changes early.

## Test Exit Criteria Met

### ✅ Two or more devices remain consistent through normal gameplay

- Tests verify that after a host action, all connected clients (`hostSnapshot`, `player1Snapshot`, `player2Snapshot`) see:
  - Same `sessionVersion`
  - Same participant list
  - Same account balances
  - Same game status

### ✅ Duplicate submissions do not duplicate transactions

- `Duplicate transaction submission is idempotent` test verifies:
  - First payment with idempotency key
  - Replay with same key does not increase balance twice
  - Both requests return the same final balance

### ✅ Refresh/reconnect preserves identity

- `Refresh during active game preserves participant identity` test verifies:
  - After page reload, participant can query session
  - Identity (participantId, displayName) is preserved
  - Balance remains correct after reload

### ✅ Host correction is auditable

- `Host correction creates compensating ledger entry` test verifies:
  - Payment creates initial ledger entry
  - Correction creates new entry with opposite effect
  - Full history is preserved and retrievable

## Future Coverage

- Mobile viewport testing
- Keyboard-only operation
- Accessibility (axe checks)
- QR code joining (when implemented)
- Session export data format validation
- Concurrent conflict resolution
- Network reconnection scenarios
- SignalR event ordering and delivery
