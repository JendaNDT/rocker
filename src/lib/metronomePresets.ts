// Metronome preset helpers — pure, no React, no audio. Unit-tested.

import type { MetronomeConfig, MetronomePreset } from "./metronomeConfig";
import {
  DEFAULT_POLYRHYTHM,
  defaultMetronomeConfig,
  sanitizeMetronomeConfig,
  sanitizePreset,
} from "./metronomeConfig";

/** Builds a named preset from a configuration, assigning a fresh id. */
export function createPreset(
  name: string,
  config: MetronomeConfig,
): MetronomePreset {
  return {
    id: crypto.randomUUID(),
    name: name.trim(),
    ...sanitizeMetronomeConfig(config),
  };
}

/** Coerces an untrusted value into a list of valid presets. */
export function sanitizePresetList(value: unknown): MetronomePreset[] {
  if (!Array.isArray(value)) return [];
  const presets: MetronomePreset[] = [];
  for (const entry of value) {
    const preset = sanitizePreset(entry);
    if (preset) presets.push(preset);
  }
  return presets;
}

const FACTORY_ID_PREFIX = "factory-";

/** True for any preset whose id was set by the factory list, not the user. */
export function isFactoryPreset(id: string): boolean {
  return id.startsWith(FACTORY_ID_PREFIX);
}

/**
 * Builds a factory preset by overlaying overrides on top of the default
 * configuration and pinning a deterministic id. Everything is funnelled
 * through `sanitizeMetronomeConfig` so factory presets stay valid even if
 * the defaults shift in a later release.
 */
function factoryPreset(
  idSlug: string,
  name: string,
  overrides: Partial<MetronomeConfig>,
): MetronomePreset {
  return {
    id: `${FACTORY_ID_PREFIX}${idSlug}`,
    name,
    ...sanitizeMetronomeConfig({ ...defaultMetronomeConfig(), ...overrides }),
  };
}

const A = "accent" as const;
const N = "normal" as const;
// Drum-mode aproximations — we have 5 synth sounds, not a real drum kit.
// "click" is the sharpest → kick, "wood" is dry → snare, "hihat" → hi-hat.
const KICK = "click";
const SNARE = "wood";
const HIHAT = "hihat";

/**
 * Six built-in presets that lean on every feature added in F1–F4: drum
 * mode (kick/snare/hi-hat), subdivision sound, swing, ramp trainer,
 * polyrhythm. The default config underneath keeps anything not mentioned
 * (volume, count-in, gap, …) at its neutral value.
 */
export const FACTORY_PRESETS: readonly MetronomePreset[] = [
  factoryPreset("blues-shuffle", "Blues shuffle", {
    bpm: 90,
    beatsPerBar: 4,
    subdivision: 2,
    accentPattern: [A, N, A, N],
    swing: 2 / 3,
    sound: "wood",
    soundPattern: [KICK, SNARE, KICK, SNARE],
    subdivisionSound: HIHAT,
  }),
  factoryPreset("metal-gallop", "Metal gallop", {
    bpm: 130,
    beatsPerBar: 4,
    subdivision: 4,
    accentPattern: [A, N, A, N],
    sound: "click",
    soundPattern: [KICK, SNARE, KICK, SNARE],
    ramp: { enabled: true, everyBars: 8, stepBpm: 5, targetBpm: 200 },
  }),
  factoryPreset("funk-groove", "Funk groove", {
    bpm: 100,
    beatsPerBar: 4,
    subdivision: 4,
    accentPattern: [N, A, N, A],
    sound: "wood",
    soundPattern: [KICK, SNARE, KICK, SNARE],
    subdivisionSound: HIHAT,
  }),
  factoryPreset("waltz-3-4", "Waltz 3/4", {
    bpm: 110,
    beatsPerBar: 3,
    subdivision: 1,
    accentPattern: [A, N, N],
    sound: "classic",
  }),
  factoryPreset("jazz-swing", "Jazz swing", {
    bpm: 120,
    beatsPerBar: 4,
    subdivision: 2,
    accentPattern: [N, A, N, A],
    swing: 2 / 3,
    sound: "classic",
    subdivisionSound: HIHAT,
  }),
  factoryPreset("polyrhythm-3-4", "Polyrytmus 3 proti 4", {
    bpm: 80,
    beatsPerBar: 4,
    subdivision: 4,
    accentPattern: [A, N, N, N],
    sound: "click",
    polyrhythm: { ...DEFAULT_POLYRHYTHM, enabled: true, ticksPerBeat: 3 },
  }),
];
