"use client";

import { useEffect } from "react";

/**
 * Keeps the screen awake while the calling component is mounted, using the
 * Screen Wake Lock API — handy on practice screens where the player reads
 * the screen without touching the device.
 *
 * The browser releases the lock whenever the page is hidden, so the lock is
 * re-acquired on visibilitychange. On browsers without the API (and if a
 * request is rejected, e.g. on low battery) the hook quietly does nothing —
 * the screen simply behaves as usual.
 */
export function useWakeLock(): void {
  useEffect(() => {
    if (typeof navigator === "undefined" || !("wakeLock" in navigator)) {
      return;
    }

    let sentinel: WakeLockSentinel | null = null;
    let cancelled = false;

    const acquire = async () => {
      if (document.visibilityState !== "visible") return;
      try {
        const lock = await navigator.wakeLock.request("screen");
        if (cancelled) {
          void lock.release();
          return;
        }
        sentinel = lock;
        // The browser drops the lock when the page is hidden; clearing the
        // reference lets visibilitychange re-acquire it on return.
        lock.addEventListener("release", () => {
          sentinel = null;
        });
      } catch {
        // Request rejected — leave the screen on its default behaviour.
      }
    };

    const handleVisibility = () => {
      if (document.visibilityState === "visible" && sentinel === null) {
        void acquire();
      }
    };

    void acquire();
    document.addEventListener("visibilitychange", handleVisibility);

    return () => {
      cancelled = true;
      document.removeEventListener("visibilitychange", handleVisibility);
      if (sentinel !== null) {
        void sentinel.release();
        sentinel = null;
      }
    };
  }, []);
}
