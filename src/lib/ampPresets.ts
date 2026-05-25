// Amp presets — generic, brand-free named tone settings. Part of Bod 16.
// Pure data, unit-tested in ampPresets.test.ts.

/**
 * The amp parameters a preset configures. Values are normalized: compression,
 * drive, delay and level run 0..1; the EQ bands (bass/mid/treble) run -1..1.
 */
export interface AmpSettings {
  compression: number;
  drive: number;
  bass: number;
  mid: number;
  treble: number;
  delay: number;
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
    settings: { compression: 0, drive: 0.1, bass: 0.1, mid: 0, treble: 0.15, delay: 0.1, level: 0.6 },
  },
  {
    id: "crunch",
    name: "Crunch",
    settings: { compression: 0, drive: 0.45, bass: 0.1, mid: 0.2, treble: 0.1, delay: 0, level: 0.5 },
  },
  {
    id: "lead",
    name: "Lead",
    settings: { compression: 0, drive: 0.7, bass: 0, mid: 0.35, treble: 0.2, delay: 0.35, level: 0.5 },
  },
  {
    id: "metal",
    name: "Metal",
    settings: { compression: 0, drive: 0.92, bass: 0.35, mid: -0.3, treble: 0.3, delay: 0, level: 0.45 },
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
    bass: numField(obj.bass, -1, 1, DEFAULT_SETTINGS.bass),
    mid: numField(obj.mid, -1, 1, DEFAULT_SETTINGS.mid),
    treble: numField(obj.treble, -1, 1, DEFAULT_SETTINGS.treble),
    delay: numField(obj.delay, 0, 1, DEFAULT_SETTINGS.delay),
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
