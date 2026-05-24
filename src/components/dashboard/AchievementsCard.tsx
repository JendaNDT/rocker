"use client";

import { ACHIEVEMENTS } from "@/lib/achievements";
import { useHydrated } from "@/lib/useHydrated";
import { useProgressStore } from "@/stores/progressStore";

/**
 * The full achievement catalogue with earned ones highlighted. Earned state
 * comes from persisted storage, so it is gated behind useHydrated — before
 * hydration everything renders as locked.
 */
export function AchievementsCard() {
  const hydrated = useHydrated();
  const unlockedAchievements = useProgressStore(
    (state) => state.unlockedAchievements,
  );
  const earned = new Set(hydrated ? unlockedAchievements : []);

  return (
    <section className="rounded-lg border border-neutral-800 bg-neutral-900 p-4">
      <div className="flex items-baseline justify-between">
        <h2 className="font-semibold">Achievementy</h2>
        <span className="text-xs text-neutral-400">
          {earned.size} z {ACHIEVEMENTS.length}
        </span>
      </div>

      <ul className="mt-3 grid gap-2 sm:grid-cols-2">
        {ACHIEVEMENTS.map((achievement) => {
          const isEarned = earned.has(achievement.id);
          return (
            <li
              key={achievement.id}
              className={
                "rounded-md border p-3 " +
                (isEarned
                  ? "border-amber-700/60 bg-amber-950/30"
                  : "border-neutral-800 bg-neutral-950")
              }
            >
              <div
                className={
                  "font-medium " +
                  (isEarned ? "text-amber-400" : "text-neutral-500")
                }
              >
                {achievement.title}
              </div>
              <div className="mt-0.5 text-sm text-neutral-400">
                {achievement.description}
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
