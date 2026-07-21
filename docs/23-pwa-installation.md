# Progressive Web App (PWA) Installation Support

Banker's Seat supports installation as a standalone Progressive Web App on desktop and mobile browsers, enabling:

- **Offline capability** — Cached assets remain accessible without network connectivity
- **Standalone mode** — App runs in full-screen without browser chrome
- **App shortcuts** — Quick access to "Create Game" and "Join Game" on home screen
- **Update detection** — Automatic check for new versions with user-friendly prompts
- **Graceful degradation** — Core UI loads offline; API operations handle failures gracefully

## Installation Methods

### Desktop (Chrome/Edge/Firefox)

1. Visit `https://bankers-seat.example.com` in Chrome, Edge, or Firefox
2. Click the install icon in the address bar (↗ or ⬇)
3. Click "Install" in the prompt
4. App launches in standalone window

### iOS (Safari)

1. Visit `https://bankers-seat.example.com` in Safari
2. Tap Share button
3. Scroll and tap "Add to Home Screen"
4. Confirm app name and tap "Add"
5. App icon appears on home screen

### Android (Chrome)

1. Visit `https://bankers-seat.example.com` in Chrome
2. Tap menu (⋯) → "Install app"
3. Tap "Install"
4. App launches in standalone mode

## Architecture

### Manifest (public/manifest.json)

Defines app metadata, icons, display mode, and shortcuts:

```json
{
  "name": "Banker's Seat",
  "short_name": "Banker's Seat",
  "display": "standalone",
  "theme_color": "#667eea",
  "start_url": "/",
  "scope": "/",
  "icons": [...]
}
```

**Key fields:**
- `display: "standalone"` — Hides browser UI
- `start_url: "/"` — Entry point when app launches
- `theme_color` — Affects system UI chrome
- `shortcuts` — Quick-access home screen actions

### Service Worker (public/service-worker.js)

Implements offline caching and request interception:

**Caching strategies:**

1. **Network-first** — API calls (`/api/`, `/hubs/`)
   - Try network first
   - Cache response if successful
   - Fall back to cached response if offline
   - Return 503 error if no cache available

2. **Cache-first** — Static assets (`.js`, `.css`, `.woff2`, `.svg`, `.png`, `.jpg`)
   - Serve from cache if available
   - Fall back to network if not cached
   - Cache response on success

3. **Network-first with SPA fallback** — HTML navigation
   - Try network first
   - Fall back to cached response
   - Fall back to cached `index.html` for offline SPA navigation

**Pre-caching on install:**
- `index.html`
- Root route `/`
- `favicon.svg`

**Cache cleanup on activation:**
- Deletes old cache versions automatically

### Registration Module (src/pwa/register-service-worker.ts)

Handles service worker lifecycle:

- **Registration** — Runs on app startup
- **Update checking** — Polls every 60 seconds
- **Update handling** — Dispatches custom event when new version available
- **Install prompt capture** — Intercepts `beforeinstallprompt` event
- **Install trigger** — `triggerInstallPrompt()` shows system prompt

### React Hooks (src/pwa/use-pwa.ts)

Provides UI integration:

**`usePwaInstall()`**
- `canInstall: boolean` — Whether install prompt is available
- `isInstalled: boolean` — Whether app is installed
- `install(): Promise<boolean>` — Trigger install prompt

**`usePwaStatus()`**
- `isOnline: boolean` — Current network status
- `updateAvailable: boolean` — New version detected
- `applyUpdate()` — Reload with new service worker

### UI Components

**InstallPrompt** (src/pwa/install-prompt.tsx)
- Shows snackbar when install prompt available
- Provides "Install" and "Dismiss" buttons
- Auto-hides after installation

**OfflineIndicator** (src/pwa/offline-indicator.tsx)
- Fixed banner at top when offline
- Shows "You are offline" message
- Automatically dismisses when connection restored

**UpdatePrompt** (src/pwa/offline-indicator.tsx)
- Snackbar appears when new version available
- Allows user to reload for immediate update
- Uses custom event system for notification

## Offline Behavior

### UI Remains Interactive

The home screen, template catalog, and settings remain fully accessible and interactive offline.

### API Operations Gracefully Fail

API calls return JSON response with `{ offline: true, message: ... }` status 503 when offline:

```typescript
try {
  const response = await fetch('/api/v1/sessions');
  if (!response.ok && response.status === 503) {
    const data = await response.json();
    if (data.offline) {
      console.log('No cached session data available');
    }
  }
} catch (error) {
  // Network error
}
```

### Reconnection Handling

When connection restores:
1. Offline indicator automatically hides
2. App retries pending operations
3. SignalR hub attempts reconnection
4. Session state is re-synchronized

## Implementation Guidelines

### For Feature Developers

When adding features that use the API:

1. **Expect offline failures** — API calls may fail with status 503
2. **Provide feedback** — Show "offline" message when cache unavailable
3. **Queue operations** — Consider persisting user intent for retry when online
4. **Test offline** — DevTools → Network tab → Offline mode

### For API Providers

- **Cache-friendly responses** — Return JSON responses consistently
- **Consistent content-type** — Always set `application/json` for APIs
- **Short timeouts** — Prevent hanging requests in poor connection states
- **Fallback responses** — Return 503 with offline message when cache unavailable

### For Deployment

1. **HTTPS required** — Service Workers only work on secure contexts
2. **Static asset versioning** — Use cache busting in build pipeline
3. **CSP compliance** — Service Worker scripts may require CSP adjustments
4. **Manifest delivery** — Ensure `Content-Type: application/manifest+json`

## Testing

### Simulate Installation (DevTools)

**Chrome/Edge:**
1. Open DevTools (F12)
2. Application → Manifest
3. Click "Install" button

**Manual trigger in code:**
```typescript
import { triggerInstallPrompt } from './pwa/register-service-worker';
await triggerInstallPrompt();
```

### Simulate Offline Mode

**Chrome/Edge DevTools:**
1. Open DevTools (F12)
2. Network tab
3. Check "Offline" checkbox
4. Refresh page

**Verify caching:**
1. Navigate to page
2. Enable offline mode
3. Refresh — should show cached content
4. Try API call — should show 503 offline error

### Monitor Service Worker

**Chrome/Edge DevTools:**
1. Application → Service Workers
2. View registration status
3. Check "Update on reload" for development
4. Use "Skip waiting" to immediately activate new version

### Test on Real Devices

**Android Chrome:**
1. Connect via USB
2. Enable USB debugging
3. Open `chrome://inspect`
4. Click "Inspect" on running app
5. DevTools mirror appears on desktop

**iOS Safari:**
1. Connect to Mac
2. Open Safari DevTools (Develop menu)
3. Select device and inspect

## Architecture Decisions

**Why Service Worker instead of AppCache:**
- Service Worker is the modern standard (AppCache deprecated)
- Fine-grained cache control per request
- Background sync capable (future enhancement)
- Worker can intercept and modify requests

**Why pre-cache only critical assets:**
- Reduces install time and disk space
- Newer assets loaded on first visit
- Cache versioning prevents stale data

**Why custom events instead of shared state:**
- Decouples PWA logic from React components
- Allows multiple listeners
- Simpler to test in isolation
- Follows browser platform patterns

## Future Enhancements

- **Background sync** — Queue failed operations for retry when online
- **Web push notifications** — Notify players of game updates
- **Periodic sync** — Sync session data in background
- **App shortcuts in menu** — Quick-access to recent games
- **Share target** — Accept room codes via share sheet
- **File handling** — Import/export game exports
- **Protocol handling** — Register `bankers-seat://` custom protocol

## Browser Support

| Browser | Desktop | Mobile | Notes |
|---------|---------|--------|-------|
| Chrome | ✅ | ✅ | Full PWA support |
| Edge | ✅ | ✅ | Full PWA support |
| Firefox | ✅ | ✅ | Full PWA support |
| Safari (iOS) | N/A | ⚠️ | Limited (home screen only) |
| Safari (macOS) | ⚠️ | N/A | Partial (big sur+) |

## Troubleshooting

### App won't install

1. Verify HTTPS is enabled
2. Check manifest.json loads: `curl https://example.com/manifest.json`
3. Verify service-worker.js loads: `curl https://example.com/service-worker.js`
4. Clear cache and reload (Ctrl+Shift+R)
5. Check DevTools → Application → Service Workers for errors

### Offline mode doesn't work

1. Verify service worker is active (DevTools → Application)
2. Check Network tab shows requests being intercepted
3. Verify cache contents: DevTools → Application → Cache Storage
4. Try "Skip waiting" to activate new service worker

### Old version still showing

1. DevTools → Application → Service Workers → "Unregister"
2. Clear storage: DevTools → Application → Clear site data
3. Reload page
4. New version should install on refresh

### Updates not detected

1. Verify service worker update check is running: Open console, search for "[PWA] Service Worker registered"
2. Check if new version actually exists (deploy verification)
3. Try manual update check: `navigator.serviceWorker.getRegistrations()` → `.update()`
4. Check browser cache headers on service-worker.js (should not cache the SW file itself)

## Related Documentation

- `docs/16-mobile-hybrid-plan.md` — Capacitor mobile shell plans
- `docs/04-system-architecture.md` — System deployment topology
- `docs/14-deployment-and-observability.md` — Docker and Kubernetes deployment
