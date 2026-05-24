"use client";

import { useEffect, useRef } from "react";
import { usePathname } from "next/navigation";
import { isPracticeRoute, msToMinutes } from "@/lib/practiceTime";
import { useProgressStore } from "@/stores/progressStore";

// How often an in-progress segment is written to the store, so a long
// uninterrupted session is not lost if the tab is closed without a clean
// unmount.
const FLUSH_INTERVAL_MS = 60000;

/**
 * Invisible tracker mounted once in AppShell. It measures active time spent
 * on practice screens (tuner, metronome, practice, youtube) and writes the
 * minutes into the progress store. Time only counts while the screen is open
 * AND the tab is visible — switching away pauses the count.
 */
export function PracticeTimeTracker() {
  const pathname = usePathname();
  const addPracticeMinutes = useProgressStore(
    (state) => state.addPracticeMinutes,
  );
  // Timestamp when the currently open practice segment began, or null when no
  // segment is open.
  const segmentStart = useRef<number | null>(null);

  useEffect(() => {
    const onPracticeRoute = isPracticeRoute(pathname);

    // Records the elapsed time of the open segment. When reopen is true a
    // fresh segment starts immediately (used by the periodic flush).
    const flush = (reopen: boolean) => {
      if (segmentStart.current !== null) {
        const minutes = msToMinutes(Date.now() - segmentStart.current);
        if (minutes > 0) addPracticeMinutes(minutes);
      }
      segmentStart.current = reopen ? Date.now() : null;
    };

    // Opens or closes the segment based on route and tab visibility.
    const sync = () => {
      const shouldTrack =
        onPracticeRoute && document.visibilityState === "visible";
      if (shouldTrack && segmentStart.current === null) {
        segmentStart.current = Date.now();
      } else if (!shouldTrack && segmentStart.current !== null) {
        flush(false);
      }
    };

    sync();

    const onVisibilityChange = () => sync();
    document.addEventListener("visibilitychange", onVisibilityChange);

    const interval = window.setInterval(() => {
      if (segmentStart.current !== null) flush(true);
    }, FLUSH_INTERVAL_MS);

    return () => {
      document.removeEventListener("visibilitychange", onVisibilityChange);
      window.clearInterval(interval);
      // Record whatever is still open when the route changes or the app unmounts.
      flush(false);
    };
  }, [pathname, addPracticeMinutes]);

  return null;
}
