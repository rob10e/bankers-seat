# Banker's Seat

Banker's Seat is a browser-first companion app that acts as the banker and shared record keeper for tabletop games. Several players join a live game session from their own devices, while a host creates the session from a JSON-defined game template.

The product is designed for games that use money, periodic payments, player-owned assets, life-state attributes, or other values that are traditionally tracked with paper money, tokens, cards, or handwritten notes.

> **Project status:** active scaffold. Core web/server foundations are implemented and wired for local host/join/snapshot flows.

## Product goals

- Replace repetitive banker work without replacing the physical board game.
- Let players join quickly with a room code or QR code.
- Keep balances and custom player attributes synchronized in real time.
- Load game definitions from validated JSON templates.
- Support multiple editions of the same game as distinct templates.
- Allow game-specific logos and other images without coupling the application to any one game.
- Preserve a complete, human-readable ledger with safe undo and correction workflows.
- Start as a responsive web app and later ship as an installable hybrid mobile app.

## Recommended stack

| Area | Technology |
|---|---|
| Web client | React, TypeScript, Vite, MUI |
| Client state | TanStack Query for server state; Zustand for local UI state |
| Routing | React Router |
| Server | ASP.NET Core with SignalR |
| Persistence | SQLite for local/self-hosted use; PostgreSQL for hosted production |
| Validation | JSON Schema for templates; server-side domain validation for commands |
| Testing | Vitest, React Testing Library, Playwright, xUnit |
| Mobile path | Capacitor wrapping the responsive web application |
| Repository | pnpm workspace with a .NET solution |

The backend recommendation reflects the need for authoritative, concurrent financial operations. The web application remains React and TypeScript, while the server owns session state and ledger integrity.

## Repository shape

```text
bankers-seat/
├── AGENTS.MD
├── README.md
├── apps/
│   ├── server/
│   └── web/
├── packages/
│   ├── template-contracts/
│   ├── template-tools/
│   └── ui/
├── templates/
│   ├── schema/
│   ├── built-in/
│   └── samples/
├── tests/
│   ├── e2e/
│   ├── integration/
│   └── template-validation/
└── docs/
```

## Development and debug launch commands

- `pnpm run dev:web` starts the Vite web app.
- `pnpm run dev:server` starts the ASP.NET Core server.
- `pnpm run debug:web` starts the web app with explicit host and port for IDE launch profiles.
- `pnpm run debug:server` starts the server with `dotnet watch` and the `http` launch profile.
- `pnpm run templates:validate` scans `templates/**/template.json` and validates schema plus semantic rules.
- The web dev server proxies `/api/*` and `/hubs/*` to `http://localhost:5266`, so run `dev:server` before host/join flows.

## Current implementation snapshot

- Web app includes routes for home, templates, host setup, join, and game workspace.
- Web app includes responsive navigation: desktop tab navigation and mobile bottom navigation.
- App settings route is available for theme mode (system/light/dark) and refresh preferences.
- Dark mode is implemented via persisted app settings.
- Host and join forms call live backend endpoints and navigate to a session workspace.
- Session workspace polls authorized snapshots from the server.
- Server provides `/api/v1/templates`, `/api/v1/sessions`, `/api/v1/sessions/join`, `/api/v1/sessions/{sessionId}/reconnect`, `/api/v1/sessions/{sessionId}/snapshot`, `/api/v1/sessions/{sessionId}/transfer`, `/api/v1/sessions/{sessionId}/bank-payments`, `/api/v1/sessions/{sessionId}/bank-collections`, `/api/v1/sessions/{sessionId}/corrections`, `/api/v1/sessions/{sessionId}/ledger`, and `/api/v1/sessions/{sessionId}/export`.
- Server provides `/api/v1/templates`, `/api/v1/sessions`, `/api/v1/sessions/join`, `/api/v1/sessions/{sessionId}/reconnect`, `/api/v1/sessions/{sessionId}/snapshot`, `/api/v1/sessions/{sessionId}/transfer`, `/api/v1/sessions/{sessionId}/bank-payments`, `/api/v1/sessions/{sessionId}/bank-collections`, `/api/v1/sessions/{sessionId}/actions/{actionId}/execute`, `/api/v1/sessions/{sessionId}/corrections`, `/api/v1/sessions/{sessionId}/ledger`, and `/api/v1/sessions/{sessionId}/export`.
- SignalR hub is scaffolded at `/hubs/game` with session subscribe and resync methods.
- Template validation CLI is implemented and available as `pnpm templates:validate`.
- SQLite persistence baseline is implemented with EF Core, checked-in migrations, and transactional session/participant/account/template snapshot storage.
- Financial transfer, bank payment, bank collection, and correction command handling is implemented with session-version checks, idempotency, and immutable ledger transaction/posting persistence.
- Financial transfer, bank payment, bank collection, template financial action execution, and correction command handling is implemented with session-version checks, idempotency, and immutable ledger transaction/posting persistence.
- GitHub Actions CI workflow runs template validation, workspace lint/typecheck/tests, and .NET build/integration tests on pushes and pull requests.

## Planning document index

Start with `docs/INDEX.md`. The highest-value documents are:

1. `docs/01-product-vision.md`
2. `docs/04-system-architecture.md`
3. `docs/05-domain-model.md`
4. `docs/06-template-system.md`
5. `docs/07-template-json-specification.md`
6. `docs/08-realtime-and-concurrency.md`
7. `docs/15-roadmap.md`
8. `AGENTS.MD`

## Fundamental invariants

1. The server is authoritative for shared game state.
2. Money is stored as integers in the template's base unit; floating-point arithmetic is prohibited.
3. Every balance change produces an immutable ledger entry.
4. Every game session stores a snapshot of the template used to create it.
5. Commands that can be retried carry an idempotency key.
6. Templates are declarative data and cannot execute code.
7. A template is uniquely identified by template ID, edition, and template version.
8. Asset paths are relative, validated, and served from controlled template directories.

## Included examples

The planning package includes two original, generic sample templates:

- `generic-property-trading`
- `generic-life-journey`

They demonstrate the template system without copying proprietary game content, trademarks, artwork, or rule text.
