// Builds a compact context block about the learner's practice for the AI
// tutor's chat. Pure logic, no DOM or network. Unit-tested in context.test.ts.
// Part of the feature-interconnection plan, phase A (A2).

import {
  type DayKey,
  currentStreak,
  shiftDayKey,
  todayKey,
  totalMinutes,
} from "@/lib/progress";

/** The progress numbers the chat context is built from. */
export interface PracticeContextInput {
  /** Lessons the learner has finished. */
  completedLessons: number;
  /** Total number of lessons available. */
  totalLessons: number;
  /** Practiced minutes per day, keyed by DayKey. */
  dailyMinutes: Record<DayKey, number>;
  /** Number of unlocked achievements. */
  achievementCount: number;
}

/** Sum of practiced minutes over the last seven days, ending on `today`. */
function weekMinutes(
  dailyMinutes: Record<DayKey, number>,
  today: DayKey,
): number {
  let sum = 0;
  for (let i = 0; i < 7; i += 1) {
    const minutes = dailyMinutes[shiftDayKey(today, -i)];
    if (typeof minutes === "number" && minutes > 0) sum += minutes;
  }
  return sum;
}

/** Rounds minutes to a whole number for display in the context block. */
function roundMinutes(minutes: number): number {
  return Math.round(minutes);
}

/**
 * Builds a short Czech context block describing the learner's practice, to
 * be appended to the chat system prompt. The user never sees this text; it
 * lets the tutor tailor its answers to where the learner actually is.
 */
export function buildPracticeContext(
  input: PracticeContextInput,
  today: DayKey = todayKey(),
): string {
  const streak = currentStreak(input.dailyMinutes, today);
  const total = roundMinutes(totalMinutes(input.dailyMinutes));
  const week = roundMinutes(weekMinutes(input.dailyMinutes, today));

  const lines = [
    "Kontext o uživateli (uživatel ho nevidí; využij ho, ať jsou rady konkrétní — ale nečti ho nahlas a nezačínej jím odpověď):",
    `- Hotové lekce: ${input.completedLessons} z ${input.totalLessons}.`,
    streak > 0
      ? `- Aktuální série cvičení: ${streak} dní v řadě.`
      : "- Aktuálně nemá rozjetou sérii cvičení.",
    total > 0
      ? `- Odcvičeno celkem ${total} minut, z toho tento týden ${week}.`
      : "- Zatím nemá žádné odcvičené minuty.",
    `- Odemčené achievementy: ${input.achievementCount}.`,
  ];

  return lines.join("\n");
}
