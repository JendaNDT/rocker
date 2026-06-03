"use client";

import { useEffect, useState } from "react";
import { audioEngine } from "@/audio/audioEngine";
import { MuteAnalyzer } from "@/audio/muteAnalyzer";
import { isOnset } from "@/lib/palmMute";
import { useAudioStore } from "@/stores/audioStore";

// Input RMS level a hit must cross (rising edge) to count as a new strike.
const ONSET_THRESHOLD = 0.04;
// Ignore new onsets for this long after a strike, so one ringing hit is not
// counted twice.
const REFRACTORY_MS = 320;
// After an onset, sample the note's body over this window — skipping the
// bright pick-attack transient, which looks the same muted or open.
const CAPTURE_START_MS = 40;
const CAPTURE_END_MS = 170;
// Exponential smoothing for the live brightness display (higher = calmer).
const SMOOTHING = 0.7;

export interface MuteDetectorReading {
  /** Live, smoothed spectral centroid of the current frame, in Hz. */
  centroid: number;
  /** Live RMS input level, roughly 0..1. */
  level: number;
  /** Monotonic counter — increments once per detected hit. */
  hitCount: number;
  /** Spectral centroid measured for the most recent hit, or 0 if none yet. */
  lastHitCentroid: number;
}

const SILENT_READING: MuteDetectorReading = {
  centroid: 0,
  level: 0,
  hitCount: 0,
  lastHitCentroid: 0,
};

/**
 * Runs the palm-mute detection loop while the audio engine is running.
 * Returns the live brightness/level plus a hit counter and the centroid of
 * the most recent detected hit. A hit is a rising edge of the input level;
 * its centroid is averaged over a short window after the attack, so the
 * bright pick transient does not mask the palm-muted body of the note.
 */
export function useMuteDetector(): MuteDetectorReading {
  const running = useAudioStore((state) => state.status === "running");
  const [reading, setReading] = useState<MuteDetectorReading>(SILENT_READING);

  useEffect(() => {
    if (!running) {
      setReading(SILENT_READING);
      return;
    }
    const analyser = audioEngine.getAnalyser();
    if (!analyser) {
      setReading(SILENT_READING);
      return;
    }

    const analyzer = new MuteAnalyzer(analyser.fftSize);
    const sampleRate = analyser.context.sampleRate;
    let raf = 0;
    let previousLevel = 0;
    let smoothed = 0;
    let lastHitAt = 0;
    let hitCount = 0;
    let lastHitCentroid = 0;
    let pendingStart = 0;
    let captureSamples: number[] = [];

    const tick = () => {
      const { centroid, level } = analyzer.read(analyser, sampleRate);
      const now = performance.now();

      // Start a pending hit on a rising edge, once past the refractory delay.
      if (
        pendingStart === 0 &&
        isOnset(previousLevel, level, ONSET_THRESHOLD) &&
        now - lastHitAt > REFRACTORY_MS
      ) {
        pendingStart = now;
        captureSamples = [];
      }
      previousLevel = level;

      // Collect centroids over the capture window and commit the hit.
      if (pendingStart !== 0) {
        const sinceOnset = now - pendingStart;
        if (sinceOnset >= CAPTURE_START_MS && sinceOnset < CAPTURE_END_MS) {
          captureSamples.push(centroid);
        } else if (sinceOnset >= CAPTURE_END_MS) {
          if (captureSamples.length > 0) {
            lastHitCentroid =
              captureSamples.reduce((sum, value) => sum + value, 0) /
              captureSamples.length;
            hitCount += 1;
            lastHitAt = pendingStart;
          }
          pendingStart = 0;
        }
      }

      smoothed =
        smoothed > 0 ? smoothed * SMOOTHING + centroid * (1 - SMOOTHING) : centroid;

      setReading({ centroid: smoothed, level, hitCount, lastHitCentroid });
      raf = requestAnimationFrame(tick);
    };
    tick();

    return () => cancelAnimationFrame(raf);
  }, [running]);

  return reading;
}
