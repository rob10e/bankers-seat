# Product Vision

## Vision statement

Banker's Seat makes money handling and state tracking during physical board games fast, trustworthy, and accessible. Players continue using the real board, cards, pieces, and social interaction while the app handles balances, payments, shared records, and optional custom attributes.

## Problem

Many board games require one person to act as banker, repeatedly count money, resolve transfers, remember periodic payments, and track player-specific facts. This creates several problems:

- The banker spends less time playing.
- Manual money handling slows the game.
- Mistakes are difficult to reconstruct.
- Some editions have different denominations, starting values, or rules.
- Physical components can be lost or inconvenient.
- Player-specific state may be spread across cards, tokens, and notes.
- Accessibility needs can make small currency, crowded tables, or mental arithmetic difficult.

## Product promise

A host chooses a template, starts a room, and shares a code or QR code. Each player joins from a phone, tablet, or browser. The server maintains the authoritative game state. Templates define the game's banker-related setup and actions without requiring application code changes.

## Product principles

1. **Assist the table, do not replace it.** The physical game remains central.
2. **Trust before speed.** Every financial change is visible and auditable.
3. **Fast table interaction.** Common actions should take only a few taps.
4. **Edition-aware configuration.** Different editions are separate, selectable definitions.
5. **Template-driven extensibility.** New games can be supported through safe JSON.
6. **Mobile-first responsiveness.** The first web UI must work well on phones.
7. **No arbitrary template code.** Flexibility cannot compromise security.
8. **Recoverability.** Refreshes, temporary disconnections, and accidental actions must be survivable.
9. **Accessible by default.** Large touch targets, keyboard support, clear contrast, and screen-reader semantics are core requirements.
10. **Respect intellectual property.** Generic examples and user-supplied assets should be the default until licensed content is available.

## Primary personas

### Casual host

Wants to start a game quickly, invite family or friends, choose an edition, and avoid bookkeeping.

### Player

Wants a clear balance, quick transfer controls, personal attributes, and confidence that actions are correct.

### Dedicated banker

Wants a high-density console to issue payments, collect fees, correct mistakes, and review the ledger.

### Template creator

Wants to define or adapt a game using JSON, assets, schema validation, and useful error messages.

### Self-hosting administrator

Wants a Docker-deployable service, local persistence, backup, health checks, and predictable upgrades.

## Core success metrics

- Median time from opening the app to a joinable lobby.
- Median number of taps for common transactions.
- Percentage of sessions completed without manual state reset.
- Reconnection success rate.
- Command failure rate and duplicate-command prevention rate.
- Template validation success and quality of error messages.
- User-reported banker workload reduction.
- Accessibility audit results.
- Session completion and return usage.

## Non-goals for the initial product

- Full digital simulation of board movement, cards, dice, or proprietary rule systems.
- Real-money financial transactions.
- Gambling, wagering, or cash settlement.
- Arbitrary scripting in templates.
- A public marketplace at initial launch.
- Cross-session social networking.
- Voice control in the MVP.
- Automated recognition of physical cards or board state.
