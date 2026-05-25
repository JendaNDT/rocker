// Builds compact context blocks about the learner — their practice and their
// instrument — for the AI tutor. Pure logic, no DOM or network. Unit-tested
// in context.test.ts. Part of the feature-interconnection plan (A2, B2).

import {
  PICKUP_TYPE_LABELS,
  POSITION_LABELS,
  type InstrumentProfile,
  type Pickup,
} from "@/lib/instrument";
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

/** Describes the installed pickups, neck to bridge, in plain Czech. */
function describePickups(pickups: Pickup[]): string {
  if (pickups.length === 0) return "neuvedené";
  return pickups
    .map(
      (pickup) =>
        `${POSITION_LABELS[pickup.position].toLowerCase()} ${PICKUP_TYPE_LABELS[pickup.type].toLowerCase()}`,
    )
    .join(", ");
}

/**
 * Builds a short Czech context block describing the learner's guitar, to be
 * appended to an AI system prompt. Empty profile fields are skipped so the
 * block stays compact. The tuning is passed in as a ready label (resolved
 * from settingsStore.tuningId) to keep this module free of the audio layer.
 */
export function buildInstrumentContext(
  profile: InstrumentProfile,
  tuningLabel: string,
): string {
  const lines = [
    "Kontext o uživatelově kytaře (uživatel ho nevidí; zohledni ho v radách o zvuku, technice a nastavení, ale nečti ho nahlas):",
  ];

  const name = profile.name.trim();
  lines.push(`- Nástroj: ${name || "neuvedeno"}.`);

  const body = profile.bodyType.trim();
  if (body) lines.push(`- Tělo: ${body}.`);

  lines.push(`- Ladění: ${tuningLabel}.`);
  lines.push(`- Snímače: ${describePickups(profile.pickups)}.`);

  const scale = profile.scaleLength.trim();
  const gauge = profile.stringGauge.trim();
  const specs: string[] = [];
  if (scale) specs.push(`menzura ${scale}`);
  if (gauge) specs.push(`struny ${gauge}`);
  if (specs.length > 0) lines.push(`- ${specs.join(", ")}.`);

  const notes = profile.notes.trim();
  if (notes) lines.push(`- Poznámky uživatele: ${notes}`);

  return lines.join("\n");
}
