"use client";

import { useEffect, useState } from "react";
import { computeRms } from "@/audio/pitchDetector";

// Maps the RMS of the post-effects signal onto the 0..1 bar. Eye-tunable.
const OUTPUT_LEVEL_SCALE = 4;
// Above this 0..1 level the signal is treated as clipping.
const CLIP_THRESHOLD = 0.92;

/**
 * A VU-style level bar driven by a Web Audio analyser. Generic — the caller
 * passes the analyser to read. Part of the amp expansion (C1); the amp's
 * output meter uses it, fed by AmpChain.outputAnalyser.
 */
export function SignalLevelMeter({
  analyser,
  active,
  label,
}: {
  analyser: AnalyserNode | null;
  active: boolean;
  label: string;
}) {
  const [level, setLevel] = useState(0);

  useEffect(() => {
    if (!active || !analyser) {
      setLevel(0);
      return;
    }
    const buffer = new Float32Array(analyser.fftSize);
    let raf = 0;

    const tick = () => {
      analyser.getFloatTimeDomainData(buffer);
      const target = Math.min(1, computeRms(buffer) * OUTPUT_LEVEL_SCALE);
      // Jump up instantly, decay slowly — VU-meter feel.
      setLevel((prev) => Math.max(target, prev * 0.9));
      raf = requestAnimationFrame(tick);
    };
    tick();

    return () => cancelAnimationFrame(raf);
  }, [active, analyser]);

  const clipping = level >= CLIP_THRESHOLD;
  const widthPct = Math.round(level * 100);

  return (
    <div>
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-text">{label}</span>
        {clipping && (
          <span className="text-xs font-semibold text-danger">
            Přebuzeno — uber hlasitost
          </span>
        )}
      </div>
      <div className="mt-2 h-3 w-full overflow-hidden rounded-full bg-bg">
        <div
          className={clipping ? "h-full bg-danger" : "h-full bg-accent"}
          style={{ width: `${widthPct}%` }}
        />
      </div>
    </div>
  );
}
