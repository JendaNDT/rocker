// Small pure math helpers shared across the lib layer.

/**
 * Clamps a value into the 0..1 inclusive range. Non-finite input (NaN,
 * ±Infinity) falls back to `fallback`. Two named wrappers below cover the
 * two opposite conventions used in the app — use the wrapper, not this
 * helper directly, so the call site spells out which way invalid input
 * should fail.
 */
export function clampUnit(value: number, fallback: number): number {
  if (!Number.isFinite(value)) return fallback;
  return Math.max(0, Math.min(1, value));
}

/**
 * 0..1 gain clamp where invalid input falls back to FULL volume (1).
 * Use for engine-side knobs where a missing or corrupt slider value
 * should not silence the engine — the user would think the app is
 * broken. Example: metronome volume / accent volume / polyrhythm volume.
 */
export function clampGainOrFull(value: number): number {
  return clampUnit(value, 1);
}

/**
 * 0..1 gain clamp where invalid input falls back to SILENT (0).
 * Use for per-asset gains where a missing value should NOT retroactively
 * boost an old take to full volume. Example: multitrack stem gains.
 */
export function clampGainOrSilent(value: number): number {
  return clampUnit(value, 0);
}
