// Amp presets — generic, brand-free named tone settings. Part of Bod 16.
// Pure data, unit-tested in ampPresets.test.ts.

/**
 * The amp parameters a preset configures. Values are normalized: drive,
 * delay and level run 0..1; the EQ bands (bass/mid/treble) run -1..1.
 */
export interface AmpSettings {
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
    settings: { drive: 0.1, bass: 0.1, mid: 0, treble: 0.15, delay: 0.1, level: 0.6 },
  },
  {
    id: "crunch",
    name: "Crunch",
    settings: { drive: 0.45, bass: 0.1, mid: 0.2, treble: 0.1, delay: 0, level: 0.5 },
  },
  {
    id: "lead",
    name: "Lead",
    settings: { drive: 0.7, bass: 0, mid: 0.35, treble: 0.2, delay: 0.35, level: 0.5 },
  },
  {
    id: "metal",
    name: "Metal",
    settings: { drive: 0.92, bass: 0.35, mid: -0.3, treble: 0.3, delay: 0, level: 0.45 },
  },
];

/** The preset applied when the amp screen first opens. */
export const DEFAULT_AMP_PRESET = AMP_PRESETS[0];
