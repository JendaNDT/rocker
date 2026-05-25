// Pitch detection wrapper around the Pitchy library (McLeod Pitch Method).
//
// Runs on the main thread: it reads the time-domain data from an AnalyserNode
// and asks Pitchy for the fundamental frequency. For a tuner this is the
// standard, well-proven approach — a tuner only needs a stable reading, not
// the low latency that would justify an AudioWorklet.

import { PitchDetector } from "pitchy";

export interface PitchReading {
  /** Detected fundamental frequency in Hz. */
  frequency: number;
  /** Confidence 0..1. Below ~0.9 the reading should be treated as noise. */
  clarity: number;
}

/**
 * Factor mapping a raw RMS level to the 0..1 scale used by the input level
 * meter and the noise-gate setting. Shared so the meter, the gate slider and
 * the detector all speak the same units.
 */
export const INPUT_LEVEL_SCALE = 3;

/** Root-mean-square level of a time-domain buffer. */
export function computeRms(buffer: Float32Array): number {
  if (buffer.length === 0) return 0;
  let sum = 0;
  for (let i = 0; i < buffer.length; i += 1) {
    sum += buffer[i] * buffer[i];
  }
  return Math.sqrt(sum / buffer.length);
}

/** Upper bound for the noise gate (matches the settings slider maximum). */
export const NOISE_GATE_MAX = 0.5;

/** Headroom added above the measured room noise when auto-calibrating. */
const NOISE_GATE_MARGIN = 0.03;

/**
 * Recommends a noise-gate level from a measured background-noise peak (raw
 * RMS). Places the gate a small margin above the loudest moment of the quiet
 * room, on the 0..1 meter scale, clamped to the gate range and rounded to the
 * slider step. Non-positive or invalid input is treated as silence.
 */
export function recommendedNoiseGate(peakRms: number): number {
  const safePeak = Number.isFinite(peakRms) && peakRms > 0 ? peakRms : 0;
  const withMargin = safePeak * INPUT_LEVEL_SCALE + NOISE_GATE_MARGIN;
  const clamped = Math.min(NOISE_GATE_MAX, Math.max(0, withMargin));
  return Math.round(clamped * 100) / 100;
}

export class PitchAnalyzer {
  private readonly detector: PitchDetector<Float32Array<ArrayBuffer>>;
  private readonly buffer: Float32Array<ArrayBuffer>;

  constructor(bufferSize: number) {
    this.detector = PitchDetector.forFloat32Array(bufferSize);
    this.buffer = new Float32Array(bufferSize);
  }

  /**
   * Reads one frame from the analyser and returns the detected pitch.
   *
   * `noiseGate` is a 0..1 level on the same scale as the input level meter:
   * input quieter than the gate is treated as silence (clarity 0) so ambient
   * room noise can't register as a low-confidence note. 0 disables the gate.
   */
  read(
    analyser: AnalyserNode,
    sampleRate: number,
    noiseGate = 0,
  ): PitchReading {
    analyser.getFloatTimeDomainData(this.buffer);
    if (
      noiseGate > 0 &&
      computeRms(this.buffer) * INPUT_LEVEL_SCALE < noiseGate
    ) {
      return { frequency: 0, clarity: 0 };
    }
    const [frequency, clarity] = this.detector.findPitch(
      this.buffer,
      sampleRate,
    );
    return { frequency, clarity };
  }
}
