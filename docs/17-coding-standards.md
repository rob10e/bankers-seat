# Coding Standards

## General

- Optimize for correctness and clarity.
- Prefer explicit domain names over abbreviations.
- Keep functions focused.
- Avoid hidden side effects.
- Validate at trust boundaries.
- Use structured errors with stable codes.
- Keep documentation and code synchronized.

## TypeScript

- Strict mode.
- Semicolons.
- `unknown` instead of `any` at external boundaries.
- Discriminated unions for commands, events, and template operations.
- `readonly` for immutable contracts.
- Runtime validation for JSON.
- Named exports for reusable modules.
- Avoid default exports except route-level lazy modules when useful.
- Use path aliases with clear module boundaries.
- Do not use labels as IDs.

Example:

```ts
export type GameCommand =
  | PayFromBankCommand
  | CollectToBankCommand
  | TransferBetweenPlayersCommand;
```

## React

- Function components.
- Hooks follow standard rules.
- Business rules belong in domain/application modules, not components.
- Forms use schema-driven validation where appropriate.
- Use TanStack Query for server state.
- Use Zustand only for local UI state that truly needs cross-component scope.
- Prefer composition over deeply configurable mega-components.
- Use MUI theme tokens rather than ad hoc styling.
- Build mobile behavior at the same time as desktop behavior.

## C#

- Nullable reference types enabled.
- Async I/O.
- `CancellationToken` propagated.
- Thin controllers and hubs.
- Domain services do not depend on ASP.NET Core.
- Transactions handled in application/infrastructure boundaries.
- Use immutable records for command/event DTOs where practical.
- Use UTC.
- Structured logging.
- Explicit authorization checks.

## Formatting and naming

### TypeScript

- File names: kebab case.
- Components/types: PascalCase.
- Variables/functions: camelCase.
- Constants: camelCase unless genuinely global/static.
- JSON: camelCase.
- Template IDs: lowercase kebab case.

### C#

- Standard .NET naming.
- One primary public type per file unless tightly related records improve clarity.
- Avoid suffixing every asynchronous method with `Async` only if the repository consistently adopts a different documented convention; otherwise use standard `Async`.

## Error codes

Use stable kebab-case codes:

- `session-not-found`
- `room-locked`
- `insufficient-funds`
- `stale-session-version`
- `duplicate-idempotency-key`
- `unauthorized-command`
- `invalid-template-action`

Messages may change or localize; codes are contracts.

## Comments

Comments explain why, constraints, or non-obvious trade-offs. Do not restate obvious code.

## Dependencies

Before adding a dependency:

1. Confirm the problem is real.
2. Evaluate maintenance and license.
3. Check bundle/runtime cost.
4. Consider security implications.
5. Document major decisions.
6. Add tests around the integration boundary.

## Database

- Migrations checked in.
- No direct production schema editing.
- Money is 64-bit integer.
- Use constraints and unique indexes for invariants where possible.
- Never silently cascade-delete ledger history.

## Documentation

- Markdown.
- Mermaid for diagrams.
- Stable relative links.
- Schema examples must validate in CI.
- Record major architecture decisions as ADRs.
