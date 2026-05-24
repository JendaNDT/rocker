"use client";

import { useEffect } from "react";
import { buildAchievementContext, newlyUnlockedIds } from "@/lib/achievements";
import { useLessonProgressStore } from "@/stores/lessonProgressStore";
import { useProgressStore } from "@/stores/progressStore";

/**
 * Invisible watcher mounted once in AppShell. Whenever progress or lesson
 * completion changes it re-evaluates the achievement catalogue and persists
 * any newly earned achievement. Unlocking is idempotent, so the effect
 * settles after one pass. The visual "unlocked" surface lives on the
 * dashboard (Krok 5).
 */
export function AchievementWatcher() {
  const dailyMinutes = useProgressStore((state) => state.dailyMinutes);
  const bpmRecords = useProgressStore((state) => state.bpmRecords);
  const unlockedAchievements = useProgressStore(
    (state) => state.unlockedAchievements,
  );
  const unlockAchievement = useProgressStore(
    (state) => state.unlockAchievement,
  );
  const completedLessonIds = useLessonProgressStore(
    (state) => state.completedLessonIds,
  );

  useEffect(() => {
    const context = buildAchievementContext(
      { dailyMinutes, bpmRecords, unlockedAchievements },
      completedLessonIds,
    );
    for (const id of newlyUnlockedIds(context, unlockedAchievements)) {
      unlockAchievement(id);
    }
  }, [
    dailyMinutes,
    bpmRecords,
    unlockedAchievements,
    completedLessonIds,
    unlockAchievement,
  ]);

  return null;
}
