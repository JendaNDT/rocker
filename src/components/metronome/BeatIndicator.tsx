import type { AccentLevel } from "@/lib/metronomeConfig";

const LEVEL_LABEL: Record<AccentLevel, string> = {
  accent: "přízvuk",
  normal: "normální",
  muted: "ztišená",
};

/**
 * Row of beats, one per beat in the bar. Each beat is a button: tapping it
 * cycles its accent level (normal -> accent -> muted). The sounding beat
 * lights up.
 */
export function BeatIndicator({
  accentPattern,
  currentBeat,
  onCycle,
}: {
  accentPattern: AccentLevel[];
  currentBeat: number;
  onCycle: (beat: number) => void;
}) {
  return (
    <div>
      <div className="flex flex-wrap justify-center gap-3">
        {accentPattern.map((level, index) => {
          const active = index === currentBeat;
          const base =
            level === "accent"
              ? "bg-accent"
              : level === "muted"
                ? "border-2 border-dashed border-line"
                : "bg-surface-3";
          const activeRing = active ? " scale-110 ring-2 ring-accent" : "";
          return (
            <button
              key={index}
              type="button"
              onClick={() => onCycle(index)}
              aria-label={`Doba ${index + 1}: ${LEVEL_LABEL[level]}`}
              className={`h-10 w-10 rounded-full transition-all duration-75 ${base}${activeRing}`}
            />
          );
        })}
      </div>
      <p className="mt-3 text-center text-xs text-muted">
        Klikni na dobu: normální → přízvuk → ztišená
      </p>
    </div>
  );
}
