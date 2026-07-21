// Service worker registration and install prompt handling
// Enables PWA installation on desktop and mobile browsers

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

let deferredPrompt: BeforeInstallPromptEvent | null = null;

// Register service worker
export async function registerServiceWorker() {
  if (!('serviceWorker' in navigator)) {
    console.log('[PWA] Service Workers not supported');
    return;
  }

  try {
    const registration = await navigator.serviceWorker.register('/service-worker.js', {
      scope: '/',
    });

    console.log('[PWA] Service Worker registered:', registration);

    // Check for updates periodically
    setInterval(() => {
      registration.update();
    }, 60000);

    // Handle updates
    registration.addEventListener('updatefound', () => {
      const newWorker = registration.installing;
      if (newWorker) {
        newWorker.addEventListener('statechange', () => {
          if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
            console.log('[PWA] New service worker available');
            notifyUpdateAvailable();
          }
        });
      }
    });
  } catch (error) {
    console.warn('[PWA] Service Worker registration failed:', error);
  }
}

// Capture install prompt
export function setupInstallPrompt() {
  window.addEventListener('beforeinstallprompt', (event: Event) => {
    event.preventDefault();
    deferredPrompt = event as BeforeInstallPromptEvent;
    console.log('[PWA] Install prompt captured');
    notifyInstallPromptReady();
  });

  window.addEventListener('appinstalled', () => {
    console.log('[PWA] App installed');
    deferredPrompt = null;
    notifyAppInstalled();
  });
}

// Trigger install prompt programmatically
export async function triggerInstallPrompt(): Promise<boolean> {
  if (!deferredPrompt) {
    console.log('[PWA] Install prompt not available');
    return false;
  }

  try {
    await deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;
    console.log(`[PWA] User choice: ${outcome}`);
    deferredPrompt = null;
    return outcome === 'accepted';
  } catch (error) {
    console.error('[PWA] Error showing install prompt:', error);
    return false;
  }
}

// Check if app is installed
export function isAppInstalled(): boolean {
  return (window.navigator as any).standalone === true || document.referrer.includes('android-app://');
}

// Notify install prompt ready (dispatch custom event for app to listen)
function notifyInstallPromptReady() {
  const event = new CustomEvent('pwa:prompt-ready', {
    detail: { canInstall: true },
  });
  window.dispatchEvent(event);
}

// Notify app installed (dispatch custom event)
function notifyAppInstalled() {
  const event = new CustomEvent('pwa:installed', {
    detail: { timestamp: Date.now() },
  });
  window.dispatchEvent(event);
}

// Notify update available (dispatch custom event)
function notifyUpdateAvailable() {
  const event = new CustomEvent('pwa:update-available', {
    detail: { timestamp: Date.now() },
  });
  window.dispatchEvent(event);
}

// Initialize PWA
if (typeof window !== 'undefined') {
  registerServiceWorker();
  setupInstallPrompt();
}
