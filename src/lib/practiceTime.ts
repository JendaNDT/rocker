// Practice-time tracking helpers — pure logic, no DOM. Unit-tested in
// practiceTime.test.ts. Powers Bod 10 (recording practiced minutes per day).

/**
 * Routes whose time counts as practice. Browsing the chord library or the
 * profile is not practice; playing along on these screens is — including the
 * looper, the multi-track studio, the fretboard/ear-training games and the
 * amp simulator. Recorder, notation viewer and live are excluded: browsing
 * sheet music or replaying takes is not active playing.
 */
export const PRACTICE_ROUTES: readonly string[] = [
  "/tuner",
  "/metronome",
  "/practice",
  "/metal",
  "/youtube",
  "/looper",
  "/studio",
  "/games",
  "/amp",
];

/**
 * True if the given pathname is a practice screen whose active time should be
 * counted. Trailing slashes (static export can produce them) are tolerated.
 */
export function isPracticeRoute(pathname: string): boolean {
  const normalized =
    pathname.length > 1 && pathname.endsWith("/")
      ? pathname.slice(0, -1)
      : pathname;
  return PRACTICE_ROUTES.includes(normalized);
}

/**
 * Converts a millisecond duration to minutes. Negative or non-finite input
 * yields 0 so a bad timestamp delta can never corrupt the daily total.
 */
export function msToMinutes(ms: number): number {
  if (!Number.isFinite(ms) || ms <= 0) return 0;
  return ms / 60000;
}
