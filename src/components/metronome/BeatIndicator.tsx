"use client";

import { useActiveTheme } from "@/lib/useActiveTheme";
import type {
  AccentLevel,
  ClickSoundId,
  PerBeatSound,
} from "@/lib/metronomeConfig";
import { CLICK_SOUNDS } from "@/lib/metronomeConfig";

const LEVEL_LABEL: Record<AccentLevel, string> = {
  accent: "přízvuk",
  normal: "normální",
  muted: "ztišená",
};

/**
 * Row of beats, one per beat in the bar. Each beat is a button: tapping it
 * cycles its accent level (normal -> accent -> muted). The sounding beat
 * lights up.
 *
 * Polymorphic per theme (same pattern as CentsBar / InputLevelMeter):
 *  - studio  : LED inside a brass ring, green for accent
 *  - vintage : brass "speed knob" coin with an engraved beat number
 *  - glassy  : neon pink orb with a halo blur
 *  - default : original surface-tinted disc — also the pre-hydration fallback
 *
 * When `showSoundDropdowns` is true, a small select appears under each beat
 * (drum mode). "Stejný" = null entry = beat falls back to `globalSound`.
 */
export function BeatIndicator({
  accentPattern,
  currentBeat,
  onCycle,
  soundPattern,
  globalSound,
  showSoundDropdowns = false,
  onBeatSoundChange,
}: {
  accentPattern: AccentLevel[];
  currentBeat: number;
  onCycle: (beat: number) => void;
  /** Per-beat sound override; same length as accentPattern. */
  soundPattern?: PerBeatSound;
  /** Global click sound — shown as "Stejný (<label>)" in the dropdown. */
  globalSound?: ClickSoundId;
  /** Whether to render the per-beat sound dropdowns. */
  showSoundDropdowns?: boolean;
  /** Called when the user picks a sound for a single beat. */
  onBeatSoundChange?: (beat: number, sound: ClickSoundId | null) => void;
}) {
  const theme = useActiveTheme();
  const globalLabel = globalSound
    ? CLICK_SOUNDS.find((option) => option.id === globalSound)?.label ?? "celkový"
    : "celkový";
  return (
    <div>
      <div className="flex flex-wrap items-start justify-center gap-3">
        {accentPattern.map((level, index) => {
          const active = index === currentBeat;
          const ariaLabel = `Doba ${index + 1}: ${LEVEL_LABEL[level]}`;
          const props = {
            level,
            active,
            beatNumber: index + 1,
            ariaLabel,
            onClick: () => onCycle(index),
          };
          let dot;
          if (theme === "studio") dot = <StudioBeat {...props} />;
          else if (theme === "vintage") dot = <VintageBeat {...props} />;
          else if (theme === "glassy") dot = <GlassyBeat {...props} />;
          else dot = <DefaultBeat {...props} />;

          return (
            <div key={index} className="flex flex-col items-center gap-1">
              {dot}
              {showSoundDropdowns && onBeatSoundChange ? (
                <select
                  aria-label={`Zvuk doby ${index + 1}`}
                  value={soundPattern?.[index] ?? ""}
                  onChange={(event) => {
                    const value = event.target.value;
                    onBeatSoundChange(
                      index,
                      value === "" ? null : (value as ClickSoundId),
                    );
                  }}
                  className="mt-1 rounded border border-line bg-surface px-1 py-0.5 text-[10px] text-text"
                >
                  <option value="">Stejný ({globalLabel})</option>
                  {CLICK_SOUNDS.map((option) => (
                    <option key={option.id} value={option.id}>
                      {option.label}
                    </option>
                  ))}
                </select>
              ) : null}
            </div>
          );
        })}
      </div>
      <p className="mt-3 text-center text-xs text-muted">
        Klikni na dobu: normální → přízvuk → ztišená
      </p>
    </div>
  );
}

type BeatProps = {
  level: AccentLevel;
  active: boolean;
  beatNumber: number;
  ariaLabel: string;
  onClick: () => void;
};

/** Original button — also the pre-hydration fallback. */
function DefaultBeat({ level, active, ariaLabel, onClick }: BeatProps) {
  const base =
    level === "accent"
      ? "bg-accent"
      : level === "muted"
        ? "border-2 border-dashed border-line"
        : "bg-surface-3";
  const activeRing = active ? " scale-110 ring-2 ring-accent" : "";
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={ariaLabel}
      className={`h-10 w-10 rounded-full transition-all duration-75 ${base}${activeRing}`}
    />
  );
}

/**
 * STUDIO — LED in a brass ring. Accent = bright green LED with halo, normal
 * = dim amber dot, muted = dead grey with an X-style overlay. The currently
 * sounding beat pulses.
 */
function StudioBeat({
  level,
  active,
  ariaLabel,
  onClick,
}: BeatProps) {
  const ledColour =
    level === "accent" ? "#7eb567" : level === "muted" ? "#2a221a" : "#c0a070";
  const glow =
    level === "accent"
      ? "0 0 10px rgba(126,181,103,0.85), 0 0 4px rgba(126,181,103,0.9)"
      : level === "muted"
        ? "inset 0 0 4px rgba(0,0,0,0.6)"
        : "0 0 4px rgba(192,160,112,0.5)";
  const activeStyle = active
    ? {
        animation: "rocker-pulse 0.45s ease-in-out infinite alternate",
        transform: "scale(1.08)",
      }
    : {};
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={ariaLabel}
      className="relative h-10 w-10 rounded-full transition-transform duration-75"
      style={{
        background: "radial-gradient(circle at 50% 50%, #1a1410 0%, #050302 100%)",
        border: "2px solid",
        borderImage: "linear-gradient(180deg, #f0d68e, #b88a2a 60%, #6e5023) 1",
        boxShadow:
          "inset 0 0 4px rgba(0,0,0,0.7), 0 1px 3px rgba(0,0,0,0.5)",
        ...activeStyle,
      }}
    >
      {/* The LED dot. */}
      <span
        aria-hidden="true"
        className="absolute top-1/2 left-1/2 h-3.5 w-3.5 -translate-x-1/2 -translate-y-1/2 rounded-full"
        style={{
          background: ledColour,
          boxShadow: glow,
          opacity: level === "muted" ? 0.7 : 1,
        }}
      />
    </button>
  );
}

/**
 * VINTAGE — brass speed-knob coin with an engraved beat number. Accent =
 * polished, highlight on top, glowing rim when active. Normal = patinated.
 * Muted = darkened with the number faded out.
 */
function VintageBeat({
  level,
  active,
  beatNumber,
  ariaLabel,
  onClick,
}: BeatProps) {
  // Three palettes for the brass face. Accent is the brightest, polished
  // version; normal sits at "warm aged"; muted goes to dark patina.
  const face =
    level === "accent"
      ? "radial-gradient(circle at 35% 30%, #f5e0a6 0%, #d4a83e 40%, #8a6f3a 80%, #4a2818 100%)"
      : level === "muted"
        ? "radial-gradient(circle at 35% 30%, #4a3826 0%, #2a1d10 60%, #110b05 100%)"
        : "radial-gradient(circle at 35% 30%, #c0a070 0%, #8e6f3e 50%, #4a3220 100%)";
  const rim =
    level === "muted" ? "#2a1d10" : "#4a2818";
  const numberColour =
    level === "accent"
      ? "#1a0e07"
      : level === "muted"
        ? "rgba(110,80,40,0.45)"
        : "#2a1d10";
  const haloShadow = active
    ? "0 0 0 1px rgba(240,214,142,0.7), 0 0 14px rgba(212,168,62,0.7)"
    : "0 0 0 1px rgba(0,0,0,0.4)";
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={ariaLabel}
      className="relative h-10 w-10 rounded-full font-display transition-all duration-150"
      style={{
        background: face,
        color: numberColour,
        border: `1px solid ${rim}`,
        boxShadow: `
          inset 0 1px 1px rgba(255,240,180,0.4),
          inset 0 -1px 2px rgba(0,0,0,0.4),
          ${haloShadow}
        `,
        transform: active ? "scale(1.08)" : "scale(1)",
        textShadow:
          level === "accent"
            ? "0 1px 0 rgba(255,240,180,0.5)"
            : "0 -1px 0 rgba(0,0,0,0.5)",
        fontWeight: 700,
        fontSize: "14px",
        lineHeight: 1,
      }}
    >
      {beatNumber}
    </button>
  );
}

/**
 * GLASSY — neon orb with halo blur. Accent = full pink fill, normal = mid
 * opacity, muted = outline only. Active beat pulses.
 */
function GlassyBeat({
  level,
  active,
  ariaLabel,
  onClick,
}: BeatProps) {
  const fill =
    level === "accent"
      ? "radial-gradient(circle at 35% 30%, #ff85b0 0%, var(--accent) 50%, #c8265f 100%)"
      : level === "muted"
        ? "transparent"
        : "radial-gradient(circle at 35% 30%, rgba(255,133,176,0.6) 0%, rgba(255,77,138,0.3) 60%, transparent 100%)";
  const haloColour =
    level === "accent"
      ? "rgba(255,77,138,0.65)"
      : level === "muted"
        ? "transparent"
        : "rgba(255,77,138,0.25)";
  const border =
    level === "muted"
      ? "1px dashed rgba(255,255,255,0.45)"
      : "1px solid rgba(255,255,255,0.35)";
  const activeStyle = active
    ? {
        animation: "rocker-pulse 0.45s ease-in-out infinite alternate",
        transform: "scale(1.10)",
      }
    : {};
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={ariaLabel}
      className="relative h-10 w-10 rounded-full transition-transform duration-75"
      style={{
        background: fill,
        border,
        boxShadow: `0 0 14px 2px ${haloColour}, inset 0 1px 0 rgba(255,255,255,0.35)`,
        backdropFilter: level === "muted" ? "blur(6px)" : undefined,
        WebkitBackdropFilter: level === "muted" ? "blur(6px)" : undefined,
        ...activeStyle,
      }}
    />
  );
}
