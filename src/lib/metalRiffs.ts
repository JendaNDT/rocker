// Metal/rock riff library for Bod 12. Pure data + types, no audio or React.
//
// Tab model: a riff is an ordered list of notes. Each note has a string index
// (0 = low E, 5 = high E — the same ordering as src/lib/chords.ts), a fret, a
// start position and a duration measured in beats (a quarter note is 1 beat),
// and a playing technique. Notes that share a start time form a chord. This
// format drives both a tab display and tempo-synced playback.
//
// The library is a hybrid: a couple of universally-known riffs (with a real
// artist) plus original riffs written in classic rock/metal styles. The
// original riffs are technique-focused practice material — see `focus` — and
// carry no `artist`.

export type RiffTechnique =
  | "normal"
  | "palm-mute"
  | "slide"
  | "bend"
  | "hammer-on"
  | "pull-off";

export type RiffDifficulty = "beginner" | "intermediate";

export interface RiffNote {
  /** String index: 0 = low E ... 5 = high E. */
  stringIndex: number;
  /** Fret number (0 = open string). */
  fret: number;
  /** Start position from the riff start, in beats. */
  start: number;
  /** Note length in beats (a quarter note is 1). */
  duration: number;
  technique: RiffTechnique;
}

export interface MetalRiff {
  id: string;
  /** Song / riff title. */
  title: string;
  /** Performing artist — set only for real, well-known songs. */
  artist?: string;
  /** Musical style, e.g. "Heavy metal", "Hard rock". */
  style: string;
  /** Technique the riff trains, e.g. "Palm mute", "Gallop". */
  focus: string;
  /** Tuning id — see TUNING_OPEN_MIDI below. */
  tuning: string;
  /** Original / target tempo in BPM. */
  bpm: number;
  difficulty: RiffDifficulty;
  notes: RiffNote[];
}

/**
 * Open-string MIDI notes per tuning, low E (index 0) to high E (index 5).
 * Kept local so this module stays inside the pure-logic layer and does not
 * import the audio layer's tunings. Mirrors src/audio/tunings.ts.
 */
export const TUNING_OPEN_MIDI: Record<string, number[]> = {
  standard: [40, 45, 50, 55, 59, 64],
  "drop-d": [38, 45, 50, 55, 59, 64],
};

export const RIFF_DIFFICULTY_LABELS: Record<RiffDifficulty, string> = {
  beginner: "Začátečník",
  intermediate: "Mírně pokročilý",
};

export const RIFF_TECHNIQUE_LABELS: Record<RiffTechnique, string> = {
  normal: "Normální",
  "palm-mute": "Palm mute",
  slide: "Slide",
  bend: "Bend",
  "hammer-on": "Hammer-on",
  "pull-off": "Pull-off",
};

export const METAL_RIFFS: MetalRiff[] = [
  // --- Iconic riffs ---
  {
    // Beginner one-string version, played on the D string — the form found
    // in essentially every guitar method book.
    id: "smoke-on-the-water",
    title: "Smoke on the Water",
    artist: "Deep Purple",
    style: "Hard rock",
    focus: "Riff na jedné struně",
    tuning: "standard",
    bpm: 113,
    difficulty: "beginner",
    notes: [
      { stringIndex: 2, fret: 0, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 3, start: 0.5, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 5, start: 1, duration: 1, technique: "normal" },
      { stringIndex: 2, fret: 0, start: 2.5, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 3, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 6, start: 3.5, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 5, start: 4, duration: 1, technique: "normal" },
      { stringIndex: 2, fret: 0, start: 5.5, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 3, start: 6, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 5, start: 6.5, duration: 1, technique: "normal" },
      { stringIndex: 2, fret: 3, start: 8, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 0, start: 8.5, duration: 1, technique: "normal" },
    ],
  },
  {
    // Main riff, played on the A string.
    id: "seven-nation-army",
    title: "Seven Nation Army",
    artist: "The White Stripes",
    style: "Rock",
    focus: "Riff na jedné struně",
    tuning: "standard",
    bpm: 124,
    difficulty: "beginner",
    notes: [
      { stringIndex: 1, fret: 7, start: 0, duration: 1.5, technique: "normal" },
      { stringIndex: 1, fret: 7, start: 1.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 10, start: 2, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 7, start: 2.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 5, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 3, start: 3.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 4, duration: 1, technique: "normal" },
    ],
  },

  // --- Original beginner riffs ---
  {
    id: "metal-chug-e",
    title: "Temný chug v E",
    style: "Heavy metal",
    focus: "Palm mute",
    tuning: "standard",
    bpm: 100,
    difficulty: "beginner",
    notes: [
      { stringIndex: 0, fret: 0, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 0.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 1, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 1.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 2, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 2, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 2.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 3, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 3.5, duration: 0.5, technique: "palm-mute" },
    ],
  },
  {
    id: "first-power-chords",
    title: "První power chordy",
    style: "Hard rock",
    focus: "Přechody power chordů",
    tuning: "standard",
    bpm: 110,
    difficulty: "beginner",
    notes: [
      { stringIndex: 0, fret: 0, start: 0, duration: 1, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 0, duration: 1, technique: "normal" },
      { stringIndex: 0, fret: 3, start: 1, duration: 1, technique: "normal" },
      { stringIndex: 1, fret: 5, start: 1, duration: 1, technique: "normal" },
      { stringIndex: 0, fret: 5, start: 2, duration: 1, technique: "normal" },
      { stringIndex: 1, fret: 7, start: 2, duration: 1, technique: "normal" },
      { stringIndex: 0, fret: 3, start: 3, duration: 1, technique: "normal" },
      { stringIndex: 1, fret: 5, start: 3, duration: 1, technique: "normal" },
    ],
  },
  {
    id: "open-e-rocker",
    title: "Otevřená E jízda",
    style: "Rock",
    focus: "Otevřené struny",
    tuning: "standard",
    bpm: 120,
    difficulty: "beginner",
    notes: [
      { stringIndex: 0, fret: 0, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 0.5, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 3, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 1.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 0, start: 2, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 3, start: 2.5, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 3.5, duration: 0.5, technique: "normal" },
    ],
  },
  {
    id: "pentatonic-warmup",
    title: "Pentatonický rozjezd",
    style: "Rock",
    focus: "Pentatonika",
    tuning: "standard",
    bpm: 100,
    difficulty: "beginner",
    notes: [
      { stringIndex: 0, fret: 5, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 8, start: 0.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 5, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 7, start: 1.5, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 5, start: 2, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 7, start: 2.5, duration: 1, technique: "normal" },
    ],
  },
  {
    id: "two-string-stomp",
    title: "Dvě struny",
    style: "Heavy metal",
    focus: "Palm mute",
    tuning: "standard",
    bpm: 110,
    difficulty: "beginner",
    notes: [
      { stringIndex: 0, fret: 0, start: 0, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 0.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 1, fret: 0, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 2, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 1.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 2, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 2.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 1, fret: 0, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 2, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 3.5, duration: 0.5, technique: "palm-mute" },
    ],
  },
  {
    id: "slow-d-riff",
    title: "Pomalý riff v D",
    style: "Hard rock",
    focus: "Přechody power chordů",
    tuning: "standard",
    bpm: 90,
    difficulty: "beginner",
    notes: [
      { stringIndex: 1, fret: 5, start: 0, duration: 2, technique: "normal" },
      { stringIndex: 2, fret: 7, start: 0, duration: 2, technique: "normal" },
      { stringIndex: 1, fret: 3, start: 2, duration: 1, technique: "normal" },
      { stringIndex: 2, fret: 5, start: 2, duration: 1, technique: "normal" },
      { stringIndex: 0, fret: 3, start: 3, duration: 1, technique: "normal" },
      { stringIndex: 1, fret: 5, start: 3, duration: 1, technique: "normal" },
    ],
  },

  // --- Original intermediate riffs ---
  {
    id: "gallop-march",
    title: "Gallopový pochod",
    style: "Heavy metal",
    focus: "Gallop",
    tuning: "standard",
    bpm: 130,
    difficulty: "intermediate",
    notes: [
      { stringIndex: 0, fret: 0, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 0.5, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 0.75, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 1.5, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 1.75, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 2, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 2, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 2.5, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 2.75, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 3.5, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 3.75, duration: 0.25, technique: "palm-mute" },
    ],
  },
  {
    id: "drop-d-crusher",
    title: "Drop D drtič",
    style: "Drop D metal",
    focus: "Palm mute",
    tuning: "drop-d",
    bpm: 120,
    difficulty: "intermediate",
    notes: [
      { stringIndex: 0, fret: 0, start: 0, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 0.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 3, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 3, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 1.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 2, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 2.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 5, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 5, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 3.5, duration: 0.5, technique: "palm-mute" },
    ],
  },
  {
    id: "drop-d-gallop",
    title: "Gallop v Drop D",
    style: "Drop D metal",
    focus: "Gallop",
    tuning: "drop-d",
    bpm: 140,
    difficulty: "intermediate",
    notes: [
      { stringIndex: 0, fret: 0, start: 0, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 0.5, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 0.75, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 1, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 1.5, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 1.75, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 3, start: 2, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 3, start: 2.5, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 3, start: 2.75, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 3, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 3.5, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 3.75, duration: 0.25, technique: "palm-mute" },
    ],
  },
  {
    id: "pentatonic-lick",
    title: "Pentatonický lick",
    style: "Rock",
    focus: "Pentatonika",
    tuning: "standard",
    bpm: 110,
    difficulty: "intermediate",
    notes: [
      { stringIndex: 2, fret: 5, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 7, start: 0.5, duration: 0.5, technique: "hammer-on" },
      { stringIndex: 4, fret: 5, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 4, fret: 8, start: 1.5, duration: 1, technique: "bend" },
      { stringIndex: 5, fret: 5, start: 2.5, duration: 0.5, technique: "normal" },
      { stringIndex: 4, fret: 5, start: 3, duration: 1, technique: "normal" },
    ],
  },
  {
    id: "jumping-power-chords",
    title: "Skákavé power chordy",
    style: "Heavy metal",
    focus: "Přechody power chordů",
    tuning: "standard",
    bpm: 140,
    difficulty: "intermediate",
    notes: [
      { stringIndex: 0, fret: 0, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 0, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 0.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 0.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 3, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 2, fret: 5, start: 1, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 3, start: 1.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 5, start: 1.5, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 5, start: 2, duration: 1, technique: "normal" },
      { stringIndex: 1, fret: 7, start: 2, duration: 1, technique: "normal" },
      { stringIndex: 0, fret: 3, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 5, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 3.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 2, start: 3.5, duration: 0.5, technique: "normal" },
    ],
  },
  {
    id: "syncopated-metal",
    title: "Synkopovaný riff",
    style: "Heavy metal",
    focus: "Palm mute",
    tuning: "standard",
    bpm: 130,
    difficulty: "intermediate",
    notes: [
      { stringIndex: 0, fret: 0, start: 0, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 0.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 1, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 1.25, duration: 0.25, technique: "palm-mute" },
      { stringIndex: 0, fret: 3, start: 1.5, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 5, start: 1.5, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 2, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 0, start: 2.5, duration: 0.5, technique: "palm-mute" },
      { stringIndex: 0, fret: 5, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 1, fret: 7, start: 3, duration: 0.5, technique: "normal" },
      { stringIndex: 0, fret: 0, start: 3.5, duration: 0.5, technique: "palm-mute" },
    ],
  },
];

/** MIDI note a riff note sounds, given the riff's tuning. */
export function riffNoteMidi(riff: MetalRiff, note: RiffNote): number {
  const openMidi = TUNING_OPEN_MIDI[riff.tuning] ?? TUNING_OPEN_MIDI.standard;
  return openMidi[note.stringIndex] + note.fret;
}

/** Total length of a riff in beats — the end of its last-finishing note. */
export function riffDurationBeats(riff: MetalRiff): number {
  return riff.notes.reduce(
    (max, note) => Math.max(max, note.start + note.duration),
    0,
  );
}

/** Looks up a riff by id. */
export function getMetalRiff(id: string): MetalRiff | undefined {
  return METAL_RIFFS.find((riff) => riff.id === id);
}

/**
 * Checks a riff's data for internal consistency and returns a list of
 * human-readable problems (empty when the riff is valid). Used by tests to
 * guard the hand-written riff library against typos.
 */
export function validateRiff(riff: MetalRiff): string[] {
  const problems: string[] = [];
  if (!TUNING_OPEN_MIDI[riff.tuning]) {
    problems.push(`neznámé ladění "${riff.tuning}"`);
  }
  if (!riff.style) problems.push("chybí styl");
  if (!riff.focus) problems.push("chybí focus");
  if (riff.bpm <= 0) problems.push("bpm musí být kladné");
  if (riff.notes.length === 0) problems.push("riff nemá žádné noty");
  riff.notes.forEach((note, index) => {
    if (note.stringIndex < 0 || note.stringIndex > 5) {
      problems.push(`nota ${index}: stringIndex mimo rozsah 0-5`);
    }
    if (note.fret < 0 || note.fret > 24) {
      problems.push(`nota ${index}: fret mimo rozsah 0-24`);
    }
    if (note.start < 0) problems.push(`nota ${index}: start je záporný`);
    if (note.duration <= 0) {
      problems.push(`nota ${index}: duration musí být kladná`);
    }
  });
  return problems;
}
