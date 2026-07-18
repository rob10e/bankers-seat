# UI and UX Plan

## Design goals

- Fast one-handed phone use.
- Clear enough to pass a device around the table.
- Large balance display.
- Minimal typing during gameplay.
- Safe confirmation and correction.
- Equal support for player self-service and a dedicated banker.
- Responsive web UI that becomes the mobile hybrid UI later.

## Primary routes

```text
/
  Home and recent rooms
/templates
  Template catalog
/templates/:templateKey
  Template details
/host/new
  Session setup
/join
  Room-code entry
/game/:sessionId
  Main game workspace
/game/:sessionId/ledger
  Full ledger
/game/:sessionId/manage
  Host and banker controls
/settings
  Local preferences
```

## Core screens

### Home

- Start a game.
- Join a game.
- Reconnect to recent game.
- Installation/offline status.
- Template/catalog health for administrators.

### Template catalog

Card content:

- Logo or fallback.
- Game name.
- Edition.
- Template version.
- Player range.
- Short description.
- Tags.

Filters:

- Search.
- Player count.
- Tags.
- Installed source.
- Edition.

### Host setup

- Selected game and edition.
- Player count expectations.
- Allowed session options.
- Starting-balance preview.
- Fields and actions preview.
- Create room button.

### Lobby

- Room code and QR code.
- Participant list.
- Identity selections.
- Connection status.
- Lock/unlock.
- Start game.
- Host transfer.
- Remove participant.

### Player dashboard

- Large current balance.
- Connection/sync status.
- Custom fields.
- Quick actions.
- Pay bank.
- Transfer to player.
- Personal ledger.
- Session status.

### Banker console

- Player grid with balances.
- Search/select player.
- Pay, collect, and transfer actions.
- Batch actions.
- Template action groups.
- Recent transaction feed.
- Correction entry point.
- Pause/complete controls.

### Ledger

- Virtualized chronological list.
- Filters by player, action, type, actor, and date.
- Detail drawer showing postings.
- Correction linkage.
- Export access.

## Responsive layouts

### Phone

- Bottom navigation.
- Single-column cards.
- Full-screen transaction sheets.
- Sticky balance header.
- Large number keypad.
- Minimal simultaneous detail.

### Tablet

- Split view with players and action panel.
- Persistent recent ledger.
- Suitable for dedicated banker use.

### Desktop

- Three-pane banker workspace.
- Keyboard shortcuts.
- Dense but readable ledger.
- Multi-select batch actions.

## Transaction flow

1. Choose action.
2. Choose source/destination when required.
3. Enter amount or use template fixed amount.
4. Add optional note.
5. Review impact.
6. Submit once with disabled duplicate button.
7. Show pending state.
8. Confirm accepted result or actionable error.
9. Provide correction/undo path when permitted.

## Custom field controls

| Type | Control |
|---|---|
| Boolean | Switch or checkbox |
| Counter | Stepper with direct entry |
| Integer | Numeric field |
| Currency | Currency keypad |
| Text | Short text input |
| Enum | Select, segmented control, or chips |

The template may offer presentation hints, but the application chooses safe accessible controls.

## Graphics

- Logo on catalog and lobby.
- Optional thumbnail.
- Optional subtle background art.
- Field/action icons by asset key.
- No image may reduce contrast or obscure controls.
- Provide fallback graphics and alt text.
- Use object-fit and bounded dimensions to avoid layout shifts.

## Status language

Use explicit states:

- Connecting.
- Reconnecting.
- Synchronized.
- Waiting for host.
- Game paused.
- Command pending.
- Payment completed.
- Payment rejected.
- State refreshed.

## Safety UX

- Disable repeat submission while a command is pending.
- Confirmation for high-value, batch, correction, host-transfer, and game-completion actions.
- Show exact source, destination, amount, and resulting balance when possible.
- Require a correction reason.
- Avoid ambiguous labels such as “OK” for financial actions.
