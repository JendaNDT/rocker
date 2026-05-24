// Builds the practice summary that feeds the AI feedback feature. Pure
// logic, no DOM or network. Unit-tested in feedback.test.ts. Part of Bod 14.

import {
  type DayKey,
  currentStreak,
  longestStreak,
  shiftDayKey,
  todayKey,
  totalMinutes,
  totalPracticeDays,
} from "@/lib/progress";

/** The numbers the practice summary is built from. */
export interface PracticeSummaryInput {
  /** Lessons the learner has finished. */
  completedLessons: number;
  /** Total number of lessons available. */
  totalLessons: number;
  /** Practiced minutes per day, keyed by DayKey. */
  dailyMinutes: Record<DayKey, number>;
  /** Best clean BPM reached per exercise id. */
  bpmRecords: Record<string, number>;
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

/**
 * Builds a plain-Czech summary of the learner's practice, ready to send to
 * the AI as the user message for the feedback feature.
 */
export function summarizePractice(
  input: PracticeSummaryInput,
  today: DayKey = todayKey(),
): string {
  const streak = currentStreak(input.dailyMinutes, today);
  const longest = longestStreak(input.dailyMinutes);
  const total = totalMinutes(input.dailyMinutes);
  const days = totalPracticeDays(input.dailyMinutes);
  const week = weekMinutes(input.dailyMinutes, today);
  const bpmValues = Object.values(input.bpmRecords);

  const lines = [
    "Souhrn mého dosavadního cvičení v aplikaci Rocker:",
    `- Hotové lekce: ${input.completedLessons} z ${input.totalLessons}.`,
    streak > 0
      ? `- Aktuální série: ${streak} dní v řadě.`
      : "- Aktuálně nemám rozjetou sérii.",
    `- Nejdelší série: ${longest} dní.`,
    `- Odcvičeno celkem: ${total} minut během ${days} dní.`,
    `- Tento týden: ${week} minut.`,
  ];
  if (bpmValues.length > 0) {
    lines.push(
      `- Zaznamenaná tempa: ${bpmValues.length}, nejvyšší ${Math.max(...bpmValues)} BPM.`,
    );
  }
  lines.push(`- Odemčené achievementy: ${input.achievementCount}.`);

  return lines.join("\n");
}
