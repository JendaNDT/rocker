"use client";

import { currentStreak, todayKey, totalMinutes } from "@/lib/progress";
import { useHydrated } from "@/hooks/useHydrated";
import { useProgressStore } from "@/stores/progressStore";

function Stat({ value, label }: { value: string; label: string }) {
  return (
    <div className="rounded-lg border border-line bg-surface p-4 text-center">
      <div className="text-2xl font-bold text-accent">{value}</div>
      <div className="mt-1 text-xs text-dim">{label}</div>
    </div>
  );
}

/**
 * Three headline numbers: current streak, minutes practiced today, and
 * lifetime minutes. Values come from persisted storage, so they are gated
 * behind useHydrated — server and first client render show zeros, then the
 * real figures appear once mounted.
 */
export function StatsRow() {
  const hydrated = useHydrated();
  const dailyMinutes = useProgressStore((state) => state.dailyMinutes);

  const streak = hydrated ? currentStreak(dailyMinutes) : 0;
  const today = hydrated ? Math.round(dailyMinutes[todayKey()] ?? 0) : 0;
  const total = hydrated ? Math.round(totalMinutes(dailyMinutes)) : 0;

  // On a 320 px iPhone the three columns squashed long labels onto two
  // lines; stack to one column there and recover the row at sm+.
  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
      <Stat value={String(streak)} label="dní v řadě" />
      <Stat value={String(today)} label="minut dnes" />
      <Stat value={String(total)} label="minut celkem" />
    </div>
  );
}
