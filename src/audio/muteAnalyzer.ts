// Mute analyzer — reads the live input AnalyserNode for the Palm Mute Studio.
//
// Runs on the main thread, like the pitch detector: each frame it reads the
// FFT spectrum and the time-domain signal from the shared input AnalyserNode
// and returns two numbers — the spectral centroid (sound "brightness", lower
// when palm-muted) and the RMS level (used to detect the start of a hit).
// The brightness maths lives in the pure layer (src/lib/palmMute.ts).

import { spectralCentroid } from "@/lib/palmMute";

export interface MuteReading {
  /** Spectral centroid of the current frame in Hz — the sound's brightness. */
  centroid: number;
  /** RMS level of the current frame, roughly 0..1. */
  level: number;
}

export class MuteAnalyzer {
  /** FFT magnitudes in dB, one per bin (length = fftSize / 2). */
  private readonly frequencyDb: Float32Array<ArrayBuffer>;
  /** Linear magnitudes derived from frequencyDb, reused each frame. */
  private readonly magnitudes: Float32Array<ArrayBuffer>;
  /** Time-domain samples, used for the RMS level. */
  private readonly timeDomain: Float32Array<ArrayBuffer>;

  constructor(fftSize: number) {
    this.frequencyDb = new Float32Array(fftSize / 2);
    this.magnitudes = new Float32Array(fftSize / 2);
    this.timeDomain = new Float32Array(fftSize);
  }

  /** Reads one frame from the analyser and returns brightness and level. */
  read(analyser: AnalyserNode, sampleRate: number): MuteReading {
    analyser.getFloatFrequencyData(this.frequencyDb);
    for (let i = 0; i < this.frequencyDb.length; i += 1) {
      // Convert decibels to a linear magnitude; -Infinity (silence) -> 0.
      this.magnitudes[i] = 10 ** (this.frequencyDb[i] / 20);
    }
    const centroid = spectralCentroid(this.magnitudes, sampleRate);

    analyser.getFloatTimeDomainData(this.timeDomain);
    let sumSquares = 0;
    for (let i = 0; i < this.timeDomain.length; i += 1) {
      sumSquares += this.timeDomain[i] * this.timeDomain[i];
    }
    const level = Math.sqrt(sumSquares / this.timeDomain.length);

    return { centroid, level };
  }
}
