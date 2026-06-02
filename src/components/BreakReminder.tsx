"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname } from "next/navigation";
import { advanceBreak, initialBreakState } from "@/lib/breakReminder";
import type { BreakState } from "@/lib/breakReminder";
import { isPracticeRoute } from "@/lib/practiceTime";

const TICK_MS = 15000;
// Cap on the time charged to one tick — guards against a huge jump after the
// tab was throttled or hidden in the background.
const MAX_TICK_MS = 30000;

/**
 * Invisible tracker mounted once in AppShell. It watches continuous practice
 * time and, after a long unbroken stretch, shows a gentle reminder to rest.
 * Time only counts on practice screens while the tab is visible; the logic
 * lives in src/lib/breakReminder.ts.
 */
export function BreakReminder() {
  const pathname = usePathname();
  const pathnameRef = useRef(pathname);
  pathnameRef.current = pathname;

  const stateRef = useRef<BreakState>(initialBreakState);
  const lastTickRef = useRef(Date.now());
  const [show, setShow] = useState(false);

  useEffect(() => {
    const interval = window.setInterval(() => {
      const now = Date.now();
      const delta = Math.min(now - lastTickRef.current, MAX_TICK_MS);
      lastTickRef.current = now;

      const practicing =
        isPracticeRoute(pathnameRef.current) &&
        document.visibilityState === "visible";
      const { state, remind } = advanceBreak(
        stateRef.current,
        delta,
        practicing,
      );
      stateRef.current = state;
      if (remind) setShow(true);
    }, TICK_MS);

    return () => window.clearInterval(interval);
  }, []);

  function dismiss() {
    setShow(false);
    stateRef.current = initialBreakState;
    lastTickRef.current = Date.now();
  }

  if (!show) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      // On desktop (md+) the side rail eats the left 14rem, so the reminder
      // centres only within the main content column. Bottom 24 leaves room
      // for the mobile bottom-bar nav.
      className="fixed inset-x-3 bottom-24 z-20 mx-auto max-w-md rounded-xl border border-accent bg-surface p-4 shadow-lg md:right-4 md:bottom-4 md:left-[14.75rem] md:mx-0"
    >
      <p className="text-sm text-text">
        Cvičíš už 45 minut. Dej si pauzu — protáhni prsty a nech uši
        odpočinout.
      </p>
      <button
        type="button"
        onClick={dismiss}
        className="mt-3 rounded-lg bg-accent px-4 py-1.5 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
      >
        Dobře
      </button>
    </div>
  );
}
