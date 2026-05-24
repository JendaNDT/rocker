import { create } from "zustand";
import { persist } from "zustand/middleware";

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
    { name: "rocker-lesson-progress" },
  ),
);
