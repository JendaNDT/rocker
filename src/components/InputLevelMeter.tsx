"use client";

import { useEffect, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { INPUT_LEVEL_SCALE, computeRms } from "@/audio/pitchDetector";
import { useAudioStore } from "@/stores/audioStore";

// Above this 0..1 level the signal is treated as clipping.
const CLIP_THRESHOLD = 0.92;

/**
 * Live input level meter. Reads the AudioEngine analyser each animation frame
 * and shows a VU-style bar — the proof that the app actually hears the guitar.
 *
 * When `threshold` (0..1) is given, a marker line is drawn at that level so
 * the user can calibrate the noise gate against what the meter shows.
 */
export function InputLevelMeter({ threshold }: { threshold?: number }) {
  const running = useAudioStore((state) => state.status === "running");
  const [level, setLevel] = useState(0);

  useEffect(() => {
    if (!running) {
      setLevel(0);
      return;
    }
    const analyser = audioEngine.getAnalyser();
    if (!analyser) return;

    const buffer = new Float32Array(analyser.fftSize);
    let raf = 0;

    const tick = () => {
      analyser.getFloatTimeDomainData(buffer);
      const target = Math.min(1, computeRms(buffer) * INPUT_LEVEL_SCALE);
      // Jump up instantly, decay slowly — VU-meter feel.
      setLevel((prev) => Math.max(target, prev * 0.9));
      raf = requestAnimationFrame(tick);
    };
    tick();

    return () => cancelAnimationFrame(raf);
  }, [running]);

  const clipping = level >= CLIP_THRESHOLD;
  const widthPct = Math.round(level * 100);
  const showThreshold = threshold !== undefined && threshold > 0;

  return (
    <div>
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-text">Úroveň vstupu</span>
        {clipping && (
          <span className="text-xs font-semibold text-danger">
            Příliš silný signál — uber gain
          </span>
        )}
      </div>
      <div className="relative mt-2 h-3 w-full overflow-hidden rounded-full bg-bg">
        <div
          className={clipping ? "h-full bg-danger" : "h-full bg-accent"}
          style={{ width: `${widthPct}%` }}
        />
        {showThreshold && (
          <div
            className="absolute inset-y-0 w-0.5 bg-text"
            style={{ left: `${Math.min(100, threshold * 100)}%` }}
            aria-hidden="true"
          />
        )}
      </div>
      <p className="mt-2 text-xs text-muted">
        {running
          ? showThreshold
            ? "Zahraj na kytaru. Svislá čárka je práh šumové brány — co je nalevo od ní, aplikace neslyší."
            : "Zahraj na kytaru — výchylka ukazuje, že tě aplikace slyší."
          : "Spusť vstup a zahraj, ukáže se úroveň signálu."}
      </p>
    </div>
  );
}
