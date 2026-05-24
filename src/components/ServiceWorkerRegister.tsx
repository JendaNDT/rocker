"use client";

import { useEffect } from "react";

/**
 * Registers the service worker (built by Serwist into /sw.js).
 * Only runs in production; in development the SW is disabled.
 * Registration failure is non-fatal — the app still works online.
 */
export function ServiceWorkerRegister() {
  useEffect(() => {
    if (
      process.env.NODE_ENV === "production" &&
      "serviceWorker" in navigator
    ) {
      navigator.serviceWorker.register("/sw.js").catch(() => {
        // Ignore — app remains usable without offline support.
      });
    }
  }, []);

  return null;
}
