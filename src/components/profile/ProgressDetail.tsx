"use client";

import {
  currentStreak,
  longestStreak,
  totalMinutes,
  totalPracticeDays,
} from "@/lib/progress";
import { useHydrated } from "@/hooks/useHydrated";
import { useProgressStore } from "@/stores/progressStore";

function Figure({ value, label }: { value: string; label: string }) {
  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <div className="text-xl font-bold text-accent">{value}</div>
      <div className="mt-1 text-xs text-dim">{label}</div>
    </div>
  );
}

/**
 * Profile section summarising practice progress (Bod 10): streaks, practiced
 * days and total minutes, plus a guarded reset. Figures come from persisted
 * storage, so they are gated behind useHydrated. The reset clears practice
 * minutes, streaks and achievements only — completed lessons are untouched.
 */
export function ProgressDetail() {
  const hydrated = useHydrated();
  const dailyMinutes = useProgressStore((state) => state.dailyMinutes);
  const resetProgress = useProgressStore((state) => state.resetProgress);

  const streak = hydrated ? currentStreak(dailyMinutes) : 0;
  const best = hydrated ? longestStreak(dailyMinutes) : 0;
  const days = hydrated ? totalPracticeDays(dailyMinutes) : 0;
  const minutes = hydrated ? Math.round(totalMinutes(dailyMinutes)) : 0;

  function handleReset() {
    if (
      window.confirm(
        "Opravdu vynulovat pokrok? Smažou se odcvičené minuty, streaky " +
          "a achievementy. Dokončené lekce zůstanou.",
      )
    ) {
      resetProgress();
    }
  }

  return (
    <div>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Figure value={String(streak)} label="dní v řadě teď" />
        <Figure value={String(best)} label="nejdelší série" />
        <Figure value={String(days)} label="odcvičených dní" />
        <Figure value={String(minutes)} label="minut celkem" />
      </div>

      <button
        type="button"
        onClick={handleReset}
        className="mt-4 rounded-lg border border-line bg-bg px-4 py-2 text-sm text-dim transition-colors hover:border-danger hover:text-danger"
      >
        Vynulovat pokrok
      </button>
    </div>
  );
}
