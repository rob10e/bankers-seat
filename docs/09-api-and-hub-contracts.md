# API and Hub Contracts

## Implementation status snapshot (2026-07-18)

Implemented in scaffold:

- `GET /api/v1/templates`
- `POST /api/v1/sessions`
- `POST /api/v1/sessions/join`
- `POST /api/v1/sessions/{sessionId}/reconnect`
- `GET /api/v1/sessions/{sessionId}/snapshot`
- `POST /api/v1/sessions/{sessionId}/transfer`
- `POST /api/v1/sessions/{sessionId}/bank-payments`
- `POST /api/v1/sessions/{sessionId}/bank-collections`
- `POST /api/v1/sessions/{sessionId}/actions/{actionId}/execute`
- `POST /api/v1/sessions/{sessionId}/corrections`
- `GET /api/v1/sessions/{sessionId}/ledger`
- `GET /api/v1/sessions/{sessionId}/export`
- SignalR hub path `/hubs/game` with `SubscribeSession` and `RequestResync`

Planned but not implemented yet:

- Detailed template endpoint by version
- Template asset serving endpoint
- Admin template rescan endpoint
- Full command/event hub surface listed below

## API conventions

- Base path: `/api/v1`
- JSON fields use camel case.
- Timestamps use UTC ISO 8601.
- Errors use a consistent problem-details shape.
- Mutating requests accept correlation and idempotency identifiers.
- Public contracts are additive within a major API version.

## Template endpoints

### `GET /api/v1/templates`

Returns validated catalog entries.

Query options may include:

- Search text.
- Tag.
- Player count.
- Template ID.
- Edition ID.
- Include invalid entries for authorized administrators.

### `GET /api/v1/templates/{templateId}/editions/{editionId}/versions/{templateVersion}`

Returns detailed setup information, not unrestricted server paths.

### `GET /api/v1/template-assets/{assetPackageId}/{assetPath}`

Returns a validated asset with safe headers.

### `POST /api/v1/admin/templates/rescan`

Triggers an authorized rescan and returns validation diagnostics.

## Session endpoints

### `POST /api/v1/sessions`

Creates a lobby.

Request:

```json
{
  "templateId": "generic-property-trading",
  "editionId": "standard-edition",
  "templateVersion": "1.0.0",
  "hostDisplayName": "Rob",
  "sessionOptions": {}
}
```

Response includes:

- Session ID.
- Room code.
- host participant ID.
- reconnect credential.
- initial session snapshot.
- SignalR connection information.

### `POST /api/v1/sessions/join`

Request:

```json
{
  "roomCode": "ABCD12",
  "displayName": "Player Two",
  "identityKey": "blue"
}
```

### `POST /api/v1/sessions/{sessionId}/reconnect`

Exchanges a valid reconnect credential for refreshed access.

### `GET /api/v1/sessions/{sessionId}/snapshot`

Returns the authorized current session view.

### `POST /api/v1/sessions/{sessionId}/transfer`

Host-authorized transfer between participant accounts. Requires actor headers, expected session version, and idempotency key.

### `POST /api/v1/sessions/{sessionId}/bank-payments`

Host-authorized bank-to-participant payment command with expected session version and idempotency key.

### `POST /api/v1/sessions/{sessionId}/bank-collections`

Host-authorized participant-to-bank collection command with expected session version and idempotency key.

### `POST /api/v1/sessions/{sessionId}/actions/{actionId}/execute`

Host-authorized template action execution command. Current implementation supports financial action operations (`bank-to-player`, `player-to-bank`, and `player-to-player`) with `single-player`, `two-players`, and `all-players` scopes where compatible; `set-field`/`increment-field` operations with `single-player` and `all-players` scopes; and `composite` multi-step actions composed of those supported step types. Requires expected session version plus idempotency key.

### `POST /api/v1/sessions/{sessionId}/corrections`

Host-authorized compensating correction of a prior ledger transaction. Requires actor headers, expected session version, idempotency key, and reason.

### `GET /api/v1/sessions/{sessionId}/ledger`

Supports cursor pagination and filters.

### `GET /api/v1/sessions/{sessionId}/export`

Returns an authorized JSON export.

## SignalR hub

Suggested path:

```text
/hubs/game
```

### Client-to-server methods

- `SubscribeSession`
- `StartSession`
- `PauseSession`
- `ResumeSession`
- `CompleteSession`
- `TransferHost`
- `RemoveParticipant`
- `PayFromBank`
- `CollectToBank`
- `TransferBetweenPlayers`
- `ExecuteTemplateAction`
- `UpdatePlayerField`
- `CorrectTransaction`
- `RequestResync`

Each mutating method receives a command envelope.

### Server-to-client events

- `SessionSnapshot`
- `ParticipantJoined`
- `ParticipantPresenceChanged`
- `SessionStatusChanged`
- `HostChanged`
- `TransactionPosted`
- `TransactionCorrected`
- `PlayerFieldChanged`
- `SessionResyncRequired`
- `CommandRejected`

## Authorization matrix

| Action | Host | Banker | Player | Spectator |
|---|---:|---:|---:|---:|
| Start/pause/complete | Yes | No | No | No |
| Transfer host | Yes | No | No | No |
| Bank payment | Yes | Yes | Template policy | No |
| Pay self to bank | Yes | Yes | Yes | No |
| Transfer own funds | Yes | Yes | Yes | No |
| Transfer another player's funds | Yes | Template policy | No | No |
| Update own field | Yes | Yes | Field policy | No |
| Update another field | Yes | Field policy | No | No |
| Correct transaction | Yes | Optional | No | No |
| View public state | Yes | Yes | Yes | Yes |
| View private fields | Policy | Policy | Owner only | No |

The server derives effective authorization from role, ownership, session state, and template policy.

## Snapshot shape

A session snapshot should contain:

- Session metadata and version.
- Safe template snapshot view.
- Current participant list and presence.
- Authorized accounts and balances.
- Authorized player field values.
- Recent ledger page.
- Current actor permissions.
- Connection/recovery hints.
- Server time.

Do not send private field values or secrets to unauthorized clients and expect the UI to hide them.
