# ADR 0004: Capacitor Mobile Strategy

- Status: Accepted
- Date: 2026-07-17

## Context

The product begins as a React web app but is expected to become a mobile hybrid app. Maintaining separate web and native UI implementations would increase effort.

## Decision

Build a responsive PWA first and use Capacitor for later native packaging. Native features are accessed through small adapter interfaces.

## Consequences

### Positive

- Maximum React/TypeScript reuse.
- Faster mobile delivery.
- Incremental native capabilities.
- Shared UI and tests.

### Negative

- Some native interactions may need platform-specific work.
- WebView performance and platform behavior require testing.
- Complex native features may eventually justify dedicated modules.
