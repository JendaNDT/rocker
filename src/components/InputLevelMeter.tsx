"use client";

import { useEffect, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { useAudioStore } from "@/stores/audioStore";

// Rough mapping from signal RMS to a 0..1 bar. The meter is a visual aid,
// not a calibrated measurement.
const LEVEL_SCALE = 3;
const CLIP_THRESHOLD = 0.92;

/**
 * Live input level meter. Reads the AudioEngine analyser each animation frame
 * and shows a VU-style bar — the proof that the app actually hears the guitar.
 */
export function InputLevelMeter() {
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
      let sum = 0;
      for (let i = 0; i < buffer.length; i += 1) {
        sum += buffer[i] * buffer[i];
      }
      const rms = Math.sqrt(sum / buffer.length);
      const target = Math.min(1, rms * LEVEL_SCALE);
      // Jump up instantly, decay slowly — VU-meter feel.
      setLevel((prev) => Math.max(target, prev * 0.9));
      raf = requestAnimationFrame(tick);
    };
    tick();

    return () => cancelAnimationFrame(raf);
  }, [running]);

  const clipping = level >= CLIP_THRESHOLD;
  const widthPct = Math.round(level * 100);

  return (
    <div>
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-text">
          Úroveň vstupu
        </span>
        {clipping && (
          <span className="text-xs font-semibold text-danger">
            Příliš silný signál — uber gain
          </span>
        )}
      </div>
      <div className="mt-2 h-3 w-full overflow-hidden rounded-full bg-bg">
        <div
          className={clipping ? "h-full bg-danger" : "h-full bg-accent"}
          style={{ width: `${widthPct}%` }}
        />
      </div>
      <p className="mt-2 text-xs text-muted">
        {running
          ? "Zahraj na kytaru — výchylka ukazuje, že tě aplikace slyší."
          : "Spusť vstup a zahraj, ukáže se úroveň signálu."}
      </p>
    </div>
  );
}
