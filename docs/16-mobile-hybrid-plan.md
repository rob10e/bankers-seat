# Mobile Hybrid Plan

## Strategy

Build a mobile-first responsive web application and Progressive Web App first. Package the same application with Capacitor when native distribution or native features provide clear value.

This approach preserves the React and TypeScript codebase and avoids creating a separate React Native application.

## Web-first requirements

- Responsive layouts from 320 px width upward.
- Touch targets of at least 44 by 44 CSS pixels.
- Safe-area CSS support.
- virtual keyboard-aware forms.
- no hover-only interactions.
- installable manifest.
- service worker for app-shell caching.
- reconnect-aware UX.
- reduced-motion support.
- orientation testing.

## Native capabilities by phase

### Initial hybrid shell

- App identity and icons.
- deep links for room invitations.
- native share sheet.
- secure credential storage.
- network status.
- haptics for accepted/rejected actions.

### Later

- QR scanner.
- local notifications for turn/payday reminders.
- keep-awake during games.
- optional local-network discovery.
- crash reporting.
- platform-specific accessibility refinements.

## Offline behavior

The real-time multiplayer game remains server-authoritative. The app shell may load offline, but mutating shared state requires a connection.

Potential later mode:

- Single-device local banker session.
- Local persistence.
- Explicit conversion/upload into a hosted session only through a conflict-safe import flow.

Do not queue arbitrary multiplayer financial commands offline and replay them blindly.

## Authentication storage

- Prefer platform secure storage for long-lived reconnect/session credentials.
- Web fallback must use a carefully chosen secure storage model.
- Provide logout/forget-room behavior.
- Do not include secrets in deep links.

## App links

A join link should contain only a room locator, such as:

```text
https://example.invalid/join/ABCD12
```

The player still receives a separate protected credential after joining.

## Capacitor project organization

```text
apps/
  web/
  mobile/
    capacitor.config.ts
    android/
    ios/
```

Keep native-specific code behind small adapters:

- `ShareService`
- `QrScannerService`
- `SecureStorageService`
- `HapticsService`
- `NetworkStatusService`

The React feature modules consume interfaces, not Capacitor APIs directly.

## Release considerations

- App-store privacy disclosures.
- account deletion if accounts are introduced.
- platform deep-link verification.
- signing keys.
- mobile analytics consent.
- update compatibility between mobile shell and server protocol.
- minimum supported OS policy.
- safe handling when the server requires a newer client protocol.
