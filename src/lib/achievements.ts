// Achievements — pure logic + data, no DOM. Unit-tested in achievements.test.ts.
// Part of Bod 10: unlockable milestones that give a reason to come back.

import { STAGES } from "./lessons";
import {
  type ProgressData,
  longestStreak,
  totalMinutes,
  totalPracticeDays,
} from "./progress";

/** Lesson ids that make up Stage 1, derived so the list stays in sync. */
const STAGE_1_LESSON_IDS =
  STAGES.find((stage) => stage.id === "stage-1")?.lessons.map(
    (lesson) => lesson.id,
  ) ?? [];

/** Total number of lessons across all stages. */
const TOTAL_LESSON_COUNT = STAGES.reduce(
  (count, stage) => count + stage.lessons.length,
  0,
);

/** The highest value among recorded BPM results, or 0 if there are none. */
function bestBpm(bpmRecords: Record<string, number>): number {
  const values = Object.values(bpmRecords);
  return values.length === 0 ? 0 : Math.max(...values);
}

/** The stats an achievement condition is evaluated against. */
export interface AchievementContext {
  /** Longest run of consecutive practiced days ever. */
  longestStreak: number;
  /** Total practiced minutes across all days. */
  totalMinutes: number;
  /** Number of distinct days with any practice. */
  totalPracticeDays: number;
  /** Ids of lessons the learner has completed. */
  completedLessonIds: string[];
  /** Best BPM reached per exercise. */
  bpmRecords: Record<string, number>;
}

export interface Achievement {
  id: string;
  /** Czech display title. */
  title: string;
  /** Czech one-line description of how it is earned. */
  description: string;
  /** True once the milestone is reached. */
  isUnlocked: (ctx: AchievementContext) => boolean;
}

/**
 * The achievement catalogue. Conditions are monotonic — once true for a given
 * history they stay true — so evaluation is stable and unlocks are permanent.
 */
export const ACHIEVEMENTS: Achievement[] = [
  {
    id: "first-practice",
    title: "První cvičení",
    description: "Odcvič svůj první den.",
    isUnlocked: (ctx) => ctx.totalPracticeDays >= 1,
  },
  {
    id: "streak-3",
    title: "Tři dny v řadě",
    description: "Cvič tři dny po sobě.",
    isUnlocked: (ctx) => ctx.longestStreak >= 3,
  },
  {
    id: "streak-7",
    title: "Týden v řadě",
    description: "Cvič sedm dní po sobě.",
    isUnlocked: (ctx) => ctx.longestStreak >= 7,
  },
  {
    id: "streak-30",
    title: "Měsíc v řadě",
    description: "Cvič třicet dní po sobě.",
    isUnlocked: (ctx) => ctx.longestStreak >= 30,
  },
  {
    id: "minutes-60",
    title: "Hodina cvičení",
    description: "Nacvič dohromady 60 minut.",
    isUnlocked: (ctx) => ctx.totalMinutes >= 60,
  },
  {
    id: "minutes-600",
    title: "Deset hodin",
    description: "Nacvič dohromady 600 minut.",
    isUnlocked: (ctx) => ctx.totalMinutes >= 600,
  },
  {
    id: "first-lesson",
    title: "První lekce",
    description: "Dokonči svou první lekci.",
    isUnlocked: (ctx) => ctx.completedLessonIds.length >= 1,
  },
  {
    id: "first-power-chord",
    title: "První power chord",
    description: "Dokonči lekci o power chordech.",
    isUnlocked: (ctx) => ctx.completedLessonIds.includes("power-chord"),
  },
  {
    id: "stage-1-done",
    title: "Stage 1 hotová",
    description: "Dokonči všechny lekce Stage 1.",
    isUnlocked: (ctx) =>
      STAGE_1_LESSON_IDS.length > 0 &&
      STAGE_1_LESSON_IDS.every((id) => ctx.completedLessonIds.includes(id)),
  },
  {
    id: "all-lessons",
    title: "Všechny lekce",
    description: "Dokonči všech " + TOTAL_LESSON_COUNT + " lekcí.",
    isUnlocked: (ctx) => ctx.completedLessonIds.length >= TOTAL_LESSON_COUNT,
  },
  {
    id: "tempo-120",
    title: "Rychlé prsty",
    description: "Zvládni cvičení na 120 BPM.",
    isUnlocked: (ctx) => bestBpm(ctx.bpmRecords) >= 120,
  },
];

/** Looks up an achievement by id. */
export function getAchievement(id: string): Achievement | undefined {
  return ACHIEVEMENTS.find((achievement) => achievement.id === id);
}

/** Builds an evaluation context from stored progress and lesson completion. */
export function buildAchievementContext(
  progress: ProgressData,
  completedLessonIds: string[],
): AchievementContext {
  return {
    longestStreak: longestStreak(progress.dailyMinutes),
    totalMinutes: totalMinutes(progress.dailyMinutes),
    totalPracticeDays: totalPracticeDays(progress.dailyMinutes),
    completedLessonIds,
    bpmRecords: progress.bpmRecords,
  };
}

/** Ids of every achievement whose condition the context satisfies. */
export function evaluateAchievements(ctx: AchievementContext): string[] {
  return ACHIEVEMENTS.filter((achievement) => achievement.isUnlocked(ctx)).map(
    (achievement) => achievement.id,
  );
}

/**
 * Ids of achievements that are satisfied now but are not yet in the supplied
 * already-unlocked list — i.e. the ones that just became earned.
 */
export function newlyUnlockedIds(
  ctx: AchievementContext,
  alreadyUnlocked: string[],
): string[] {
  return evaluateAchievements(ctx).filter(
    (id) => !alreadyUnlocked.includes(id),
  );
}
