// Progress tracking — pure logic, no DOM, no audio. Unit-tested in
// progress.test.ts. Powers Bod 10 (streaks, practice minutes, BPM records,
// achievements). The persisted shape lives in progressStore.ts.

/** A calendar day in local time, formatted as "YYYY-MM-DD". */
export type DayKey = string;

/**
 * The persisted progress payload. Small, structured JSON — kept in
 * localStorage via Zustand persist (IndexedDB is reserved for large blobs
 * such as recordings in Bod 11).
 */
export interface ProgressData {
  /** Practiced minutes per day, keyed by DayKey. Days with no practice are absent. */
  dailyMinutes: Record<DayKey, number>;
  /** Best (highest) clean BPM reached per exercise id. */
  bpmRecords: Record<string, number>;
  /** Ids of achievements the learner has unlocked. */
  unlockedAchievements: string[];
}

/** A fresh, empty progress payload. */
export function emptyProgress(): ProgressData {
  return { dailyMinutes: {}, bpmRecords: {}, unlockedAchievements: [] };
}

/** Formats a Date into a local-time DayKey ("YYYY-MM-DD"). */
export function formatDayKey(date: Date): DayKey {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/** The DayKey for today in local time. */
export function todayKey(now: Date = new Date()): DayKey {
  return formatDayKey(now);
}

/**
 * Shifts a DayKey by a number of days (negative goes back in time).
 * Parsed and rebuilt via a local Date so month/year boundaries are correct.
 */
export function shiftDayKey(key: DayKey, deltaDays: number): DayKey {
  const [year, month, day] = key.split("-").map(Number);
  const date = new Date(year, month - 1, day);
  date.setDate(date.getDate() + deltaDays);
  return formatDayKey(date);
}

/** True if the given day has any recorded practice. */
function practiced(dailyMinutes: Record<DayKey, number>, key: DayKey): boolean {
  const minutes = dailyMinutes[key];
  return typeof minutes === "number" && minutes > 0;
}

/** True if the learner has practiced today. */
export function practicedToday(
  dailyMinutes: Record<DayKey, number>,
  today: DayKey = todayKey(),
): boolean {
  return practiced(dailyMinutes, today);
}

/**
 * The current streak: consecutive days with practice ending today or
 * yesterday. Today not yet practiced does not break the streak (the day is
 * still in progress) — it counts from yesterday. Returns 0 if neither today
 * nor yesterday has practice.
 */
export function currentStreak(
  dailyMinutes: Record<DayKey, number>,
  today: DayKey = todayKey(),
): number {
  let cursor = today;
  if (!practiced(dailyMinutes, cursor)) {
    cursor = shiftDayKey(today, -1);
    if (!practiced(dailyMinutes, cursor)) return 0;
  }

  let count = 0;
  while (practiced(dailyMinutes, cursor)) {
    count += 1;
    cursor = shiftDayKey(cursor, -1);
  }
  return count;
}

/** The longest run of consecutive practiced days ever recorded. */
export function longestStreak(dailyMinutes: Record<DayKey, number>): number {
  const days = Object.keys(dailyMinutes)
    .filter((key) => practiced(dailyMinutes, key))
    .sort();
  if (days.length === 0) return 0;

  let longest = 1;
  let run = 1;
  for (let i = 1; i < days.length; i += 1) {
    if (shiftDayKey(days[i - 1], 1) === days[i]) {
      run += 1;
    } else {
      run = 1;
    }
    if (run > longest) longest = run;
  }
  return longest;
}

/** Total practiced minutes across all days. */
export function totalMinutes(dailyMinutes: Record<DayKey, number>): number {
  return Object.values(dailyMinutes).reduce(
    (sum, minutes) => sum + (typeof minutes === "number" ? minutes : 0),
    0,
  );
}

/** Number of distinct days with any recorded practice. */
export function totalPracticeDays(
  dailyMinutes: Record<DayKey, number>,
): number {
  return Object.keys(dailyMinutes).filter((key) =>
    practiced(dailyMinutes, key),
  ).length;
}

// --- Type guards ----------------------------------------------------------
// Storage can be corrupted or written by an older app version. Every value
// loaded from persistence passes through these guards; broken pieces are
// dropped so the app never crashes on bad data (see AGENTS.md).

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Keeps only finite, positive numeric entries from a record. */
function sanitizeNumberRecord(value: unknown): Record<string, number> {
  if (!isPlainObject(value)) return {};
  const result: Record<string, number> = {};
  for (const [key, entry] of Object.entries(value)) {
    if (typeof entry === "number" && Number.isFinite(entry) && entry > 0) {
      result[key] = entry;
    }
  }
  return result;
}

/** Keeps only string entries from an array. */
function sanitizeStringArray(value: unknown): string[] {
  if (!Array.isArray(value)) return [];
  return value.filter((entry): entry is string => typeof entry === "string");
}

/**
 * Coerces unknown persisted data into a valid ProgressData, discarding
 * anything malformed. Always returns a usable object.
 */
export function sanitizeProgress(value: unknown): ProgressData {
  if (!isPlainObject(value)) return emptyProgress();
  return {
    dailyMinutes: sanitizeNumberRecord(value.dailyMinutes),
    bpmRecords: sanitizeNumberRecord(value.bpmRecords),
    unlockedAchievements: Array.from(
      new Set(sanitizeStringArray(value.unlockedAchievements)),
    ),
  };
}
