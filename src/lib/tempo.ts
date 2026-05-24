// Tempo helpers — pure, no DOM. Unit-tested in tempo.test.ts.

export const MIN_BPM = 40;
export const MAX_BPM = 240;

/** Clamps and rounds a BPM value into the supported range. */
export function clampBpm(bpm: number): number {
  if (!Number.isFinite(bpm)) return MIN_BPM;
  return Math.max(MIN_BPM, Math.min(MAX_BPM, Math.round(bpm)));
}

/**
 * Computes BPM from a series of tap timestamps in milliseconds, using the
 * average interval. Returns null if there are fewer than two taps.
 * The caller is responsible for clamping the result into a valid range.
 */
export function bpmFromTaps(timestamps: number[]): number | null {
  if (timestamps.length < 2) return null;

  let total = 0;
  for (let i = 1; i < timestamps.length; i += 1) {
    total += timestamps[i] - timestamps[i - 1];
  }
  const averageInterval = total / (timestamps.length - 1);
  if (averageInterval <= 0) return null;

  return Math.round(60000 / averageInterval);
}
