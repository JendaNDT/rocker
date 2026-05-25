// Metronome preset helpers — pure, no React, no audio. Unit-tested.

import type { MetronomeConfig, MetronomePreset } from "./metronomeConfig";
import { sanitizeMetronomeConfig, sanitizePreset } from "./metronomeConfig";

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
