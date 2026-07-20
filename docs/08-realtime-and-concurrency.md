# Real-Time and Concurrency

## Goals

- All players see accepted changes quickly.
- Duplicate network retries do not duplicate transactions.
- Concurrent commands cannot corrupt balances.
- Reconnecting clients recover cleanly.
- Event order is explicit and testable.

## Transport

Use SignalR for:

- Session subscription.
- Mutating gameplay commands.
- Presence notifications.
- Accepted domain events.
- Snapshot/resynchronization requests.

Use HTTP for:

- Template catalog.
- Session creation.
- Initial join.
- Asset delivery.
- Administrative template operations.
- Exports.

Commands may also be exposed through HTTP for automation or diagnostics, but one authoritative application service must process both transports.

## Command envelope

```json
{
  "protocolVersion": 1,
  "commandId": "01JEXAMPLECOMMAND",
  "idempotencyKey": "client-generated-unique-value",
  "sessionId": "01JEXAMPLESESSION",
  "expectedSessionVersion": 42,
  "type": "transfer-between-players",
  "payload": {
    "fromParticipantId": "01JFROM",
    "toParticipantId": "01JTO",
    "amount": 200,
    "note": "Rent"
  }
}
```

## Event envelope

```json
{
  "protocolVersion": 1,
  "eventId": "01JEXAMPLEEVENT",
  "sessionId": "01JEXAMPLESESSION",
  "sessionVersion": 43,
  "type": "transaction-posted",
  "occurredAtUtc": "2026-07-17T12:00:00Z",
  "correlationId": "01JEXAMPLECOMMAND",
  "payload": {}
}
```

## Ordering

- Each accepted state change receives the next `sessionVersion`.
- Events for a session are published in session-version order.
- Clients track the latest applied version.
- If the next event version is not exactly current plus one, the client pauses incremental application and requests resynchronization.

## Idempotency

Idempotency records are scoped to session and actor.

Store:

- Actor participant ID.
- Idempotency key.
- Command type.
- Request hash.
- Accepted result or rejection category.
- Created time.
- Expiration policy.

If the same key is reused with a different request hash, reject it as a client error.

## Optimistic concurrency

The command may carry `expectedSessionVersion`.

Recommended behavior:

- Commands requiring strict current context reject stale versions.
- Simple independent transactions may be retried server-side against the newest version if their domain preconditions remain valid.
- The accepted result always returns the actual resulting session version.

The implementation should begin with strict version checks for clarity, then selectively relax them only with tests.

## Transaction boundary

A mutation must atomically persist:

- Account changes.
- Ledger transaction and postings.
- Player field changes.
- Session version.
- Idempotency record.
- Outbox event records when the outbox pattern is used.

Publish real-time events only after commit. In the current implementation, accepted mutating commands broadcast the latest `SessionSnapshot` to the session SignalR group after commit so connected devices can refresh immediately.

## Reconnection

1. Client detects transport loss.
2. UI changes to reconnecting state.
3. SignalR reconnects.
4. Client reauthenticates with participant credential.
5. Client sends last applied session version.
6. Server sends missing events if retained and reasonably small.
7. Otherwise server sends or instructs the client to fetch a fresh snapshot.
8. Client replaces server-derived state and resumes.

## Presence

Presence is transient and must not be the source of financial truth.

Track:

- Connection count per participant.
- Last seen time.
- Connected/disconnected display state.
- Host device state.

A participant may have multiple tabs or devices. Presence changes do not invalidate account ownership.

## Host disconnect

Recommended MVP behavior:

- Keep the host role for a grace period.
- Allow reconnect.
- Permit a host-authorized co-banker to continue normal actions.
- After a configurable timeout, allow explicit host transfer using a secure recovery rule.
- Never assign host solely based on first connected client without audit.

## Failure responses

Return structured errors:

```json
{
  "code": "insufficient-funds",
  "message": "Rob does not have enough funds for this payment.",
  "details": {
    "available": 150,
    "required": 200
  },
  "currentSessionVersion": 43,
  "retryable": false
}
```

Avoid exposing server stack traces to clients.
