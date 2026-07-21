// Service Worker for Banker's Seat PWA
// Provides offline capability and asset caching for installed app

const CACHE_VERSION = 'v1';
const CACHE_STATIC = `${CACHE_VERSION}-static`;
const CACHE_DYNAMIC = `${CACHE_VERSION}-dynamic`;
const CACHE_API = `${CACHE_VERSION}-api`;

// Static assets to pre-cache on install
const PRECACHE_ASSETS = [
  '/',
  '/index.html',
  '/favicon.svg',
];

// Network-first strategy: try network, fall back to cache
const NETWORK_FIRST_PATHS = [
  '/api/',
  '/hubs/',
];

// Cache-first strategy: use cache, fall back to network
const CACHE_FIRST_PATHS = [
  '.js',
  '.css',
  '.woff2',
  '.svg',
  '.png',
  '.jpg',
];

self.addEventListener('install', (event) => {
  console.log('[SW] Installing service worker');
  event.waitUntil(
    caches.open(CACHE_STATIC)
      .then((cache) => {
        console.log('[SW] Pre-caching static assets');
        return cache.addAll(PRECACHE_ASSETS).catch((err) => {
          console.warn('[SW] Some assets failed to pre-cache (expected in dev):', err.message);
        });
      })
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  console.log('[SW] Activating service worker');
  event.waitUntil(
    caches.keys()
      .then((cacheNames) => {
        return Promise.all(
          cacheNames
            .filter((name) => name !== CACHE_STATIC && name !== CACHE_DYNAMIC && name !== CACHE_API)
            .map((name) => {
              console.log('[SW] Deleting old cache:', name);
              return caches.delete(name);
            })
        );
      })
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const { request } = event;
  const url = new URL(request.url);

  // Skip cross-origin requests and non-GET
  if (url.origin !== self.location.origin || request.method !== 'GET') {
    return;
  }

  // Network-first for API calls
  if (NETWORK_FIRST_PATHS.some((path) => url.pathname.startsWith(path))) {
    event.respondWith(networkFirst(request));
    return;
  }

  // Cache-first for static assets
  if (CACHE_FIRST_PATHS.some((ext) => url.pathname.endsWith(ext))) {
    event.respondWith(cacheFirst(request));
    return;
  }

  // Default: network-first with offline fallback to index.html for SPA
  event.respondWith(networkFirstWithSpaFallback(request));
});

async function networkFirst(request) {
  try {
    const response = await fetch(request);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    // Cache successful API responses
    const cache = await caches.open(CACHE_API);
    cache.put(request, response.clone());
    return response;
  } catch (error) {
    console.log('[SW] Network request failed, trying cache:', request.url);
    const cached = await caches.match(request);
    if (cached) {
      return cached;
    }
    // Return offline response for failed API
    return new Response(
      JSON.stringify({ offline: true, message: 'No cached response available' }),
      { status: 503, headers: { 'Content-Type': 'application/json' } }
    );
  }
}

async function cacheFirst(request) {
  const cached = await caches.match(request);
  if (cached) {
    return cached;
  }

  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(CACHE_STATIC);
      cache.put(request, response.clone());
    }
    return response;
  } catch (error) {
    console.log('[SW] Failed to fetch asset:', request.url);
    return new Response('Asset not available offline', { status: 404 });
  }
}

async function networkFirstWithSpaFallback(request) {
  try {
    const response = await fetch(request);
    if (response.ok) {
      const cache = await caches.open(CACHE_DYNAMIC);
      cache.put(request, response.clone());
    }
    return response;
  } catch (error) {
    console.log('[SW] Network request failed, falling back to cached index:', request.url);
    const cached = await caches.match(request);
    if (cached) {
      return cached;
    }

    // Fall back to cached index.html for offline SPA navigation
    const indexResponse = await caches.match('/index.html');
    if (indexResponse) {
      return indexResponse;
    }

    return new Response('Offline - try refreshing when connection is restored', { status: 503 });
  }
}

// Handle messages from clients
self.addEventListener('message', (event) => {
  if (event.data?.type === 'SKIP_WAITING') {
    self.skipWaiting();
  }
});
