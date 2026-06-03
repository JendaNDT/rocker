"use client";

import { useEffect, useState } from "react";
import { audioEngine } from "@/audio/audioEngine";
import { INPUT_LEVEL_SCALE, computeRms } from "@/audio/pitchDetector";
import { useActiveTheme } from "@/hooks/useActiveTheme";
import { useAudioStore } from "@/stores/audioStore";

// Above this 0..1 level the signal is treated as clipping.
const CLIP_THRESHOLD = 0.92;

// LED ladder colour boundaries (0..1 level). Below LED_GREEN_MAX the segment
// glows green, between green and yellow it goes amber, above yellow it goes
// red. Mirrors what a real amp meter does.
const LED_GREEN_MAX = 0.6;
const LED_YELLOW_MAX = 0.85;
const LED_COUNT = 18;

/**
 * Live input level meter. Reads the AudioEngine analyser each animation frame
 * and shows a VU-style bar — the proof that the app actually hears the guitar.
 *
 * When `threshold` (0..1) is given, a marker line is drawn at that level so
 * the user can calibrate the noise gate against what the meter shows.
 *
 * Polymorphic per theme (same pattern as CentsBar):
 *  - studio  : 18-segment LED ladder in a brass frame
 *  - vintage : analogue VU dial with an ink needle behind a brass bezel
 *  - glassy  : frosted track with a pink → cyan glowing fill
 *  - default : original simple pill bar — also the pre-hydration fallback
 */
export function InputLevelMeter({ threshold }: { threshold?: number }) {
  const running = useAudioStore((state) => state.status === "running");
  const [level, setLevel] = useState(0);
  const theme = useActiveTheme();

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
  const showThreshold = threshold !== undefined && threshold > 0;
  const thresholdPct = showThreshold
    ? Math.min(100, (threshold ?? 0) * 100)
    : null;

  const variantProps: VariantProps = {
    level,
    clipping,
    thresholdPct,
  };

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
      <div className="mt-2">
        {theme === "studio" ? (
          <StudioVariant {...variantProps} />
        ) : theme === "vintage" ? (
          <VintageVariant {...variantProps} />
        ) : theme === "glassy" ? (
          <GlassyVariant {...variantProps} />
        ) : (
          <DefaultVariant {...variantProps} />
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

type VariantProps = {
  level: number;
  clipping: boolean;
  thresholdPct: number | null;
};

/** Original render — also the safe pre-hydration fallback. */
function DefaultVariant({ level, clipping, thresholdPct }: VariantProps) {
  const widthPct = Math.round(level * 100);
  return (
    <div className="relative h-3 w-full overflow-hidden rounded-full bg-bg">
      <div
        className={clipping ? "h-full bg-danger" : "h-full bg-accent"}
        style={{ width: `${widthPct}%` }}
      />
      {thresholdPct !== null && (
        <div
          className="absolute inset-y-0 w-0.5 bg-text"
          style={{ left: `${thresholdPct}%` }}
          aria-hidden="true"
        />
      )}
    </div>
  );
}

/**
 * STUDIO — 18-segment LED ladder in a brass frame. The bottom 60% of segments
 * light green, the next band amber, the top band red — just like a real amp
 * level meter. Active segments stop at the current level; everything beyond
 * is dimmed. When clipping, the top reds pulse.
 */
function StudioVariant({ level, clipping, thresholdPct }: VariantProps) {
  return (
    <div
      className="relative h-6 w-full rounded-md p-1"
      style={{
        background:
          "linear-gradient(180deg, rgba(0,0,0,0.55), rgba(0,0,0,0.35))",
        border: "1px solid var(--line-strong)",
        boxShadow:
          "inset 0 1px 2px rgba(0,0,0,0.7), 0 0 0 1px rgba(180,138,74,0.15)",
      }}
    >
      <div className="flex h-full w-full gap-[2px]">
        {Array.from({ length: LED_COUNT }, (_, i) => {
          const segmentFloor = i / LED_COUNT;
          const segmentCenter = (i + 0.5) / LED_COUNT;
          const active = level >= segmentFloor + 1 / (LED_COUNT * 2);
          let colour = "#3a6d27"; // dim green
          let activeColour = "#7eb567";
          if (segmentCenter >= LED_YELLOW_MAX) {
            colour = "#5a2818";
            activeColour = "#e85a3a";
          } else if (segmentCenter >= LED_GREEN_MAX) {
            colour = "#6e5028";
            activeColour = "#e8a04a";
          }
          const isTopRedClipping =
            clipping && segmentCenter >= LED_YELLOW_MAX;
          return (
            <span
              key={i}
              aria-hidden="true"
              className="flex-1 rounded-[1px]"
              style={{
                background: active ? activeColour : colour,
                opacity: active ? 1 : 0.35,
                boxShadow: active
                  ? `0 0 4px ${activeColour}, inset 0 0 1px rgba(255,255,255,0.4)`
                  : "inset 0 1px 1px rgba(0,0,0,0.4)",
                animation: isTopRedClipping
                  ? "rocker-pulse 0.4s ease-in-out infinite alternate"
                  : undefined,
                transition: "background 60ms linear, opacity 60ms linear",
              }}
            />
          );
        })}
      </div>
      {thresholdPct !== null && (
        <span
          aria-hidden="true"
          className="absolute h-3 w-0.5"
          style={{
            left: `${thresholdPct}%`,
            bottom: "-6px",
            background: "var(--brass)",
            boxShadow: "0 0 2px rgba(0,0,0,0.5)",
          }}
        />
      )}
    </div>
  );
}

/**
 * VINTAGE — analogue VU dial. Paper scale behind a brass bezel, hand-painted
 * tick marks at 25/50/75/100 percent and a soft "0" label at midpoint. A dark
 * ink needle hovers above the active level; a small red LED on the right
 * lights up when the signal clips.
 */
function VintageVariant({ level, clipping, thresholdPct }: VariantProps) {
  const needlePct = Math.max(0, Math.min(100, level * 100));
  const ticks = [0, 25, 50, 75, 100];
  return (
    <div
      className="relative h-10 w-full rounded-sm"
      style={{
        background: "var(--maple-light, var(--surface-2))",
        backgroundImage: "var(--paper-grain)",
        border: "1px solid var(--line-strong)",
        boxShadow:
          "inset 0 1px 0 rgba(255,250,232,0.6), inset 0 0 14px rgba(80,60,30,0.25), 0 0 0 1px rgba(140,100,50,0.3)",
      }}
    >
      {/* Inked tick scale. */}
      {ticks.map((pct) => (
        <span
          key={pct}
          aria-hidden="true"
          className="absolute top-1 h-2 w-px -translate-x-1/2"
          style={{
            left: `${pct}%`,
            background: pct === 50 ? "var(--text)" : "var(--dim)",
            opacity: pct === 50 ? 0.8 : 0.5,
          }}
        />
      ))}
      <span
        aria-hidden="true"
        className="absolute top-3 left-1/2 -translate-x-1/2 font-mono text-[9px]"
        style={{ color: "var(--text)", opacity: 0.7 }}
      >
        0
      </span>
      {/* Ink needle — narrow vertical line with a slight smoothing transition. */}
      <span
        aria-hidden="true"
        className="absolute bottom-1 top-6 w-0.5 -translate-x-1/2"
        style={{
          left: `${needlePct}%`,
          background: "var(--text)",
          opacity: 0.85,
          boxShadow: "0 0 2px rgba(40,20,8,0.6)",
          transition: "left 90ms ease-out",
        }}
      />
      {/* Threshold marker — ink tick below the needle base. */}
      {thresholdPct !== null && (
        <span
          aria-hidden="true"
          className="absolute h-2 w-px -translate-x-1/2"
          style={{
            left: `${thresholdPct}%`,
            bottom: "-2px",
            background: "var(--accent)",
            opacity: 0.8,
          }}
        />
      )}
      {/* Clip LED on the right. */}
      <span
        aria-hidden="true"
        className="absolute top-1.5 right-1.5 h-2 w-2 rounded-full"
        style={{
          background: clipping ? "var(--danger)" : "rgba(60,20,12,0.4)",
          boxShadow: clipping
            ? "0 0 6px var(--danger), 0 0 2px var(--danger)"
            : "inset 0 0 2px rgba(0,0,0,0.5)",
          transition: "background 100ms linear, box-shadow 100ms linear",
        }}
      />
    </div>
  );
}

/**
 * GLASSY — frosted translucent track with a vibrant pink → magenta → cyan
 * gradient fill and halo blur. Clipping turns the halo red.
 */
function GlassyVariant({ level, clipping, thresholdPct }: VariantProps) {
  const widthPct = Math.round(level * 100);
  const fillGradient = clipping
    ? "linear-gradient(90deg, #ff5577, #ff6b8a, #ff9aa2)"
    : "linear-gradient(90deg, var(--accent), #c84db8, var(--accent-2, #39d4ff))";
  const haloColour = clipping
    ? "rgba(255,85,119,0.65)"
    : "rgba(255,77,138,0.50)";
  return (
    <div
      className="relative h-5 w-full overflow-visible rounded-full"
      style={{
        background:
          "linear-gradient(180deg, rgba(255,255,255,0.10), rgba(255,255,255,0.04))",
        backdropFilter: "blur(8px)",
        WebkitBackdropFilter: "blur(8px)",
        border: "1px solid var(--line)",
        boxShadow: "inset 0 1px 0 rgba(255,255,255,0.35)",
      }}
    >
      <span
        aria-hidden="true"
        className="absolute inset-y-0 left-0 rounded-full"
        style={{
          width: `${widthPct}%`,
          background: fillGradient,
          boxShadow: `0 0 14px 2px ${haloColour}`,
          transition:
            "width 90ms ease-out, background 150ms ease, box-shadow 200ms ease",
        }}
      />
      {thresholdPct !== null && (
        <span
          aria-hidden="true"
          className="absolute inset-y-0 w-0.5"
          style={{
            left: `${thresholdPct}%`,
            background: "var(--accent-2, #39d4ff)",
            boxShadow: "0 0 4px var(--accent-2, #39d4ff)",
            opacity: 0.85,
          }}
        />
      )}
    </div>
  );
}
