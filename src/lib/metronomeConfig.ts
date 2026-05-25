// Metronome configuration — pure types, defaults and sanitisers.
// Architecture layer 3 (lib): no React, no audio, unit-tested.

import { clampBpm } from "./tempo";

/** Accent level applied to a single beat in the bar. */
export type AccentLevel = "muted" | "normal" | "accent";

/** Cycle order when the user taps a beat in the indicator. */
const ACCENT_CYCLE: AccentLevel[] = ["normal", "accent", "muted"];

/** Returns the next accent level in the tap cycle. */
export function cycleAccent(level: AccentLevel): AccentLevel {
  const index = ACCENT_CYCLE.indexOf(level);
  return ACCENT_CYCLE[(index + 1) % ACCENT_CYCLE.length];
}

function isAccentLevel(value: unknown): value is AccentLevel {
  return value === "muted" || value === "normal" || value === "accent";
}

export const MIN_BEATS_PER_BAR = 1;
export const MAX_BEATS_PER_BAR = 16;

/** Clamps a beats-per-bar value into the supported range. */
export function clampBeatsPerBar(beats: number): number {
  if (!Number.isFinite(beats)) return 4;
  return Math.max(
    MIN_BEATS_PER_BAR,
    Math.min(MAX_BEATS_PER_BAR, Math.round(beats)),
  );
}

const VALID_SUBDIVISIONS = [1, 2, 3, 4];

/** Clamps a subdivision to one of the supported values (1, 2, 3, 4). */
export function clampSubdivision(value: number): number {
  return VALID_SUBDIVISIONS.includes(value) ? value : 1;
}

/** Clamps a 0..1 gain value; invalid input falls back to full volume. */
export function clamp01(value: number): number {
  if (!Number.isFinite(value)) return 1;
  return Math.max(0, Math.min(1, value));
}

/** First beat accented, every other beat normal. */
export function defaultAccentPattern(beatsPerBar: number): AccentLevel[] {
  const count = clampBeatsPerBar(beatsPerBar);
  return Array.from({ length: count }, (_, index) =>
    index === 0 ? "accent" : "normal",
  );
}

/**
 * Resizes an accent pattern to a new bar length, keeping existing entries
 * and padding any new beats with "normal".
 */
export function resizeAccentPattern(
  pattern: AccentLevel[],
  beatsPerBar: number,
): AccentLevel[] {
  const count = clampBeatsPerBar(beatsPerBar);
  return Array.from({ length: count }, (_, index) => pattern[index] ?? "normal");
}

/**
 * Coerces an untrusted value into a valid accent pattern of the given length.
 * Used when loading a pattern from storage.
 */
export function sanitizeAccentPattern(
  value: unknown,
  beatsPerBar: number,
): AccentLevel[] {
  const count = clampBeatsPerBar(beatsPerBar);
  const source = Array.isArray(value) ? value : [];
  return Array.from({ length: count }, (_, index) => {
    const entry = source[index];
    if (isAccentLevel(entry)) return entry;
    return index === 0 ? "accent" : "normal";
  });
}

/** A selectable time signature with its natural accent pattern. */
export interface TimeSignature {
  id: string;
  label: string;
  beatsPerBar: number;
  defaultAccents: AccentLevel[];
}

const A: AccentLevel = "accent";
const N: AccentLevel = "normal";

/** Time signatures offered on the metronome screen. */
export const TIME_SIGNATURES: TimeSignature[] = [
  { id: "2/4", label: "2/4", beatsPerBar: 2, defaultAccents: [A, N] },
  { id: "3/4", label: "3/4", beatsPerBar: 3, defaultAccents: [A, N, N] },
  { id: "4/4", label: "4/4", beatsPerBar: 4, defaultAccents: [A, N, N, N] },
  { id: "5/4", label: "5/4", beatsPerBar: 5, defaultAccents: [A, N, N, A, N] },
  { id: "6/8", label: "6/8", beatsPerBar: 6, defaultAccents: [A, N, N, A, N, N] },
  { id: "7/8", label: "7/8", beatsPerBar: 7, defaultAccents: [A, N, A, N, A, N, N] },
  {
    id: "12/8",
    label: "12/8",
    beatsPerBar: 12,
    defaultAccents: [A, N, N, A, N, N, A, N, N, A, N, N],
  },
];

/** Identifier of a synthesised click sound. */
export type ClickSoundId = "classic" | "wood" | "beep" | "click" | "hihat";

/** Click sounds offered on the metronome screen (all synthesised). */
export const CLICK_SOUNDS: { id: ClickSoundId; label: string }[] = [
  { id: "classic", label: "Klasik" },
  { id: "wood", label: "Dřevo" },
  { id: "beep", label: "Pípnutí" },
  { id: "click", label: "Cvak" },
  { id: "hihat", label: "Hi-hat" },
];

const CLICK_SOUND_IDS: ClickSoundId[] = CLICK_SOUNDS.map((sound) => sound.id);

export function isClickSoundId(value: unknown): value is ClickSoundId {
  return (
    typeof value === "string" &&
    CLICK_SOUND_IDS.includes(value as ClickSoundId)
  );
}

/** Progressive speed-up trainer: raise the tempo every few bars. */
export interface RampConfig {
  enabled: boolean;
  /** Raise the tempo once every this many bars. */
  everyBars: number;
  /** BPM added on each raise. */
  stepBpm: number;
  /** Stop raising once the tempo reaches this BPM. */
  targetBpm: number;
}

/** Gap trainer: alternate audible and fully silent bars. */
export interface GapConfig {
  enabled: boolean;
  audibleBars: number;
  silentBars: number;
}

export const DEFAULT_RAMP: RampConfig = {
  enabled: false,
  everyBars: 4,
  stepBpm: 5,
  targetBpm: 160,
};

export const DEFAULT_GAP: GapConfig = {
  enabled: false,
  audibleBars: 4,
  silentBars: 1,
};

function clampInt(
  value: unknown,
  min: number,
  max: number,
  fallback: number,
): number {
  if (typeof value !== "number" || !Number.isFinite(value)) return fallback;
  return Math.max(min, Math.min(max, Math.round(value)));
}

/** Coerces an untrusted value into a valid ramp config. */
export function sanitizeRamp(value: unknown): RampConfig {
  const v = (value ?? {}) as Partial<RampConfig>;
  return {
    enabled: v.enabled === true,
    everyBars: clampInt(v.everyBars, 1, 16, DEFAULT_RAMP.everyBars),
    stepBpm: clampInt(v.stepBpm, 1, 20, DEFAULT_RAMP.stepBpm),
    targetBpm: clampBpm(
      typeof v.targetBpm === "number" ? v.targetBpm : DEFAULT_RAMP.targetBpm,
    ),
  };
}

/** Coerces an untrusted value into a valid gap config. */
export function sanitizeGap(value: unknown): GapConfig {
  const v = (value ?? {}) as Partial<GapConfig>;
  return {
    enabled: v.enabled === true,
    audibleBars: clampInt(v.audibleBars, 1, 16, DEFAULT_GAP.audibleBars),
    silentBars: clampInt(v.silentBars, 1, 16, DEFAULT_GAP.silentBars),
  };
}

/** Whether a given 0-based bar falls in the silent phase of the gap trainer. */
export function isSilentBar(bar: number, gap: GapConfig): boolean {
  if (!gap.enabled || bar < 0) return false;
  const cycle = gap.audibleBars + gap.silentBars;
  if (cycle <= 0) return false;
  return bar % cycle >= gap.audibleBars;
}

/**
 * The tempo after one ramp raise. Returns the current BPM unchanged once the
 * target is reached — the trainer only ever speeds up.
 */
export function nextRampBpm(currentBpm: number, ramp: RampConfig): number {
  if (currentBpm >= ramp.targetBpm) return currentBpm;
  return Math.min(currentBpm + ramp.stepBpm, ramp.targetBpm);
}

/** The full persistable metronome configuration. */
export interface MetronomeConfig {
  bpm: number;
  beatsPerBar: number;
  subdivision: number;
  accentPattern: AccentLevel[];
  sound: ClickSoundId;
  /** Master metronome volume (0..1). */
  volume: number;
  /** Extra gain applied to accented beats (0..1). */
  accentVolume: number;
  ramp: RampConfig;
  gap: GapConfig;
}

/** A named, saved metronome configuration. */
export interface MetronomePreset extends MetronomeConfig {
  id: string;
  name: string;
}

/** The default configuration for a fresh metronome. */
export function defaultMetronomeConfig(): MetronomeConfig {
  return {
    bpm: 100,
    beatsPerBar: 4,
    subdivision: 1,
    accentPattern: defaultAccentPattern(4),
    sound: "classic",
    volume: 0.8,
    accentVolume: 1,
    ramp: { ...DEFAULT_RAMP },
    gap: { ...DEFAULT_GAP },
  };
}

/** Coerces an untrusted value into a valid metronome configuration. */
export function sanitizeMetronomeConfig(value: unknown): MetronomeConfig {
  const v = (value ?? {}) as Partial<MetronomeConfig>;
  const beatsPerBar = clampBeatsPerBar(
    typeof v.beatsPerBar === "number" ? v.beatsPerBar : 4,
  );
  return {
    bpm: clampBpm(typeof v.bpm === "number" ? v.bpm : 100),
    beatsPerBar,
    subdivision: clampSubdivision(
      typeof v.subdivision === "number" ? v.subdivision : 1,
    ),
    accentPattern: sanitizeAccentPattern(v.accentPattern, beatsPerBar),
    sound: isClickSoundId(v.sound) ? v.sound : "classic",
    volume: clamp01(typeof v.volume === "number" ? v.volume : 0.8),
    accentVolume: clamp01(
      typeof v.accentVolume === "number" ? v.accentVolume : 1,
    ),
    ramp: sanitizeRamp(v.ramp),
    gap: sanitizeGap(v.gap),
  };
}

/** Type guard: a loosely-typed value is shaped like a stored preset. */
export function isMetronomePreset(value: unknown): value is MetronomePreset {
  if (typeof value !== "object" || value === null) return false;
  const v = value as Record<string, unknown>;
  return typeof v.id === "string" && typeof v.name === "string";
}

/** Coerces an untrusted value into a valid preset, or null if unusable. */
export function sanitizePreset(value: unknown): MetronomePreset | null {
  if (!isMetronomePreset(value)) return null;
  const name = value.name.trim();
  if (name.length === 0) return null;
  return { id: value.id, name, ...sanitizeMetronomeConfig(value) };
}
