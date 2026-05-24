import type { Chord } from "@/lib/chords";

// Diagram geometry (SVG user units).
const STRINGS = 6;
const FRETS = 5;
const MARGIN_X = 14;
const MARGIN_TOP = 22; // room above the nut for open/muted markers
const STRING_GAP = 16;
const FRET_GAP = 20;

const VIEW_W = MARGIN_X * 2 + STRING_GAP * (STRINGS - 1);
const NUT_Y = MARGIN_TOP;
const BOTTOM_Y = NUT_Y + FRET_GAP * FRETS;
const VIEW_H = BOTTOM_Y + 10;

const COLOR_LINE = "#525252";
const COLOR_NUT = "#a3a3a3";
const COLOR_DOT = "#f59e0b";
const COLOR_DOT_TEXT = "#0a0a0a";
const COLOR_MARKER = "#a3a3a3";

function stringX(index: number, mirror: boolean): number {
  const ordered = mirror ? STRINGS - 1 - index : index;
  return MARGIN_X + ordered * STRING_GAP;
}

function fretCenterY(relativeFret: number): number {
  return NUT_Y + (relativeFret - 0.5) * FRET_GAP;
}

/**
 * SVG chord diagram. Strings run vertically; low E is on the left, or on the
 * right when `mirror` is set (left-handed view).
 */
export function ChordDiagram({
  chord,
  mirror = false,
}: {
  chord: Chord;
  mirror?: boolean;
}) {
  const { frets, fingers, barre, baseFret } = chord;
  const showNut = baseFret === 1;

  return (
    <svg
      viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}
      className="h-auto w-full"
      aria-label={`Akord ${chord.name}`}
    >
      {/* Fret lines (the top line is the nut when the diagram starts at fret 1). */}
      {Array.from({ length: FRETS + 1 }, (_, line) => {
        const isNut = line === 0 && showNut;
        return (
          <line
            key={`fret-${line}`}
            x1={MARGIN_X}
            y1={NUT_Y + line * FRET_GAP}
            x2={MARGIN_X + STRING_GAP * (STRINGS - 1)}
            y2={NUT_Y + line * FRET_GAP}
            stroke={isNut ? COLOR_NUT : COLOR_LINE}
            strokeWidth={isNut ? 4 : 1.5}
          />
        );
      })}

      {/* String lines. */}
      {Array.from({ length: STRINGS }, (_, index) => {
        const x = MARGIN_X + index * STRING_GAP;
        return (
          <line
            key={`string-${index}`}
            x1={x}
            y1={NUT_Y}
            x2={x}
            y2={BOTTOM_Y}
            stroke={COLOR_LINE}
            strokeWidth={1.5}
          />
        );
      })}

      {/* Base fret label when the diagram does not start at the nut. */}
      {!showNut && (
        <text
          x={MARGIN_X - 6}
          y={fretCenterY(1)}
          fill={COLOR_MARKER}
          fontSize="11"
          textAnchor="end"
          dominantBaseline="central"
        >
          {baseFret}
        </text>
      )}

      {/* Barre. */}
      {barre &&
        (() => {
          const relative = barre.fret - baseFret + 1;
          if (relative < 1 || relative > FRETS) return null;
          const xa = stringX(barre.fromString, mirror);
          const xb = stringX(barre.toString, mirror);
          const left = Math.min(xa, xb);
          const right = Math.max(xa, xb);
          const y = fretCenterY(relative);
          return (
            <rect
              x={left - 6.5}
              y={y - 6.5}
              width={right - left + 13}
              height={13}
              rx={6.5}
              fill={COLOR_DOT}
            />
          );
        })()}

      {/* Open / muted markers and finger dots. */}
      {frets.map((fret, index) => {
        const x = stringX(index, mirror);

        if (fret === "x") {
          // Drawn as two crossed lines so it renders in any environment.
          return (
            <g key={`mark-${index}`} stroke={COLOR_MARKER} strokeWidth={1.5}>
              <line x1={x - 3.5} y1={8} x2={x + 3.5} y2={15} />
              <line x1={x - 3.5} y1={15} x2={x + 3.5} y2={8} />
            </g>
          );
        }

        if (fret === 0) {
          return (
            <circle
              key={`mark-${index}`}
              cx={x}
              cy={11}
              r={4}
              fill="none"
              stroke={COLOR_MARKER}
              strokeWidth={1.5}
            />
          );
        }

        const relative = fret - baseFret + 1;
        if (relative < 1 || relative > FRETS) return null;

        // A note that sits on the barre fret is already covered by the capsule.
        const coveredByBarre =
          barre !== null &&
          fret === barre.fret &&
          index >= barre.fromString &&
          index <= barre.toString;
        if (coveredByBarre) return null;

        const y = fretCenterY(relative);
        const finger = fingers[index];
        return (
          <g key={`dot-${index}`}>
            <circle cx={x} cy={y} r={6.5} fill={COLOR_DOT} />
            {finger && (
              <text
                x={x}
                y={y}
                fill={COLOR_DOT_TEXT}
                fontSize="9"
                fontWeight="700"
                textAnchor="middle"
                dominantBaseline="central"
              >
                {finger}
              </text>
            )}
          </g>
        );
      })}
    </svg>
  );
}
