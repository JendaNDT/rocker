// Rocker service worker — hand-written.
//
// Strategy:
//   - install:  precache the minimal app shell. A new version does NOT take
//               over immediately — it waits until the user accepts the
//               update (the page posts "SKIP_WAITING").
//   - navigate: network-first, fall back to cache when offline
//   - other GET: cache-first, then network (and cache the result)
//
// Hashed Next.js assets are cached at runtime as they are first requested,
// so after one online visit the app works fully offline.
//
// VERSION is replaced at build time by scripts/stamp-sw.mjs, so every deploy
// ships a byte-different sw.js — that is what lets the browser detect a new
// version and the app offer the update.

const VERSION = "DEV";
const CACHE = `rocker-${VERSION}`;

const APP_SHELL = [
  "/",
  "/manifest.json",
  "/icons/icon-192.png",
  "/icons/icon-512.png",
];

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll(APP_SHELL)));
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(
          keys.filter((key) => key !== CACHE).map((key) => caches.delete(key)),
        ),
      )
      .then(() => self.clients.claim()),
  );
});

// The page sends this once the user accepts the update, so the waiting
// worker activates right away instead of on the next cold start.
self.addEventListener("message", (event) => {
  if (event.data === "SKIP_WAITING") {
    self.skipWaiting();
  }
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;

  // Navigations: try the network first, fall back to the cached shell.
  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request)
        .then((response) => {
          const copy = response.clone();
          caches.open(CACHE).then((cache) => cache.put(request, copy));
          return response;
        })
        .catch(() =>
          caches.match(request).then((cached) => cached || caches.match("/")),
        ),
    );
    return;
  }

  // Everything else: serve from cache if present, otherwise fetch and cache.
  event.respondWith(
    caches.match(request).then((cached) => {
      if (cached) return cached;
      return fetch(request).then((response) => {
        const sameOrigin =
          new URL(request.url).origin === self.location.origin;
        if (response.ok && sameOrigin) {
          const copy = response.clone();
          caches.open(CACHE).then((cache) => cache.put(request, copy));
        }
        return response;
      });
    }),
  );
});
