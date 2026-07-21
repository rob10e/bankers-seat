import { useEffect, useState, useCallback } from 'react';
import { triggerInstallPrompt, isAppInstalled } from './register-service-worker';

/**
 * Hook for PWA install prompt management
 * Detects when install prompt is available and provides trigger function
 */
export function usePwaInstall() {
  const [canInstall, setCanInstall] = useState(false);
  const [isInstalled, setIsInstalled] = useState(false);

  useEffect(() => {
    setIsInstalled(isAppInstalled());

    const handlePromptReady = () => {
      setCanInstall(true);
    };

    const handleInstalled = () => {
      setCanInstall(false);
      setIsInstalled(true);
    };

    window.addEventListener('pwa:prompt-ready', handlePromptReady);
    window.addEventListener('pwa:installed', handleInstalled);

    return () => {
      window.removeEventListener('pwa:prompt-ready', handlePromptReady);
      window.removeEventListener('pwa:installed', handleInstalled);
    };
  }, []);

  const install = useCallback(async () => {
    return triggerInstallPrompt();
  }, []);

  return { canInstall, isInstalled, install };
}

/**
 * Hook for detecting offline state and service worker updates
 * Provides connection status and update availability
 */
export function usePwaStatus() {
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [updateAvailable, setUpdateAvailable] = useState(false);

  useEffect(() => {
    const handleOnline = () => {
      setIsOnline(true);
      console.log('[PWA] Connection restored');
    };

    const handleOffline = () => {
      setIsOnline(false);
      console.log('[PWA] Connection lost');
    };

    const handleUpdateAvailable = () => {
      setUpdateAvailable(true);
      console.log('[PWA] Update available');
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    window.addEventListener('pwa:update-available', handleUpdateAvailable);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
      window.removeEventListener('pwa:update-available', handleUpdateAvailable);
    };
  }, []);

  const applyUpdate = useCallback(() => {
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
      navigator.serviceWorker.controller.postMessage({ type: 'SKIP_WAITING' });
      window.location.reload();
    }
  }, []);

  return { isOnline, updateAvailable, applyUpdate };
}
