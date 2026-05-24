import { create } from "zustand";
import { persist } from "zustand/middleware";
import {
  type ProgressData,
  emptyProgress,
  sanitizeProgress,
  todayKey,
} from "@/lib/progress";

interface ProgressState extends ProgressData {
  /** Adds practiced minutes to a day (defaults to today). */
  addPracticeMinutes: (minutes: number, day?: string) => void;
  /** Records a BPM result for an exercise, keeping only the highest. */
  recordBpm: (exerciseId: string, bpm: number) => void;
  /** Unlocks an achievement; a no-op if already unlocked. */
  unlockAchievement: (id: string) => void;
  /** Wipes all progress back to empty. */
  resetProgress: () => void;
}

/**
 * Progress tracking, persisted in localStorage under "rocker-progress".
 * This is small structured JSON (minutes per day, records, achievement ids),
 * so localStorage via Zustand persist is enough — IndexedDB stays reserved
 * for large blobs (recordings, Bod 11). Persisted data is run through
 * sanitizeProgress on rehydration so corrupt storage can never crash the app.
 */
export const useProgressStore = create<ProgressState>()(
  persist(
    (set) => ({
      ...emptyProgress(),

      addPracticeMinutes: (minutes, day) =>
        set((state) => {
          if (!Number.isFinite(minutes) || minutes <= 0) return state;
          const key = day ?? todayKey();
          const current = state.dailyMinutes[key] ?? 0;
          return {
            dailyMinutes: { ...state.dailyMinutes, [key]: current + minutes },
          };
        }),

      recordBpm: (exerciseId, bpm) =>
        set((state) => {
          if (!Number.isFinite(bpm) || bpm <= 0) return state;
          const best = state.bpmRecords[exerciseId] ?? 0;
          if (bpm <= best) return state;
          return {
            bpmRecords: { ...state.bpmRecords, [exerciseId]: bpm },
          };
        }),

      unlockAchievement: (id) =>
        set((state) =>
          state.unlockedAchievements.includes(id)
            ? state
            : {
                unlockedAchievements: [...state.unlockedAchievements, id],
              },
        ),

      resetProgress: () => set({ ...emptyProgress() }),
    }),
    {
      name: "rocker-progress",
      partialize: (state) => ({
        dailyMinutes: state.dailyMinutes,
        bpmRecords: state.bpmRecords,
        unlockedAchievements: state.unlockedAchievements,
      }),
      merge: (persisted, current) => ({
        ...current,
        ...sanitizeProgress(persisted),
      }),
    },
  ),
);
