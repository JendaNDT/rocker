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

/**
 * Swing / shuffle feel. 0.5 = straight eighths (no swing), 0.667 = triplet
 * jazz swing, 0.75 = sharp dotted shuffle. Only the values inside this range
 * are accepted; everything else clamps to 0.5.
 */
export const SWING_MIN = 0.5;
export const SWING_MAX = 0.75;
/** Default value — neutral, equivalent to "swing off". */
export const DEFAULT_SWING = 0.5;

/** Coerces an untrusted value into a valid swing ratio. */
export function clampSwing(value: unknown): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    return DEFAULT_SWING;
  }
  return Math.max(SWING_MIN, Math.min(SWING_MAX, value));
}

/**
 * Seconds a single subdivision tick should occupy, after applying swing.
 * Even-indexed ticks (0, 2, …) take a `2*swing` share of the pair; odd
 * ticks take the remaining `2*(1-swing)`. The pair sums to `2*baseDuration`,
 * so the beat itself stays exactly on time — only its inner ticks slide.
 *
 * Swing applies to eighths (subdivision = 2) and sixteenths (subdivision = 4).
 * Triplets (3) are already triplet — swinging them would warp them off-feel.
 * Quarters (1) have nothing to swing.
 */
export function swingedTickDuration(
  tickInBeat: number,
  baseDuration: number,
  subdivision: number,
  swing: number,
): number {
  if (subdivision !== 2 && subdivision !== 4) return baseDuration;
  const ratio = clampSwing(swing);
  // The very first tick of every beat sits on the downbeat — for sixteenths
  // the swing pairs are (0,1) and (2,3), so we look at parity within the pair.
  const isOffbeat = tickInBeat % 2 === 1;
  const weight = isOffbeat ? 2 * (1 - ratio) : 2 * ratio;
  return baseDuration * weight;
}

/** True when the current subdivision lets swing have an audible effect. */
export function swingSupported(subdivision: number): boolean {
  return subdivision === 2 || subdivision === 4;
}

/** Maximum number of count-in bars the user can pick. */
export const MAX_COUNT_IN_BARS = 2;

/** Coerces an untrusted value into a valid count-in bar count (0..2). */
export function clampCountInBars(value: unknown): number {
  if (typeof value !== "number" || !Number.isFinite(value)) return 0;
  return Math.max(0, Math.min(MAX_COUNT_IN_BARS, Math.round(value)));
}

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

/**
 * Per-beat sound override. Same length as the accent pattern. Each entry is
 * either a ClickSoundId (this beat plays that sound) or null (this beat falls
 * back to the global `sound` field). Used by the "drum mode" — kick on 1,
 * snare on 2, etc. The whole field is optional on the config; undefined means
 * "no override anywhere, use global sound on every beat".
 */
export type PerBeatSound = (ClickSoundId | null)[];

/**
 * Resizes a per-beat sound pattern to a new bar length, keeping existing
 * entries and padding any new beats with null (= use global sound).
 */
export function resizeSoundPattern(
  pattern: PerBeatSound,
  beatsPerBar: number,
): PerBeatSound {
  const count = clampBeatsPerBar(beatsPerBar);
  return Array.from({ length: count }, (_, index) => {
    const entry = pattern[index];
    return isClickSoundId(entry) ? entry : null;
  });
}

/**
 * Coerces an untrusted value into a valid per-beat sound pattern of the
 * given length, or undefined when the input is missing or empty (all
 * entries fall back to the global sound).
 */
export function sanitizeSoundPattern(
  value: unknown,
  beatsPerBar: number,
): PerBeatSound | undefined {
  if (value === undefined || value === null) return undefined;
  if (!Array.isArray(value)) return undefined;
  const count = clampBeatsPerBar(beatsPerBar);
  const pattern: PerBeatSound = Array.from({ length: count }, (_, index) => {
    const entry = value[index];
    return isClickSoundId(entry) ? entry : null;
  });
  // An all-null pattern is equivalent to "no override" — drop it so the
  // engine and persisted config stay as small as possible.
  if (pattern.every((entry) => entry === null)) return undefined;
  return pattern;
}

/**
 * Coerces an untrusted value into a ClickSoundId for the subdivision tick,
 * or undefined when missing — subdivisions then play the beat's own sound.
 */
export function sanitizeSubdivisionSound(
  value: unknown,
): ClickSoundId | undefined {
  return isClickSoundId(value) ? value : undefined;
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

/**
 * Polyrhythm — a secondary click layer running its own tick count against
 * the main pulse. Classic "3 against 4" drill: ticksPerBeat = 3 against the
 * main subdivision = 4.
 */
export interface PolyrhythmConfig {
  enabled: boolean;
  /** Ticks per main beat; 2..7. Equal to subdivision = no audible polyrhythm. */
  ticksPerBeat: number;
  /** Sound for the secondary layer. */
  sound: ClickSoundId;
  /** Volume relative to the master metronome volume (0..1). */
  volume: number;
}

export const POLYRHYTHM_MIN_TICKS = 2;
export const POLYRHYTHM_MAX_TICKS = 7;

export const DEFAULT_POLYRHYTHM: PolyrhythmConfig = {
  enabled: false,
  ticksPerBeat: 3,
  sound: "hihat",
  volume: 0.6,
};

/** Coerces an untrusted value into a valid polyrhythm config. */
export function sanitizePolyrhythm(value: unknown): PolyrhythmConfig {
  const v = (value ?? {}) as Partial<PolyrhythmConfig>;
  return {
    enabled: v.enabled === true,
    ticksPerBeat: clampInt(
      v.ticksPerBeat,
      POLYRHYTHM_MIN_TICKS,
      POLYRHYTHM_MAX_TICKS,
      DEFAULT_POLYRHYTHM.ticksPerBeat,
    ),
    sound: isClickSoundId(v.sound) ? v.sound : DEFAULT_POLYRHYTHM.sound,
    volume: clamp01(
      typeof v.volume === "number" ? v.volume : DEFAULT_POLYRHYTHM.volume,
    ),
  };
}

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
  /**
   * Swing / shuffle feel for paired subdivisions. 0.5 = straight (off),
   * 0.667 = jazz triplet, 0.75 = dotted shuffle.
   */
  swing: number;
  /**
   * Count-in bars played before the main tempo starts (0 = off, max 2).
   * Trainers (e.g. Power Chord Trainer) override to 0.
   */
  countInBars: number;
  /** Optional secondary polyrhythmic click layer. */
  polyrhythm: PolyrhythmConfig;
  /**
   * Optional per-beat sound override (drum mode). When set, each non-null
   * entry plays its own sound on that beat; null entries fall back to
   * `sound`. When undefined, every beat uses `sound` (= classic metronome).
   */
  soundPattern?: PerBeatSound;
  /**
   * Optional sound used for subdivision ticks (8ths, triplets, 16ths). When
   * set, every subdivision tick plays this sound regardless of the beat it
   * sits on (think hi-hat over kick/snare). When undefined, subdivisions
   * play the beat's own sound.
   */
  subdivisionSound?: ClickSoundId;
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
    swing: DEFAULT_SWING,
    countInBars: 0,
    polyrhythm: { ...DEFAULT_POLYRHYTHM },
    // soundPattern and subdivisionSound stay undefined by default — a fresh
    // metronome behaves like the classic one until the user opens "Pokročilé".
  };
}

/** Coerces an untrusted value into a valid metronome configuration. */
export function sanitizeMetronomeConfig(value: unknown): MetronomeConfig {
  const v = (value ?? {}) as Partial<MetronomeConfig>;
  const beatsPerBar = clampBeatsPerBar(
    typeof v.beatsPerBar === "number" ? v.beatsPerBar : 4,
  );
  const soundPattern = sanitizeSoundPattern(v.soundPattern, beatsPerBar);
  const config: MetronomeConfig = {
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
    swing: clampSwing(v.swing),
    countInBars: clampCountInBars(v.countInBars),
    polyrhythm: sanitizePolyrhythm(v.polyrhythm),
  };
  if (soundPattern) config.soundPattern = soundPattern;
  const subdivisionSound = sanitizeSubdivisionSound(v.subdivisionSound);
  if (subdivisionSound) config.subdivisionSound = subdivisionSound;
  return config;
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
