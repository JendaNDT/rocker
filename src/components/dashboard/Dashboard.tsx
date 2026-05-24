"use client";

import { useEffect, useState } from "react";
import { AchievementsCard } from "@/components/dashboard/AchievementsCard";
import { DailyPlanCard } from "@/components/dashboard/DailyPlanCard";
import { StatsRow } from "@/components/dashboard/StatsRow";
import { useSettingsStore } from "@/stores/settingsStore";

/**
 * Home-screen dashboard (Bod 10): headline stats, today's practice plan, and
 * achievements. Hidden entirely in Zen mode (Bod 13) for a calmer home
 * screen — progress is still tracked, only its display is hidden.
 */
export function Dashboard() {
  const zenMode = useSettingsStore((state) => state.zenMode);
  const [hydrated, setHydrated] = useState(false);
  useEffect(() => {
    setHydrated(true);
  }, []);

  if (!hydrated || zenMode) return null;

  return (
    <div className="space-y-4">
      <StatsRow />
      <DailyPlanCard />
      <AchievementsCard />
    </div>
  );
}
