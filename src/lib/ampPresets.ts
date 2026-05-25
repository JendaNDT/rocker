// Amp presets — generic, brand-free named tone settings. Part of Bod 16.
// Pure data, unit-tested in ampPresets.test.ts.

/** Distortion character — the shape of the waveshaper curve. */
export type DistortionType = "overdrive" | "distortion" | "fuzz";

/** Cabinet voicing — which speaker-cabinet simulation to use. */
export type CabinetType = "modern" | "vintage" | "open";

/**
 * The amp parameters a preset configures. Numeric values are normalized:
 * compression, drive, delay, reverb, chorus and level run 0..1; the EQ bands
 * (bass/mid/treble) run -1..1. `distortion` and `cabinet` are enums.
 */
export interface AmpSettings {
  compression: number;
  drive: number;
  distortion: DistortionType;
  bass: number;
  mid: number;
  treble: number;
  cabinet: CabinetType;
  delay: number;
  reverb: number;
  chorus: number;
  level: number;
}

/** A named tone preset. */
export interface AmpPreset {
  id: string;
  /** Czech display name — generic, no instrument brands. */
  name: string;
  settings: AmpSettings;
}

/**
 * Built-in presets. Names are deliberately generic descriptions of the tone,
 * not brand or model names (see ARCHITECTURE.md, section 7).
 */
export const AMP_PRESETS: AmpPreset[] = [
  {
    id: "clean",
    name: "Čistý",
    settings: { compression: 0, drive: 0.1, distortion: "distortion", bass: 0.1, mid: 0, treble: 0.15, cabinet: "modern", delay: 0.1, reverb: 0, chorus: 0, level: 0.6 },
  },
  {
    id: "crunch",
    name: "Crunch",
    settings: { compression: 0, drive: 0.45, distortion: "distortion", bass: 0.1, mid: 0.2, treble: 0.1, cabinet: "modern", delay: 0, reverb: 0, chorus: 0, level: 0.5 },
  },
  {
    id: "lead",
    name: "Lead",
    settings: { compression: 0, drive: 0.7, distortion: "distortion", bass: 0, mid: 0.35, treble: 0.2, cabinet: "modern", delay: 0.35, reverb: 0, chorus: 0, level: 0.5 },
  },
  {
    id: "metal",
    name: "Metal",
    settings: { compression: 0, drive: 0.92, distortion: "distortion", bass: 0.35, mid: -0.3, treble: 0.3, cabinet: "modern", delay: 0, reverb: 0, chorus: 0, level: 0.45 },
  },
];

/** The preset applied when the amp screen first opens. */
export const DEFAULT_AMP_PRESET = AMP_PRESETS[0];

// --- Type guards ----------------------------------------------------------
// User presets are persisted in localStorage and can be corrupt or written
// by an older app version. Stored values pass through these guards; anything
// invalid falls back to a default so the app never crashes on bad data
// (see AGENTS.md).

/** Settings new or malformed presets fall back to — the clean tone. */
const DEFAULT_SETTINGS: AmpSettings = AMP_PRESETS[0].settings;

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

/** Reads one numeric field, clamped to [min, max], or the fallback. */
function numField(
  value: unknown,
  min: number,
  max: number,
  fallback: number,
): number {
  return typeof value === "number" && Number.isFinite(value)
    ? clamp(value, min, max)
    : fallback;
}

/** Reads one enum field — a value from `allowed`, or the fallback. */
function enumField<T extends string>(
  value: unknown,
  allowed: readonly T[],
  fallback: T,
): T {
  return typeof value === "string" &&
    (allowed as readonly string[]).includes(value)
    ? (value as T)
    : fallback;
}

/** Allowed distortion types — also used to validate stored presets. */
export const DISTORTION_TYPES: DistortionType[] = [
  "overdrive",
  "distortion",
  "fuzz",
];

/** Allowed cabinet types — also used to validate stored presets. */
export const CABINET_TYPES: CabinetType[] = ["modern", "vintage", "open"];

/**
 * Coerces an unknown value into AmpSettings. Each field is validated
 * independently; anything missing or invalid falls back to the default
 * (clean) tone. Forward-compatible by design: a preset saved before a
 * parameter existed simply gets that parameter's default — no migration.
 */
export function sanitizeAmpSettings(value: unknown): AmpSettings {
  const obj = isPlainObject(value) ? value : {};
  return {
    compression: numField(obj.compression, 0, 1, DEFAULT_SETTINGS.compression),
    drive: numField(obj.drive, 0, 1, DEFAULT_SETTINGS.drive),
    distortion: enumField(
      obj.distortion,
      DISTORTION_TYPES,
      DEFAULT_SETTINGS.distortion,
    ),
    bass: numField(obj.bass, -1, 1, DEFAULT_SETTINGS.bass),
    mid: numField(obj.mid, -1, 1, DEFAULT_SETTINGS.mid),
    treble: numField(obj.treble, -1, 1, DEFAULT_SETTINGS.treble),
    cabinet: enumField(obj.cabinet, CABINET_TYPES, DEFAULT_SETTINGS.cabinet),
    delay: numField(obj.delay, 0, 1, DEFAULT_SETTINGS.delay),
    reverb: numField(obj.reverb, 0, 1, DEFAULT_SETTINGS.reverb),
    chorus: numField(obj.chorus, 0, 1, DEFAULT_SETTINGS.chorus),
    level: numField(obj.level, 0, 1, DEFAULT_SETTINGS.level),
  };
}

/** Coerces an unknown value into one user preset, or null if unusable. */
function sanitizeUserPreset(value: unknown): AmpPreset | null {
  if (!isPlainObject(value)) return null;
  const { id, name } = value;
  if (typeof id !== "string" || id.length === 0) return null;
  if (typeof name !== "string" || name.trim().length === 0) return null;
  return { id, name, settings: sanitizeAmpSettings(value.settings) };
}

/**
 * Coerces an unknown value into a list of user amp presets. Malformed
 * presets are dropped; always returns a usable array.
 */
export function sanitizeUserPresets(value: unknown): AmpPreset[] {
  if (!Array.isArray(value)) return [];
  return value
    .map(sanitizeUserPreset)
    .filter((preset): preset is AmpPreset => preset !== null);
}
