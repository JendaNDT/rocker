"use client";

import { useEffect, useRef, useState } from "react";

/**
 * Registers the hand-written service worker and, once a newer version has
 * been deployed, shows a small bar offering to load it.
 *
 * Only runs in production; in development the SW is disabled. Registration
 * failure is non-fatal — the app still works online.
 */
export function ServiceWorkerRegister() {
  const [waiting, setWaiting] = useState<ServiceWorker | null>(null);
  // True only once the user accepts the update — guards the reload so the
  // first-install controllerchange can't reload the page on its own.
  const acceptedRef = useRef(false);

  useEffect(() => {
    if (
      process.env.NODE_ENV !== "production" ||
      !("serviceWorker" in navigator)
    ) {
      return;
    }

    let reloading = false;
    const onControllerChange = () => {
      if (!acceptedRef.current || reloading) return;
      reloading = true;
      window.location.reload();
    };
    navigator.serviceWorker.addEventListener(
      "controllerchange",
      onControllerChange,
    );

    navigator.serviceWorker
      .register("/sw.js")
      .then((registration) => {
        // A newer worker may already be waiting from an earlier visit.
        if (registration.waiting && navigator.serviceWorker.controller) {
          setWaiting(registration.waiting);
        }
        registration.addEventListener("updatefound", () => {
          const installing = registration.installing;
          if (!installing) return;
          installing.addEventListener("statechange", () => {
            // "installed" while a controller already exists means this is an
            // update (not the very first install) — offer it to the user.
            if (
              installing.state === "installed" &&
              navigator.serviceWorker.controller
            ) {
              setWaiting(installing);
            }
          });
        });
      })
      .catch(() => {
        // Ignore — the app remains usable without offline support.
      });

    return () => {
      navigator.serviceWorker.removeEventListener(
        "controllerchange",
        onControllerChange,
      );
    };
  }, []);

  function applyUpdate() {
    if (!waiting) return;
    acceptedRef.current = true;
    waiting.postMessage("SKIP_WAITING");
  }

  if (!waiting) return null;

  return (
    <div
      role="status"
      className="fixed bottom-24 left-1/2 z-30 flex max-w-[92vw] -translate-x-1/2 items-center gap-3 rounded-lg border border-line bg-surface px-4 py-3 shadow-lg md:bottom-4"
    >
      <span className="text-sm text-text">Je k dispozici nová verze.</span>
      <button
        type="button"
        onClick={applyUpdate}
        className="shrink-0 rounded-lg bg-accent px-3 py-1.5 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
      >
        Načíst
      </button>
    </div>
  );
}
