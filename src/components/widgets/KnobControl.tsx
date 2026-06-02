"use client";

import {
  type CSSProperties,
  type KeyboardEvent,
  type PointerEvent,
  useCallback,
  useRef,
  useState,
} from "react";
import { useActiveTheme } from "@/hooks/useActiveTheme";
import type { Theme } from "@/stores/settingsStore";

/**
 * Rotary knob with vertical pointer drag, keyboard support and per-theme
 * visuals. Used in place of native range inputs for the Aparát parameters
 * and the metronome volumes.
 *
 * Interaction:
 *  - vertical drag (pointerdown then move up/down). The full drag range
 *    (top to bottom of the screen / 200 px, whichever is shorter) covers
 *    min..max — so users on mobile can sweep the value precisely.
 *  - arrow keys: ±step. Shift + arrow: ±fineStep. Home / End jump to
 *    min / max. PageUp / PageDown move by ±10*step.
 *  - mouse wheel over the knob nudges it in step increments.
 *
 * Visual rotation: maps value to angle in [-135°, +135°] (270° sweep,
 * standard for analogue gear). Bipolar knobs centre the indicator at 0°.
 *
 * Variants chosen by useActiveTheme():
 *  - studio  : chromed body, brass bezel, brass indicator stripe
 *  - vintage : Solton-style brass speed knob with engraved indicator
 *  - glassy  : translucent frosted body with a neon ring and indicator
 *  - default : neutral grey disc with a notch — pre-hydration fallback
 */

const ANGLE_RANGE = 270;
const ANGLE_OFFSET = -135;
// Pixels of vertical drag that span the full min..max range.
const DRAG_RANGE_PX = 200;

type KnobControlProps = {
  label: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  fineStep?: number;
  bipolar?: boolean;
  formatValue?: (value: number) => string;
  onChange: (next: number) => void;
  ariaLabel?: string;
};

export function KnobControl({
  label,
  value,
  min,
  max,
  step = 1,
  fineStep,
  bipolar = false,
  formatValue,
  onChange,
  ariaLabel,
}: KnobControlProps) {
  const theme = useActiveTheme();
  const knobRef = useRef<HTMLButtonElement>(null);
  // Anchor point captured on pointer-down — drag deltas are calculated
  // against this so the knob doesn't "snap" if the user grabs it mid-sweep.
  const dragAnchor = useRef<{ startY: number; startValue: number } | null>(
    null,
  );
  const [dragging, setDragging] = useState(false);

  const span = max - min;
  // Map value to angle. Bipolar treats 0 as the centre; unipolar treats
  // min as the bottom-left and max as the top-right.
  const normalised = bipolar
    ? (value - (min + max) / 2) / (span / 2)
    : (value - min) / span;
  const clampedNormalised = Math.max(bipolar ? -1 : 0, Math.min(1, normalised));
  const angle = bipolar
    ? clampedNormalised * (ANGLE_RANGE / 2)
    : ANGLE_OFFSET + clampedNormalised * ANGLE_RANGE;

  const clamp = useCallback(
    (next: number) => Math.max(min, Math.min(max, next)),
    [min, max],
  );

  // Snap to step grid so the knob doesn't drift to weird fractional values.
  const snap = useCallback(
    (next: number) => {
      if (step <= 0) return next;
      // Round to step from min, keeping min as the anchor.
      const stepped = Math.round((next - min) / step) * step + min;
      return clamp(stepped);
    },
    [step, min, clamp],
  );

  const handlePointerDown = (event: PointerEvent<HTMLButtonElement>) => {
    if (event.button !== 0) return;
    event.preventDefault();
    knobRef.current?.focus();
    dragAnchor.current = { startY: event.clientY, startValue: value };
    setDragging(true);
    knobRef.current?.setPointerCapture(event.pointerId);
  };

  const handlePointerMove = (event: PointerEvent<HTMLButtonElement>) => {
    if (!dragAnchor.current) return;
    const dy = dragAnchor.current.startY - event.clientY;
    const ratio = dy / DRAG_RANGE_PX;
    const next = snap(dragAnchor.current.startValue + ratio * span);
    if (next !== value) onChange(next);
  };

  const releaseDrag = (event: PointerEvent<HTMLButtonElement>) => {
    if (!dragAnchor.current) return;
    dragAnchor.current = null;
    setDragging(false);
    knobRef.current?.releasePointerCapture(event.pointerId);
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    const baseStep = event.shiftKey ? (fineStep ?? step / 10) : step;
    let next: number | null = null;
    switch (event.key) {
      case "ArrowUp":
      case "ArrowRight":
        next = value + baseStep;
        break;
      case "ArrowDown":
      case "ArrowLeft":
        next = value - baseStep;
        break;
      case "PageUp":
        next = value + step * 10;
        break;
      case "PageDown":
        next = value - step * 10;
        break;
      case "Home":
        next = min;
        break;
      case "End":
        next = max;
        break;
    }
    if (next !== null) {
      event.preventDefault();
      const snapped = snap(next);
      if (snapped !== value) onChange(snapped);
    }
  };

  const handleWheel: React.WheelEventHandler<HTMLButtonElement> = (event) => {
    // Don't hijack page scroll unless the knob is focused — wheel only acts
    // when the user has explicitly engaged the knob.
    if (document.activeElement !== knobRef.current) return;
    event.preventDefault();
    const direction = event.deltaY > 0 ? -1 : 1;
    const next = snap(value + direction * step);
    if (next !== value) onChange(next);
  };

  const displayValue = formatValue
    ? formatValue(value)
    : value.toFixed(step < 1 ? 2 : 0);

  return (
    <div className="flex w-20 flex-col items-center gap-1">
      <span className="text-xs font-medium text-dim text-center">{label}</span>
      <button
        ref={knobRef}
        type="button"
        role="slider"
        aria-valuemin={min}
        aria-valuemax={max}
        aria-valuenow={value}
        aria-label={ariaLabel ?? label}
        tabIndex={0}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={releaseDrag}
        onPointerCancel={releaseDrag}
        onKeyDown={handleKeyDown}
        onWheel={handleWheel}
        className="relative h-14 w-14 cursor-grab touch-none rounded-full focus:outline-none focus-visible:ring-2 focus-visible:ring-accent active:cursor-grabbing"
        style={{ userSelect: "none" }}
      >
        <KnobVisual theme={theme} angle={angle} dragging={dragging} />
      </button>
      <span className="font-mono text-xs tabular-nums text-text">
        {displayValue}
      </span>
    </div>
  );
}

function KnobVisual({
  theme,
  angle,
  dragging,
}: {
  theme: Theme | null;
  angle: number;
  dragging: boolean;
}) {
  if (theme === "studio") return <StudioKnob angle={angle} dragging={dragging} />;
  if (theme === "vintage") return <VintageKnob angle={angle} dragging={dragging} />;
  if (theme === "glassy") return <GlassyKnob angle={angle} dragging={dragging} />;
  return <DefaultKnob angle={angle} />;
}

type VisualProps = { angle: number; dragging: boolean };

/** Neutral fallback — also what renders before hydration. */
function DefaultKnob({ angle }: { angle: number }) {
  return (
    <span
      aria-hidden="true"
      className="absolute inset-0 rounded-full border border-line bg-surface-3"
    >
      <span
        className="absolute top-1 left-1/2 h-3 w-0.5 -translate-x-1/2 origin-[50%_1.5rem] rounded-full bg-text"
        style={{ transform: `translateX(-50%) rotate(${angle}deg)` }}
      />
    </span>
  );
}

/**
 * STUDIO — chromed steel knob with a brass bezel and a brass indicator
 * stripe across the top. Subtle ring of brass when dragging.
 */
function StudioKnob({ angle, dragging }: VisualProps) {
  const indicator: CSSProperties = {
    transform: `translateX(-50%) rotate(${angle}deg)`,
  };
  return (
    <span aria-hidden="true" className="absolute inset-0">
      {/* Brass bezel. */}
      <span
        className="absolute inset-0 rounded-full"
        style={{
          background:
            "linear-gradient(180deg, #f0d68e 0%, #d4a83e 35%, #b88a2a 70%, #6e5023 100%)",
          boxShadow: dragging
            ? "0 0 0 2px rgba(212,168,62,0.4), inset 0 0 4px rgba(0,0,0,0.4)"
            : "0 1px 2px rgba(0,0,0,0.45), inset 0 0 3px rgba(0,0,0,0.4)",
        }}
      />
      {/* Chromed inner body. */}
      <span
        className="absolute inset-[5px] rounded-full"
        style={{
          background:
            "radial-gradient(circle at 35% 30%, #d8d8d8 0%, #6f6f6f 55%, #2a2a2a 100%)",
          boxShadow:
            "inset 0 1px 2px rgba(255,255,255,0.3), inset 0 -2px 4px rgba(0,0,0,0.6)",
        }}
      />
      {/* Indicator stripe — runs from centre to top of the knob and rotates. */}
      <span
        className="absolute top-2 left-1/2 h-5 w-1 origin-[50%_1.5rem] rounded-sm"
        style={{
          ...indicator,
          background:
            "linear-gradient(180deg, #f0d68e 0%, #d4a83e 50%, #8a6f3a 100%)",
          boxShadow: "0 0 2px rgba(0,0,0,0.6)",
        }}
      />
    </span>
  );
}

/**
 * VINTAGE — Solton speed-knob: polished brass face with the engraved
 * indicator notch on top. A thin darker rim catches the light.
 */
function VintageKnob({ angle, dragging }: VisualProps) {
  return (
    <span aria-hidden="true" className="absolute inset-0">
      {/* Outer dark patina ring. */}
      <span
        className="absolute inset-0 rounded-full"
        style={{
          background:
            "radial-gradient(circle at 50% 100%, #4a2818 0%, #1a0e07 100%)",
          boxShadow: dragging
            ? "0 0 12px rgba(212,168,62,0.6)"
            : "0 1px 3px rgba(0,0,0,0.6)",
        }}
      />
      {/* Polished brass face. */}
      <span
        className="absolute inset-[3px] rounded-full"
        style={{
          background:
            "radial-gradient(circle at 35% 28%, #f5e0a6 0%, #d4a83e 38%, #8a6f3a 80%, #4a2818 100%)",
          boxShadow:
            "inset 0 1px 1px rgba(255,240,180,0.6), inset 0 -2px 3px rgba(0,0,0,0.45)",
        }}
      />
      {/* Engraved indicator notch — darker so it reads as a stamp. */}
      <span
        className="absolute top-1.5 left-1/2 h-4 w-1 origin-[50%_1.75rem] rounded-sm"
        style={{
          transform: `translateX(-50%) rotate(${angle}deg)`,
          background: "#1a0e07",
          boxShadow: "0 1px 0 rgba(255,240,180,0.4)",
        }}
      />
    </span>
  );
}

/**
 * GLASSY — frosted translucent body with a neon ring and a glowing
 * indicator. The ring intensifies while dragging.
 */
function GlassyKnob({ angle, dragging }: VisualProps) {
  return (
    <span aria-hidden="true" className="absolute inset-0">
      {/* Neon ring. */}
      <span
        className="absolute inset-0 rounded-full"
        style={{
          background:
            "conic-gradient(from 180deg, #39d4ff, #c84db8, #ff4d8a, #c84db8, #39d4ff)",
          boxShadow: dragging
            ? "0 0 18px rgba(255,77,138,0.7), 0 0 8px rgba(57,212,255,0.5)"
            : "0 0 10px rgba(255,77,138,0.45)",
        }}
      />
      {/* Frosted inner body. */}
      <span
        className="absolute inset-[3px] rounded-full"
        style={{
          background:
            "radial-gradient(circle at 35% 30%, rgba(255,255,255,0.45) 0%, rgba(255,255,255,0.15) 60%, rgba(0,0,0,0.20) 100%)",
          backdropFilter: "blur(8px)",
          WebkitBackdropFilter: "blur(8px)",
          boxShadow: "inset 0 1px 0 rgba(255,255,255,0.45)",
        }}
      />
      {/* Glowing indicator dot. */}
      <span
        className="absolute top-1.5 left-1/2 h-4 w-1.5 origin-[50%_1.75rem] rounded-full"
        style={{
          transform: `translateX(-50%) rotate(${angle}deg)`,
          background: "#ffffff",
          boxShadow: "0 0 8px #39d4ff, 0 0 3px #ffffff",
        }}
      />
    </span>
  );
}
