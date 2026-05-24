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
    <div className="fixed inset-x-3 bottom-24 z-20 mx-auto max-w-md rounded-xl border border-amber-700 bg-neutral-900 p-4 shadow-lg md:bottom-4">
      <p className="text-sm text-neutral-200">
        Cvičíš už 45 minut. Dej si pauzu — protáhni prsty a nech uši
        odpočinout.
      </p>
      <button
        type="button"
        onClick={dismiss}
        className="mt-3 rounded-lg bg-amber-500 px-4 py-1.5 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
      >
        Dobře
      </button>
    </div>
  );
}
