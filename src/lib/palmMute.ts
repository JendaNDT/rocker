// Palm Mute Studio engine — pure logic, no DOM, audio or React.
// Unit-tested in palmMute.test.ts.
//
// Palm muting damps a string's higher harmonics, making the sound darker.
// "Darkness" is measured here with the spectral centroid — the magnitude-
// weighted mean frequency of the FFT spectrum. Because the absolute centroid
// depends on the guitar, pickup and input gain, the studio first calibrates:
// the learner plays a few open and a few muted reference hits, and the
// threshold is set between the two. Detection is then self-paced — one
// deliberate hit at a time, not a graded stream.

export type MuteTechnique = "muted" | "open";

/**
 * Spectral centroid (the magnitude-weighted mean frequency) of an FFT
 * magnitude spectrum, in Hz. A brighter sound has a higher centroid; palm
 * muting darkens the sound and lowers it. `magnitudes` must hold linear,
 * non-negative magnitudes (one per FFT bin); the audio layer converts the
 * AnalyserNode's dB output before calling this.
 */
export function spectralCentroid(
  magnitudes: ArrayLike<number>,
  sampleRate: number,
): number {
  const fftSize = magnitudes.length * 2;
  let weighted = 0;
  let total = 0;
  for (let i = 0; i < magnitudes.length; i += 1) {
    const magnitude = magnitudes[i];
    const frequency = (i * sampleRate) / fftSize;
    weighted += frequency * magnitude;
    total += magnitude;
  }
  return total > 0 ? weighted / total : 0;
}

/**
 * True when the input level crosses the onset threshold on a rising edge —
 * the start of a new hit. The caller holds the previous level and applies a
 * refractory delay so one ringing hit is not counted twice.
 */
export function isOnset(
  previousLevel: number,
  currentLevel: number,
  threshold: number,
): boolean {
  return previousLevel < threshold && currentLevel >= threshold;
}

// --- Calibration ----------------------------------------------------------

export interface PalmMuteCalibration {
  /** Mean centroid of the open (un-muted) reference hits. */
  openMean: number;
  /** Mean centroid of the muted reference hits. */
  mutedMean: number;
  /** Centroid below which a hit counts as muted. */
  threshold: number;
}

/** Minimum reference hits of each kind needed to calibrate. */
export const MIN_CALIBRATION_SAMPLES = 3;

function mean(values: number[]): number {
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

/**
 * Builds a calibration from reference centroid samples. Returns null when
 * there are too few samples, or when the muted samples are not darker than
 * the open ones (a sign the reference hits were not played as asked).
 */
export function calibrate(
  openSamples: number[],
  mutedSamples: number[],
): PalmMuteCalibration | null {
  if (
    openSamples.length < MIN_CALIBRATION_SAMPLES ||
    mutedSamples.length < MIN_CALIBRATION_SAMPLES
  ) {
    return null;
  }
  const openMean = mean(openSamples);
  const mutedMean = mean(mutedSamples);
  if (openMean <= mutedMean) return null;
  return { openMean, mutedMean, threshold: (openMean + mutedMean) / 2 };
}

/** Classifies a hit's centroid against a calibration. */
export function classifyHit(
  centroid: number,
  calibration: PalmMuteCalibration,
): MuteTechnique {
  return centroid < calibration.threshold ? "muted" : "open";
}

// --- Drills ---------------------------------------------------------------

export interface PalmMuteDrill {
  id: string;
  /** Czech display name. */
  name: string;
  /** Czech one-line description. */
  description: string;
  /** Expected technique for each hit, in order. */
  pattern: MuteTechnique[];
}

export const PALM_MUTE_DRILLS: PalmMuteDrill[] = [
  {
    id: "eight-muted",
    name: "Osm utlumených",
    description: "Zahraj osm utlumených úderů za sebou.",
    pattern: ["muted", "muted", "muted", "muted", "muted", "muted", "muted", "muted"],
  },
  {
    id: "alternating",
    name: "Střídání",
    description: "Střídej utlumený a otevřený úder — celkem osm úderů.",
    pattern: ["muted", "open", "muted", "open", "muted", "open", "muted", "open"],
  },
];

/** Looks up a drill by id. */
export function getPalmMuteDrill(id: string): PalmMuteDrill | undefined {
  return PALM_MUTE_DRILLS.find((drill) => drill.id === id);
}

export interface PalmMuteDrillState {
  drill: PalmMuteDrill;
  /** Detected technique of each logged hit, in order. */
  hits: MuteTechnique[];
}

/** Creates a fresh drill state. */
export function startPalmMuteDrill(drill: PalmMuteDrill): PalmMuteDrillState {
  return { drill, hits: [] };
}

/** True once every hit of the drill pattern has been logged. */
export function isDrillComplete(state: PalmMuteDrillState): boolean {
  return state.hits.length >= state.drill.pattern.length;
}

/** Index of the next hit to play, or -1 when the drill is complete. */
export function currentHitIndex(state: PalmMuteDrillState): number {
  return isDrillComplete(state) ? -1 : state.hits.length;
}

/** Technique expected for the next hit, or null when the drill is complete. */
export function expectedTechnique(
  state: PalmMuteDrillState,
): MuteTechnique | null {
  const index = currentHitIndex(state);
  return index < 0 ? null : state.drill.pattern[index];
}

/** Logs a detected hit; a no-op once the drill is complete. */
export function logDrillHit(
  state: PalmMuteDrillState,
  detected: MuteTechnique,
): PalmMuteDrillState {
  if (isDrillComplete(state)) return state;
  return { ...state, hits: [...state.hits, detected] };
}

/** Number of logged hits that matched the expected technique. */
export function drillScore(state: PalmMuteDrillState): number {
  return state.hits.reduce(
    (score, hit, index) =>
      hit === state.drill.pattern[index] ? score + 1 : score,
    0,
  );
}
