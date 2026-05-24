// Scales as interval sets — semitone offsets from the root (0 = root).
// A fretboard can then light up any root by comparing pitch classes.

export interface Scale {
  id: string;
  name: string;
  intervals: number[];
}

export const SCALES: Scale[] = [
  { id: "minor-pentatonic", name: "Pentatonika moll", intervals: [0, 3, 5, 7, 10] },
  { id: "major-pentatonic", name: "Pentatonika dur", intervals: [0, 2, 4, 7, 9] },
  { id: "major", name: "Dur", intervals: [0, 2, 4, 5, 7, 9, 11] },
  { id: "minor", name: "Moll", intervals: [0, 2, 3, 5, 7, 8, 10] },
  { id: "harmonic-minor", name: "Harmonická moll", intervals: [0, 2, 3, 5, 7, 8, 11] },
  {
    id: "phrygian-dominant",
    name: "Frygická dominantní",
    intervals: [0, 1, 4, 5, 7, 8, 10],
  },
];

export const DEFAULT_SCALE_ID = "minor-pentatonic";

export function getScale(id: string): Scale {
  return SCALES.find((scale) => scale.id === id) ?? SCALES[0];
}

/**
 * If `pitchClass` belongs to `scale` built on `rootPitchClass`, returns the
 * scale degree offset in semitones (0 = root). Otherwise returns null.
 * Pitch classes are 0..11 (0 = C).
 */
export function scaleOffset(
  scale: Scale,
  rootPitchClass: number,
  pitchClass: number,
): number | null {
  const relative = (((pitchClass - rootPitchClass) % 12) + 12) % 12;
  return scale.intervals.includes(relative) ? relative : null;
}
