# Planning Documentation Index

## Product and scope

- `01-product-vision.md` — product purpose, principles, personas, and success measures.
- `02-scope-and-requirements.md` — functional and non-functional requirements.
- `03-user-stories-and-flows.md` — primary stories, journeys, and acceptance criteria.

## Technical design

- `04-system-architecture.md` — target architecture and deployment topology.
- `05-domain-model.md` — core entities, commands, events, and invariants.
- `06-template-system.md` — discovery, validation, versioning, assets, and lifecycle.
- `07-template-json-specification.md` — field-level template contract.
- `08-realtime-and-concurrency.md` — SignalR protocol, ordering, reconnect, and idempotency.
- `09-api-and-hub-contracts.md` — proposed HTTP and hub surface.
- `10-data-storage.md` — persistence model, migrations, retention, and backup.

## Experience, quality, and operations

- `11-ui-ux-plan.md` — screens, components, responsive behavior, and error states.
- `12-testing-strategy.md` — unit, integration, schema, concurrency, and end-to-end tests.
- `13-security-privacy-and-ip.md` — threat model, privacy, asset safety, and intellectual property.
- `14-deployment-and-observability.md` — environments, configuration, logs, health checks, and hosting.
- `15-roadmap.md` — phased delivery plan.
- `16-mobile-hybrid-plan.md` — PWA-first and Capacitor migration.
- `17-coding-standards.md` — code conventions and repository rules.
- `18-accessibility-plan.md` — WCAG target and table-side usability.
- `19-risks-and-open-decisions.md` — important risks and recommended defaults.
- `20-glossary.md` — shared product and engineering vocabulary.

## Architecture decisions

- `adr/0001-server-authoritative-ledger.md`
- `adr/0002-json-schema-template-contract.md`
- `adr/0003-template-snapshot-per-session.md`
- `adr/0004-capacitor-mobile-strategy.md`

## Machine-readable template assets

- `../templates/schema/game-template.schema.json`
- `../templates/samples/generic-property-trading/template.json`
- `../templates/samples/generic-life-journey/template.json`
