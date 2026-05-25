"use client";

import { useEffect, useRef, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { PitchAnalyzer } from "@/audio/pitchDetector";
import { frequencyToNote } from "@/lib/pitch";
import type { NoteInfo } from "@/lib/pitch";
import { useAudioStore } from "@/stores/audioStore";
import { useSettingsStore } from "@/stores/settingsStore";

// Below this Pitchy confidence the signal is treated as noise.
const CLARITY_THRESHOLD = 0.9;
// Keep showing the last good note briefly after the string fades.
const SILENCE_HOLD_MS = 600;
// Exponential smoothing factor for a steadier needle (higher = steadier).
const SMOOTHING = 0.8;

export interface TunerReading {
  note: NoteInfo;
  frequency: number;
  clarity: number;
}

/**
 * Runs the pitch-detection loop while the audio engine is running and returns
 * the current note reading (or null when nothing clear is being played).
 */
export function useTuner(): { reading: TunerReading | null } {
  const running = useAudioStore((state) => state.status === "running");
  const [reading, setReading] = useState<TunerReading | null>(null);

  // Read the noise gate through a ref so changing it from the settings slider
  // takes effect live without tearing down and rebuilding the detection loop.
  const noiseGate = useSettingsStore((state) => state.noiseGate);
  const noiseGateRef = useRef(noiseGate);
  noiseGateRef.current = noiseGate;

  useEffect(() => {
    if (!running) {
      setReading(null);
      return;
    }
    const analyser = audioEngine.getAnalyser();
    if (!analyser) {
      setReading(null);
      return;
    }

    const analyzer = new PitchAnalyzer(analyser.fftSize);
    const sampleRate = analyser.context.sampleRate;
    let raf = 0;
    let smoothed = 0;
    let lastGoodAt = performance.now();

    const tick = () => {
      const { frequency, clarity } = analyzer.read(
        analyser,
        sampleRate,
        noiseGateRef.current,
      );
      const now = performance.now();

      if (clarity >= CLARITY_THRESHOLD && frequency > 0) {
        smoothed =
          smoothed > 0
            ? smoothed * SMOOTHING + frequency * (1 - SMOOTHING)
            : frequency;
        const note = frequencyToNote(smoothed);
        if (note) {
          setReading({ note, frequency: smoothed, clarity });
          lastGoodAt = now;
        }
      } else if (now - lastGoodAt > SILENCE_HOLD_MS) {
        smoothed = 0;
        setReading(null);
      }

      raf = requestAnimationFrame(tick);
    };
    tick();

    return () => cancelAnimationFrame(raf);
  }, [running]);

  return { reading };
}
