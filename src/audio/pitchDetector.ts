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

export class PitchAnalyzer {
  private readonly detector: PitchDetector<Float32Array<ArrayBuffer>>;
  private readonly buffer: Float32Array<ArrayBuffer>;

  constructor(bufferSize: number) {
    this.detector = PitchDetector.forFloat32Array(bufferSize);
    this.buffer = new Float32Array(bufferSize);
  }

  /** Reads one frame from the analyser and returns the detected pitch. */
  read(analyser: AnalyserNode, sampleRate: number): PitchReading {
    analyser.getFloatTimeDomainData(this.buffer);
    const [frequency, clarity] = this.detector.findPitch(
      this.buffer,
      sampleRate,
    );
    return { frequency, clarity };
  }
}
