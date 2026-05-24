// Chord shapes for the library. Standard tuning. Frets are stored low E
// (index 0) to high E (index 5): a number is a fret (0 = open string),
// "x" means the string is muted.

export type FretPosition = number | "x";
export type ChordCategory = "open" | "barre" | "power";

export interface ChordBarre {
  fret: number;
  /** String index where the barre starts (0 = low E). */
  fromString: number;
  /** String index where the barre ends. */
  toString: number;
}

export interface Chord {
  id: string;
  name: string;
  category: ChordCategory;
  /** Lowest fret shown in the 5-fret diagram window. */
  baseFret: number;
  frets: FretPosition[];
  fingers: (number | null)[];
  barre: ChordBarre | null;
}

export const CHORDS: Chord[] = [
  // --- Open chords ---
  {
    id: "E",
    name: "E",
    category: "open",
    baseFret: 1,
    frets: [0, 2, 2, 1, 0, 0],
    fingers: [null, 2, 3, 1, null, null],
    barre: null,
  },
  {
    id: "Em",
    name: "Em",
    category: "open",
    baseFret: 1,
    frets: [0, 2, 2, 0, 0, 0],
    fingers: [null, 2, 3, null, null, null],
    barre: null,
  },
  {
    id: "E7",
    name: "E7",
    category: "open",
    baseFret: 1,
    frets: [0, 2, 0, 1, 0, 0],
    fingers: [null, 2, null, 1, null, null],
    barre: null,
  },
  {
    id: "A",
    name: "A",
    category: "open",
    baseFret: 1,
    frets: ["x", 0, 2, 2, 2, 0],
    fingers: [null, null, 1, 2, 3, null],
    barre: null,
  },
  {
    id: "Am",
    name: "Am",
    category: "open",
    baseFret: 1,
    frets: ["x", 0, 2, 2, 1, 0],
    fingers: [null, null, 2, 3, 1, null],
    barre: null,
  },
  {
    id: "A7",
    name: "A7",
    category: "open",
    baseFret: 1,
    frets: ["x", 0, 2, 0, 2, 0],
    fingers: [null, null, 2, null, 3, null],
    barre: null,
  },
  {
    id: "D",
    name: "D",
    category: "open",
    baseFret: 1,
    frets: ["x", "x", 0, 2, 3, 2],
    fingers: [null, null, null, 1, 3, 2],
    barre: null,
  },
  {
    id: "Dm",
    name: "Dm",
    category: "open",
    baseFret: 1,
    frets: ["x", "x", 0, 2, 3, 1],
    fingers: [null, null, null, 2, 3, 1],
    barre: null,
  },
  {
    id: "D7",
    name: "D7",
    category: "open",
    baseFret: 1,
    frets: ["x", "x", 0, 2, 1, 2],
    fingers: [null, null, null, 2, 1, 3],
    barre: null,
  },
  {
    id: "G",
    name: "G",
    category: "open",
    baseFret: 1,
    frets: [3, 2, 0, 0, 0, 3],
    fingers: [3, 2, null, null, null, 4],
    barre: null,
  },
  {
    id: "G7",
    name: "G7",
    category: "open",
    baseFret: 1,
    frets: [3, 2, 0, 0, 0, 1],
    fingers: [3, 2, null, null, null, 1],
    barre: null,
  },
  {
    id: "C",
    name: "C",
    category: "open",
    baseFret: 1,
    frets: ["x", 3, 2, 0, 1, 0],
    fingers: [null, 3, 2, null, 1, null],
    barre: null,
  },

  // --- Barre chords ---
  {
    id: "F",
    name: "F",
    category: "barre",
    baseFret: 1,
    frets: [1, 3, 3, 2, 1, 1],
    fingers: [1, 3, 4, 2, 1, 1],
    barre: { fret: 1, fromString: 0, toString: 5 },
  },
  {
    id: "Fm",
    name: "Fm",
    category: "barre",
    baseFret: 1,
    frets: [1, 3, 3, 1, 1, 1],
    fingers: [1, 3, 4, 1, 1, 1],
    barre: { fret: 1, fromString: 0, toString: 5 },
  },
  {
    id: "F#m",
    name: "F#m",
    category: "barre",
    baseFret: 1,
    frets: [2, 4, 4, 2, 2, 2],
    fingers: [1, 3, 4, 1, 1, 1],
    barre: { fret: 2, fromString: 0, toString: 5 },
  },
  {
    id: "Bb",
    name: "Bb",
    category: "barre",
    baseFret: 1,
    frets: ["x", 1, 3, 3, 3, 1],
    fingers: [null, 1, 2, 3, 4, 1],
    barre: { fret: 1, fromString: 1, toString: 5 },
  },
  {
    id: "Bm",
    name: "Bm",
    category: "barre",
    baseFret: 1,
    frets: ["x", 2, 4, 4, 3, 2],
    fingers: [null, 1, 3, 4, 2, 1],
    barre: { fret: 2, fromString: 1, toString: 5 },
  },
  {
    id: "B",
    name: "B",
    category: "barre",
    baseFret: 1,
    frets: ["x", 2, 4, 4, 4, 2],
    fingers: [null, 1, 2, 3, 4, 1],
    barre: { fret: 2, fromString: 1, toString: 5 },
  },

  // --- Power chords ---
  {
    id: "E5",
    name: "E5",
    category: "power",
    baseFret: 1,
    frets: [0, 2, 2, "x", "x", "x"],
    fingers: [null, 1, 3, null, null, null],
    barre: null,
  },
  {
    id: "F5",
    name: "F5",
    category: "power",
    baseFret: 1,
    frets: [1, 3, 3, "x", "x", "x"],
    fingers: [1, 3, 4, null, null, null],
    barre: null,
  },
  {
    id: "F#5",
    name: "F#5",
    category: "power",
    baseFret: 1,
    frets: [2, 4, 4, "x", "x", "x"],
    fingers: [1, 3, 4, null, null, null],
    barre: null,
  },
  {
    id: "G5",
    name: "G5",
    category: "power",
    baseFret: 1,
    frets: [3, 5, 5, "x", "x", "x"],
    fingers: [1, 3, 4, null, null, null],
    barre: null,
  },
  {
    id: "A5",
    name: "A5",
    category: "power",
    baseFret: 1,
    frets: ["x", 0, 2, 2, "x", "x"],
    fingers: [null, null, 1, 3, null, null],
    barre: null,
  },
  {
    id: "B5",
    name: "B5",
    category: "power",
    baseFret: 1,
    frets: ["x", 2, 4, 4, "x", "x"],
    fingers: [null, 1, 3, 4, null, null],
    barre: null,
  },
  {
    id: "C5",
    name: "C5",
    category: "power",
    baseFret: 1,
    frets: ["x", 3, 5, 5, "x", "x"],
    fingers: [null, 1, 3, 4, null, null],
    barre: null,
  },
  {
    id: "D5",
    name: "D5",
    category: "power",
    baseFret: 1,
    frets: ["x", "x", 0, 2, 3, "x"],
    fingers: [null, null, null, 1, 3, null],
    barre: null,
  },
];

export const CHORD_CATEGORY_LABELS: Record<ChordCategory, string> = {
  open: "Otevřené akordy",
  barre: "Barré akordy",
  power: "Power chordy",
};

export function getChord(id: string): Chord | undefined {
  return CHORDS.find((chord) => chord.id === id);
}

// Open-string MIDI notes for standard tuning, low E to high E.
const STANDARD_OPEN_MIDI = [40, 45, 50, 55, 59, 64];

/** MIDI notes a chord sounds, low to high. Muted strings are skipped. */
export function chordMidiNotes(chord: Chord): number[] {
  const notes: number[] = [];
  chord.frets.forEach((fret, index) => {
    if (fret !== "x") {
      notes.push(STANDARD_OPEN_MIDI[index] + fret);
    }
  });
  return notes;
}
