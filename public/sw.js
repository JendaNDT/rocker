// Rocker service worker — hand-written.
//
// Strategy:
//   - install:  precache the minimal app shell. A new version does NOT take
//               over immediately — it waits until the user accepts the
//               update (the page posts "SKIP_WAITING").
//   - navigate: network-first, fall back to cache when offline
//   - RSC data (.txt route payloads): network-first too, so a fresh deploy is
//               not shadowed by a stale cached payload that points at deleted
//               chunks and forces a full page reload
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

// Stores a successful response in the cache. Two guards:
//   - response.ok keeps 404/500 out of the cache — otherwise an offline
//     visit would happily replay an error page.
//   - event.waitUntil keeps the worker alive until the write finishes;
//     a bare fire-and-forget cache.put can be killed mid-write when the
//     worker idles out.
function cachePut(event, request, response) {
  if (!response || !response.ok) return;
  const copy = response.clone();
  event.waitUntil(
    caches.open(CACHE).then((cache) => cache.put(request, copy)),
  );
}

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;

  const url = new URL(request.url);

  // Navigations: try the network first, fall back to the cached shell.
  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request)
        .then((response) => {
          cachePut(event, request, response);
          return response;
        })
        .catch(() =>
          caches.match(request).then((cached) => cached || caches.match("/")),
        ),
    );
    return;
  }

  // Next.js client-side navigation data: the per-route .txt RSC payloads in a
  // static export (and any App Router RSC request). Network-first, like real
  // navigations — after a deploy these payloads reference freshly hashed
  // chunks, so a stale cached copy would point at deleted files and make Next
  // fall back to a full page reload, tearing down long-lived audio (the
  // aparát) on the first navigation after every update. Fall back to cache
  // when offline.
  if (request.headers.get("RSC") === "1" || url.pathname.endsWith(".txt")) {
    event.respondWith(
      fetch(request)
        .then((response) => {
          cachePut(event, request, response);
          return response;
        })
        .catch(() => caches.match(request)),
    );
    return;
  }

  // Everything else: serve from cache if present, otherwise fetch and cache.
  event.respondWith(
    caches.match(request).then((cached) => {
      if (cached) return cached;
      return fetch(request).then((response) => {
        // Only cache same-origin GETs — CDN responses can vary by visitor
        // and are managed by the browser cache, not us.
        if (url.origin === self.location.origin) {
          cachePut(event, request, response);
        }
        return response;
      });
    }),
  );
});
