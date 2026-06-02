import { create } from "zustand";
import { persist } from "zustand/middleware";
import { sanitizeCompletedLessonIds } from "@/lib/lessonProgress";

interface LessonProgressState {
  /** Ids of lessons the learner has finished. */
  completedLessonIds: string[];
  markLessonComplete: (lessonId: string) => void;
  resetProgress: () => void;
}

/**
 * Lesson progress, persisted in localStorage under "rocker-lesson-progress".
 * Bod 10 will expand progress tracking (streaks, practice minutes); for now
 * this just records which lessons are done.
 */
export const useLessonProgressStore = create<LessonProgressState>()(
  persist(
    (set) => ({
      completedLessonIds: [],
      markLessonComplete: (lessonId) =>
        set((state) =>
          state.completedLessonIds.includes(lessonId)
            ? state
            : {
                completedLessonIds: [...state.completedLessonIds, lessonId],
              },
        ),
      resetProgress: () => set({ completedLessonIds: [] }),
    }),
    {
      name: "rocker-lesson-progress",
      // Defensive: a corrupt or hand-edited value (this key is also restored
      // from the data backup) must not crash the dashboard / daily plan /
      // tutor that read this list. Coerce to a clean string array.
      merge: (persistedState, currentState) => {
        const persisted =
          persistedState && typeof persistedState === "object"
            ? (persistedState as { completedLessonIds?: unknown })
            : {};
        return {
          ...currentState,
          completedLessonIds: sanitizeCompletedLessonIds(
            persisted.completedLessonIds,
          ),
        };
      },
    },
  ),
);
