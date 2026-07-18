# Copilot Instructions

Read and follow `/AGENTS.MD` before proposing or modifying code.

Key constraints:

- The server is authoritative for shared session state.
- Store money as integers, never floating-point values.
- Every accepted balance mutation creates immutable ledger records.
- A game uses a persisted snapshot of its template.
- Templates are declarative and cannot execute code.
- Mutating commands require authorization, validation, idempotency, and transactional behavior.
- React components must not contain domain calculations.
- SignalR hubs must remain thin.
- Add or update tests and documentation with every behavior or contract change.
- Keep template contracts free of React, MUI, and server-framework types.
