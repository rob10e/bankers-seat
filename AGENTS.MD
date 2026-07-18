# AGENTS.MD

## Purpose

This repository contains Banker's Seat, a browser-first multiplayer companion application for tabletop games. It acts as the banker and shared record keeper. The system uses JSON templates to define denominations, starting balances, payments, player fields, actions, edition metadata, and game-specific graphics.

This file is the primary operating guide for coding agents. Read it before changing code, schemas, tests, or documentation.

## Product boundary

The application assists a physical board game; it is not intended to reproduce an entire proprietary board game digitally.

The application may:

- Track player balances and bank transactions.
- Track custom player state such as homes, children, jobs, properties, debt, or status flags.
- Apply template-defined actions and periodic payments.
- Display template-provided logos and supporting images.
- Record an audit trail and permit controlled corrections.
- Support multiple editions of a game through separate template identities.

The application must not:

- Execute arbitrary JavaScript, TypeScript, C#, shell commands, or expressions from templates.
- Infer or automate rules that are not explicitly represented by a safe declarative operation.
- Silently mutate balances or custom fields.
- Depend on copyrighted logos, artwork, names, or rule text in the core application.
- Treat a client-provided balance as authoritative.

## Read these documents first

Before implementing a feature, read the relevant files:

- `docs/04-system-architecture.md`
- `docs/05-domain-model.md`
- `docs/06-template-system.md`
- `docs/07-template-json-specification.md`
- `docs/08-realtime-and-concurrency.md`
- `docs/09-api-and-hub-contracts.md`
- `docs/12-testing-strategy.md`
- `docs/17-coding-standards.md`

For a cross-cutting architectural change, also add or update an ADR under `docs/adr`.

## Recommended repository layout

```text
apps/
  server/                  ASP.NET Core API, SignalR hubs, persistence
  web/                     React and TypeScript application
packages/
  template-contracts/      TypeScript types and JSON Schema bindings
  template-tools/          Template validation and author tooling
  ui/                      Reusable React components and theme
templates/
  schema/                  Canonical JSON Schema
  built-in/                First-party or licensed templates
  samples/                 Original generic examples
tests/
  e2e/
  integration/
  template-validation/
docs/
```

Keep domain concepts independent from React and MUI. Shared template contracts must not import UI types.

## Architecture rules

### Server authority

The server is the source of truth for:

- Session membership.
- Player identity within a session.
- Account balances.
- Ledger order.
- Field values.
- Session lifecycle.
- Permissions.
- Template snapshots.
- Command acceptance and rejection.

Clients may use optimistic presentation only when rollback behavior is implemented. The server response or emitted event always wins.

### Financial integrity

- Represent all monetary values as signed integers in the template's base unit.
- Never use JavaScript floating-point math for monetary values.
- Never directly set a player's current balance from the client.
- Apply balance changes through domain commands that create ledger entries.
- A transfer must debit and credit atomically.
- A rejected command must not partially mutate state.
- Corrections must reference the original ledger entry and create compensating entries; do not delete history.
- Enforce overdraft behavior from the session/template policy.
- Ensure command handlers are idempotent when given the same actor and idempotency key.

### Template immutability

When a game starts:

1. Resolve the selected template.
2. Validate it.
3. Calculate its content hash.
4. persist a complete template snapshot with the game session.
5. Use that snapshot for the lifetime of the session.

Editing or replacing a template file must never alter an active or completed game.

### Template safety

Templates are untrusted input.

- Validate against the canonical JSON Schema.
- Reject unknown operation types.
- Reject absolute paths and parent-directory traversal.
- Restrict asset extensions and size.
- Do not fetch arbitrary remote assets by default.
- Do not evaluate expressions supplied by templates.
- Put reasonable limits on fields, actions, denominations, image count, and text length.
- Return validation errors with precise JSON paths and actionable messages.

### Real-time consistency

- All mutating commands go to the server.
- Use a monotonically increasing session version.
- Include the resulting version in every event.
- Detect missed versions and request a fresh snapshot.
- Preserve event ordering per session.
- Reconnect using a short-lived reconnect token, not a player name alone.
- Separate commands from events. Commands express intent; events state accepted facts.

## Implementation workflow

For each feature:

1. Identify the user story and acceptance criteria.
2. Update or add domain contracts before UI implementation.
3. Add server-side validation and authorization.
4. Add unit tests for domain behavior.
5. Add integration tests for persistence or hub behavior.
6. Implement the client flow.
7. Add Playwright coverage for critical user journeys.
8. Update documentation and schemas in the same change.
9. Run formatting, linting, type checking, and tests.

Do not create placeholder handlers that return success without enforcing domain rules.

## Suggested commands

These commands are target conventions for the future repository:

```bash
pnpm install
pnpm dev
pnpm lint
pnpm typecheck
pnpm test
pnpm test:e2e
pnpm templates:validate

dotnet restore
dotnet build
dotnet test
dotnet run --project apps/server
```

If actual scripts differ, update this file and `README.md` together.

## React and TypeScript rules

- Use TypeScript strict mode.
- Keep semicolons.
- Prefer named exports for reusable modules.
- Use function components and hooks.
- Keep server state in TanStack Query.
- Keep ephemeral local UI state in component state or Zustand.
- Do not duplicate authoritative session state into multiple stores.
- Use discriminated unions for command and event payloads.
- Validate external JSON at runtime before narrowing types.
- Avoid `any`; use `unknown` at trust boundaries.
- Keep business calculations outside React components.
- Build accessible controls with keyboard support and clear labels.
- Design for narrow mobile screens from the first implementation.

## ASP.NET Core rules

- Use explicit request and response DTOs.
- Keep SignalR hubs thin; delegate to application services.
- Keep domain mutations in transaction-scoped command handlers.
- Pass `CancellationToken` through asynchronous calls.
- Use structured logging with session ID, actor ID, command ID, and correlation ID.
- Never log reconnect tokens, access tokens, or imported template contents containing personal data.
- Use optimistic concurrency or an equivalent session version check.
- Use UTC timestamps and serialize them in ISO 8601 format.

## Naming

- TypeScript files: `kebab-case.ts` and `kebab-case.tsx`.
- React components and TypeScript types: `PascalCase`.
- Variables and functions: `camelCase`.
- C# types and public members: `PascalCase`.
- JSON fields: `camelCase`.
- Template IDs: lowercase kebab case.
- Stable field/action IDs: lowercase kebab case.
- Database table names: follow the selected ORM convention consistently.

IDs are machine contracts. Labels are display text. Never use a display label as a durable identifier.

## API and event compatibility

- Version public HTTP routes under `/api/v1`.
- Version hub contracts with a protocol version.
- Additive changes are preferred.
- Renaming or changing the meaning of an event is a breaking change.
- Unknown event types must fail visibly in development and trigger safe resynchronization in production.
- Persist the application version and template schema version for diagnostics.

## Testing obligations

Every balance-changing command requires tests for:

- Success.
- Insufficient funds when overdrafts are prohibited.
- Allowed negative balance when overdrafts are permitted.
- Duplicate idempotency key.
- Unauthorized actor.
- Stale session version.
- Atomic failure.
- Correct ledger entries.
- Correct real-time event.
- Reconnection/state resync where relevant.

Every template feature requires:

- Valid example coverage.
- Invalid example coverage.
- Schema compatibility coverage.
- Path and asset-security tests.
- Duplicate ID tests.
- Active-session snapshot tests.

## Security and privacy

- Treat room codes as discoverability aids, not full authentication.
- Use a separate session/player secret for reconnect and command authorization.
- Rate-limit join attempts and mutating commands.
- Make host-only actions explicit.
- Use secure cookies or protected local storage appropriate to the deployment model.
- Apply standard CSRF protections when cookie authentication is used.
- Enforce HTTPS in hosted production.
- Escape or sanitize all template-provided text before rendering.
- Serve assets with safe content types and no script execution.

## Accessibility and usability

- Meet WCAG 2.2 AA as a product target.
- Do not rely on color alone for transaction type or player identity.
- Support screen readers, keyboard navigation, reduced motion, and scalable text.
- Confirm destructive or high-impact actions.
- Provide an undo/correction route that explains its effect.
- Show large, clear balances suitable for viewing around a table.

## Documentation requirements

Update documentation whenever a change affects:

- User-visible behavior.
- Template fields or schema.
- Commands, events, or API endpoints.
- Persistence.
- Security assumptions.
- Deployment.
- Mobile compatibility.
- Accessibility.

Use Mermaid diagrams when a flow or relationship is easier to understand visually.

## Dependency policy

- Prefer mature, actively maintained libraries.
- Add a dependency only when it materially reduces risk or maintenance.
- Keep shared contracts framework-agnostic.
- Avoid libraries that require templates to execute arbitrary code.
- Document significant dependency choices in an ADR.
- Pin reproducible lockfiles and enable automated dependency updates.

## Git and pull request guidance

Use focused commits and small reviewable changes. Suggested commit prefixes:

- `feat:`
- `fix:`
- `docs:`
- `test:`
- `refactor:`
- `build:`
- `chore:`

A pull request should state:

- User outcome.
- Architectural impact.
- Template/API compatibility.
- Security implications.
- Tests performed.
- Screenshots for visible UI changes.
- Documentation changed.

## Definition of done

A change is complete only when:

- Acceptance criteria are met.
- Domain invariants remain true.
- Validation and authorization are present.
- Tests cover important success and failure paths.
- Type checking and linting pass.
- Documentation is current.
- No secrets or unlicensed assets are included.
- Mobile-width behavior has been considered.
- Error messages are actionable.
- Telemetry does not expose sensitive values.

## Priority order when requirements conflict

1. Data integrity and player trust.
2. Security and privacy.
3. Correct session synchronization.
4. Accessibility.
5. Clear user experience.
6. Template flexibility.
7. Developer convenience.
8. Visual polish.
