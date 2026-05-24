"use client";

import { currentStreak, todayKey, totalMinutes } from "@/lib/progress";
import { useHydrated } from "@/lib/useHydrated";
import { useProgressStore } from "@/stores/progressStore";

function Stat({ value, label }: { value: string; label: string }) {
  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-900 p-4 text-center">
      <div className="text-2xl font-bold text-amber-500">{value}</div>
      <div className="mt-1 text-xs text-neutral-400">{label}</div>
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

  return (
    <div className="grid grid-cols-3 gap-3">
      <Stat value={String(streak)} label="dní v řadě" />
      <Stat value={String(today)} label="minut dnes" />
      <Stat value={String(total)} label="minut celkem" />
    </div>
  );
}
