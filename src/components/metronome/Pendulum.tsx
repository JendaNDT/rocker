"use client";

import { useEffect, useRef } from "react";
import { useActiveTheme } from "@/lib/useActiveTheme";
import { useMetronomeStore } from "@/stores/metronomeStore";

/** Maximum swing angle each side of vertical. */
const MAX_ANGLE = 45;

/**
 * Pendulum — Wittner-style swinging arm replacing the dot row when the
 * user picks Visual mode "pendulum". One full sweep (left → right → left)
 * spans two beats, matching real mechanical metronomes.
 *
 * Polymorphic per theme:
 *  - studio  : chromed arm in a brass bezel
 *  - vintage : warm-wood casing with a brass arm
 *  - glassy  : neon stroke with a soft halo
 *  - default : minimalist grey arm — also the pre-hydration fallback
 *
 * Animation runs in a single rAF loop driven by refs so React doesn't
 * re-render every frame.
 */
export function Pendulum() {
  const theme = useActiveTheme();
  const isPlaying = useMetronomeStore((state) => state.isPlaying);
  const bpm = useMetronomeStore((state) => state.bpm);
  const currentBeat = useMetronomeStore((state) => state.currentBeat);
  const accentPattern = useMetronomeStore((state) => state.accentPattern);

  const armRef = useRef<HTMLDivElement>(null);
  const boxRef = useRef<HTMLDivElement>(null);
  // Live refs let the rAF loop read the latest values without re-creating.
  const bpmRef = useRef(bpm);
  const beatRef = useRef(currentBeat);
  const beatStartRef = useRef<number>(
    typeof performance !== "undefined" ? performance.now() : 0,
  );
  const lastFlashBeatRef = useRef(-1);

  // Sync refs whenever React renders with new values.
  bpmRef.current = bpm;
  useEffect(() => {
    beatRef.current = currentBeat;
    if (currentBeat >= 0) {
      // A new beat just started — restart the per-beat clock.
      beatStartRef.current = performance.now();
    }
  }, [currentBeat]);

  // Flash the whole box briefly whenever an accented beat starts. The CSS
  // class `pendulum-accent-flash` lives in globals.css and runs a 220 ms
  // brightness flash via @keyframes.
  useEffect(() => {
    if (currentBeat < 0) return;
    if (currentBeat === lastFlashBeatRef.current) return;
    lastFlashBeatRef.current = currentBeat;
    if (accentPattern[currentBeat] !== "accent") return;
    const box = boxRef.current;
    if (!box) return;
    box.classList.remove("pendulum-accent-flash");
    // Force reflow so removing+re-adding the class restarts the animation
    // even when two accent beats land back-to-back.
    void box.offsetWidth;
    box.classList.add("pendulum-accent-flash");
    const timeout = setTimeout(
      () => box.classList.remove("pendulum-accent-flash"),
      260,
    );
    return () => clearTimeout(timeout);
  }, [currentBeat, accentPattern]);

  // Single rAF loop while the metronome is playing.
  useEffect(() => {
    if (!isPlaying) {
      if (armRef.current) armRef.current.style.transform = "rotate(0deg)";
      return;
    }
    let raf = 0;
    const tick = () => {
      if (armRef.current) {
        const beat = beatRef.current;
        if (beat < 0) {
          // Between stop and the first beat: park in the middle.
          armRef.current.style.transform = "rotate(0deg)";
        } else {
          const beatDurationMs = (60 / bpmRef.current) * 1000;
          const elapsed = performance.now() - beatStartRef.current;
          const phase = Math.max(0, Math.min(elapsed / beatDurationMs, 1));
          // Even beats swing left-to-right (-MAX → +MAX); odd ones swing
          // back right-to-left. A sine eased shape gives the physical
          // "slow at edges, fast through middle" Wittner feel.
          const direction = beat % 2 === 0 ? 1 : -1;
          const angle =
            direction * MAX_ANGLE * Math.sin((phase - 0.5) * Math.PI);
          armRef.current.style.transform = `rotate(${angle}deg)`;
        }
      }
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [isPlaying]);

  if (theme === "studio")
    return <StudioPendulum armRef={armRef} boxRef={boxRef} />;
  if (theme === "vintage")
    return <VintagePendulum armRef={armRef} boxRef={boxRef} />;
  if (theme === "glassy")
    return <GlassyPendulum armRef={armRef} boxRef={boxRef} />;
  return <DefaultPendulum armRef={armRef} boxRef={boxRef} />;
}

type PendulumProps = {
  armRef: React.RefObject<HTMLDivElement | null>;
  boxRef: React.RefObject<HTMLDivElement | null>;
};

/** Container that hosts the arm — common to every theme. */
function PendulumBox({
  children,
  boxRef,
  background,
  border,
  bevel,
}: {
  children: React.ReactNode;
  boxRef?: React.RefObject<HTMLDivElement | null>;
  background?: string;
  border?: string;
  bevel?: string;
}) {
  return (
    <div
      ref={boxRef}
      className="pendulum-box relative mx-auto"
      style={{
        width: 240,
        height: 220,
        background,
        border,
        borderRadius: 14,
        boxShadow: bevel,
      }}
    >
      {/* The pivot sits at the bottom centre. */}
      <div
        className="absolute"
        style={{ left: "50%", bottom: 22, transform: "translateX(-50%)" }}
      >
        {children}
      </div>
    </div>
  );
}

/** Original — also the pre-hydration fallback. */
function DefaultPendulum({ armRef, boxRef }: PendulumProps) {
  return (
    <PendulumBox
      boxRef={boxRef}
      background="var(--surface-2, #2a2a2a)"
      border="1px solid var(--line, #444)"
    >
      <div
        ref={armRef}
        aria-hidden="true"
        style={{
          width: 6,
          height: 170,
          background: "var(--text, #ddd)",
          borderRadius: 3,
          transformOrigin: "50% 100%",
          transform: "rotate(0deg)",
          willChange: "transform",
        }}
      />
      {/* Pivot dot. */}
      <div
        aria-hidden="true"
        className="absolute"
        style={{
          left: "50%",
          bottom: -6,
          width: 14,
          height: 14,
          borderRadius: "50%",
          background: "var(--accent, #ffaa66)",
          transform: "translateX(-50%)",
        }}
      />
    </PendulumBox>
  );
}

/** STUDIO — chromed arm with a brass tip in a dark brass bezel. */
function StudioPendulum({ armRef, boxRef }: PendulumProps) {
  return (
    <PendulumBox
      boxRef={boxRef}
      background="radial-gradient(circle at 50% 30%, #1a1410 0%, #050302 100%)"
      border="2px solid"
      bevel="inset 0 0 8px rgba(0,0,0,0.7), 0 2px 4px rgba(0,0,0,0.5)"
    >
      <div
        ref={armRef}
        aria-hidden="true"
        style={{
          width: 8,
          height: 175,
          background:
            "linear-gradient(180deg, #f5e0a6 0%, #c0a070 40%, #6e5023 100%)",
          borderRadius: 4,
          boxShadow:
            "inset 1px 0 0 rgba(255,240,180,0.5), 0 0 8px rgba(212,168,62,0.4)",
          transformOrigin: "50% 100%",
          transform: "rotate(0deg)",
          willChange: "transform",
        }}
      >
        {/* Brass weight near the top of the arm. */}
        <div
          aria-hidden="true"
          style={{
            position: "absolute",
            top: 20,
            left: "50%",
            transform: "translateX(-50%)",
            width: 26,
            height: 14,
            borderRadius: 3,
            background:
              "radial-gradient(circle at 35% 30%, #f5e0a6 0%, #d4a83e 50%, #6e5023 100%)",
            boxShadow: "inset 0 1px 1px rgba(255,240,180,0.5)",
          }}
        />
      </div>
      {/* Brass pivot. */}
      <div
        aria-hidden="true"
        className="absolute"
        style={{
          left: "50%",
          bottom: -8,
          width: 20,
          height: 20,
          borderRadius: "50%",
          background:
            "radial-gradient(circle at 35% 30%, #f5e0a6 0%, #d4a83e 50%, #6e5023 100%)",
          boxShadow: "inset 0 1px 1px rgba(255,240,180,0.4)",
          transform: "translateX(-50%)",
        }}
      />
    </PendulumBox>
  );
}

/** VINTAGE — Solton-leaning warm mahogany casing with a brass arm. */
function VintagePendulum({ armRef, boxRef }: PendulumProps) {
  return (
    <PendulumBox
      boxRef={boxRef}
      background="linear-gradient(180deg, #3a1e10 0%, #2a1408 100%)"
      border="1px solid #4a2818"
      bevel="inset 0 1px 2px rgba(255,240,180,0.2), inset 0 -1px 3px rgba(0,0,0,0.5), 0 2px 4px rgba(0,0,0,0.5)"
    >
      <div
        ref={armRef}
        aria-hidden="true"
        style={{
          width: 7,
          height: 175,
          background:
            "linear-gradient(180deg, #f5e0a6 0%, #d4a83e 50%, #8a6f3a 100%)",
          borderRadius: 3,
          boxShadow:
            "inset 0 1px 0 rgba(255,240,180,0.4), 0 0 6px rgba(212,168,62,0.5)",
          transformOrigin: "50% 100%",
          transform: "rotate(0deg)",
          willChange: "transform",
        }}
      >
        {/* Brass bob — a hand-engraved coin. */}
        <div
          aria-hidden="true"
          style={{
            position: "absolute",
            top: 22,
            left: "50%",
            transform: "translateX(-50%)",
            width: 32,
            height: 20,
            borderRadius: 4,
            background:
              "radial-gradient(circle at 35% 30%, #f5e0a6 0%, #d4a83e 40%, #8a6f3a 80%, #4a2818 100%)",
            boxShadow:
              "inset 0 1px 1px rgba(255,240,180,0.5), inset 0 -1px 1px rgba(0,0,0,0.5)",
          }}
        />
      </div>
      <div
        aria-hidden="true"
        className="absolute"
        style={{
          left: "50%",
          bottom: -8,
          width: 22,
          height: 22,
          borderRadius: "50%",
          background:
            "radial-gradient(circle at 35% 30%, #f5e0a6 0%, #d4a83e 50%, #8a6f3a 100%)",
          boxShadow:
            "inset 0 1px 1px rgba(255,240,180,0.4), 0 0 0 1px #4a2818",
          transform: "translateX(-50%)",
        }}
      />
    </PendulumBox>
  );
}

/** GLASSY — frosted neon stroke with a halo. */
function GlassyPendulum({ armRef, boxRef }: PendulumProps) {
  return (
    <PendulumBox
      boxRef={boxRef}
      background="linear-gradient(180deg, rgba(255,255,255,0.06) 0%, rgba(255,255,255,0.02) 100%)"
      border="1px solid rgba(255,255,255,0.18)"
      bevel="inset 0 1px 0 rgba(255,255,255,0.25), 0 0 22px rgba(255,77,138,0.18)"
    >
      <div
        ref={armRef}
        aria-hidden="true"
        style={{
          width: 6,
          height: 175,
          background:
            "linear-gradient(180deg, #ff85b0 0%, var(--accent, #ff4d8a) 100%)",
          borderRadius: 3,
          boxShadow:
            "0 0 10px rgba(255,77,138,0.7), 0 0 18px 3px rgba(255,77,138,0.35)",
          transformOrigin: "50% 100%",
          transform: "rotate(0deg)",
          willChange: "transform",
        }}
      >
        {/* Neon orb instead of a brass weight. */}
        <div
          aria-hidden="true"
          style={{
            position: "absolute",
            top: 16,
            left: "50%",
            transform: "translateX(-50%)",
            width: 24,
            height: 24,
            borderRadius: "50%",
            background:
              "radial-gradient(circle at 35% 30%, #ff85b0 0%, var(--accent, #ff4d8a) 60%, #c8265f 100%)",
            boxShadow: "0 0 16px 3px rgba(255,77,138,0.65)",
          }}
        />
      </div>
      <div
        aria-hidden="true"
        className="absolute"
        style={{
          left: "50%",
          bottom: -8,
          width: 20,
          height: 20,
          borderRadius: "50%",
          background:
            "radial-gradient(circle at 35% 30%, #ff85b0 0%, var(--accent, #ff4d8a) 60%, #c8265f 100%)",
          boxShadow: "0 0 14px rgba(255,77,138,0.7)",
          transform: "translateX(-50%)",
        }}
      />
    </PendulumBox>
  );
}
