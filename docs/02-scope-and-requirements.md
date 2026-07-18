# Scope and Requirements

## MVP functional requirements

### Template catalog

- Discover built-in and administrator-installed JSON templates.
- Validate every template before it appears in the catalog.
- Display game name, edition, template version, description, player range, and logo.
- Distinguish editions as separate selectable entries.
- Report invalid templates to administrators without breaking valid templates.
- Support a generic fallback graphic when no logo is supplied.

### Session creation

- Let a host select a template and create a game.
- Generate a short room code and QR code.
- Allow the host to choose permitted template options.
- Persist a snapshot of the selected template.
- Support the template-defined minimum and maximum player counts.
- Let the host lock the lobby and start the game.

### Joining

- Join by room code, link, or QR code.
- Enter a display name and choose an available color/avatar.
- Receive a reconnect credential.
- Prevent duplicate active names within a room unless the host explicitly allows them.
- Rejoin after refresh or temporary network loss.

### Player state

- Show current balance prominently.
- Show custom fields defined by the template.
- Respect visibility and editability rules.
- Allow host-approved edits to tracked fields.
- Support field types: boolean, integer, text, enum, currency, and counter.

### Banking

- Bank-to-player payment.
- Player-to-bank payment.
- Player-to-player transfer.
- Template-defined one-tap payment or collection actions.
- Payday or periodic payment actions.
- Multi-player batch payments.
- Optional overdraft policy.
- Transaction note.
- Confirmation for unusually large or destructive actions.

### Ledger and corrections

- Immutable chronological ledger.
- Per-player and session-wide views.
- Actor, timestamp, amount, source, destination, action type, and note.
- Safe correction by compensating transaction.
- Optional host-only undo for the latest eligible action.
- Clear visual relationship between an original transaction and its correction.

### Session lifecycle

- Lobby, active, paused, completed, and archived states.
- Host pause and resume.
- Host transfer to another connected player.
- Completion summary.
- Export session data as JSON.
- Optional printable summary later.

## Post-MVP functional requirements

- User accounts and saved profile preferences.
- Cloud synchronization across devices.
- Template import and export packages.
- Visual template editor.
- Public or private template library.
- Team or household template sharing.
- Saved game resume across multiple days.
- Custom action groups and contextual menus.
- Per-player PINs.
- Spectator mode.
- Remote host administration.
- Local-network discovery.
- Localization and multiple currencies/number formats.
- Optional offline single-device banker mode.

## Non-functional requirements

### Correctness

- Atomic transactions.
- Deterministic command processing.
- Idempotent retries.
- Versioned state.
- No floating-point money.
- Active sessions immune to template file changes.

### Performance

- Common commands acknowledged in under 300 ms on a normal local network.
- Real-time updates visible to connected players shortly after acceptance.
- Initial session snapshot remains practical for sessions with at least 20 players and 10,000 ledger entries.
- Large ledgers use pagination or virtualization.

### Availability and recovery

- Browser refresh does not lose player identity when reconnect credentials are valid.
- Server restart can restore persisted sessions.
- Database backups are documented.
- A client detects stale state and resynchronizes automatically.

### Security

- Authorization on every mutation.
- Rate limiting for join and command endpoints.
- No executable template content.
- Safe asset serving.
- HTTPS for hosted deployments.
- Secrets stored outside source control.

### Accessibility

- WCAG 2.2 AA target.
- Touch targets suitable for phone use.
- Full keyboard navigation.
- Screen-reader labels.
- Reduced-motion mode.
- No color-only meaning.

### Maintainability

- Modular architecture.
- Versioned contracts.
- Automated schema validation.
- Unit, integration, and end-to-end coverage.
- Architecture decision records for major choices.
- Documentation updated with behavior changes.

## Assumptions

- Players normally have access to a browser-capable device.
- The game is social and turn-based, not high-frequency.
- The server can be hosted locally or remotely.
- Templates represent banker-related behavior, not every game rule.
- All balances use integer base units.
- The host has elevated permissions but cannot erase audit history.
