"use client";

import { useEffect, useState } from "react";
import { AchievementsCard } from "@/components/dashboard/AchievementsCard";
import { DailyPlanCard } from "@/components/dashboard/DailyPlanCard";
import { StatsRow } from "@/components/dashboard/StatsRow";
import { useResolvedLayoutMode } from "@/lib/layout";
import { useSettingsStore } from "@/stores/settingsStore";

// Daily plan + achievements: side by side on desktop, stacked on mobile.
const PLAN_GRID = {
  desktop: "grid grid-cols-2 items-start gap-4",
  mobile: "space-y-4",
  auto: "space-y-4 lg:grid lg:grid-cols-2 lg:items-start lg:gap-4 lg:space-y-0",
};

/**
 * Home-screen dashboard (Bod 10): headline stats, today's practice plan, and
 * achievements. Hidden entirely in Zen mode (Bod 13) for a calmer home
 * screen — progress is still tracked, only its display is hidden.
 */
export function Dashboard() {
  const zenMode = useSettingsStore((state) => state.zenMode);
  const layoutMode = useResolvedLayoutMode();
  const [hydrated, setHydrated] = useState(false);
  useEffect(() => {
    setHydrated(true);
  }, []);

  if (!hydrated || zenMode) return null;

  return (
    <div className="space-y-4">
      <StatsRow />
      <div className={PLAN_GRID[layoutMode]}>
        <DailyPlanCard />
        <AchievementsCard />
      </div>
    </div>
  );
}
