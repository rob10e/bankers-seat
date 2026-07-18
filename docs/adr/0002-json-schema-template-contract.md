# ADR 0002: JSON Schema Template Contract

- Status: Accepted
- Date: 2026-07-17

## Context

Game definitions need to be discoverable, editable, versioned, and validated across TypeScript and C#.

## Decision

Use JSON files validated by a canonical JSON Schema plus server-side semantic validation. Templates are declarative and use a closed set of operation types.

## Consequences

### Positive

- Tooling support.
- Language-neutral contract.
- Clear validation.
- Safe extensibility.
- Easy packaging and source control.

### Negative

- Some advanced rules cannot be expressed initially.
- Schema evolution requires version management.
- Semantic validation remains necessary beyond JSON Schema.
