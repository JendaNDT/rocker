import { getTuning } from "@/audio/tunings";
import { type Notation, pitchClassName } from "@/lib/pitch";
import { scaleOffset } from "@/lib/scales";
import type { Scale } from "@/lib/scales";

// Fretboard geometry (SVG user units).
const FRETS = 12;
const LEFT = 46; // x of the nut
const FRET_W = 34;
const TOP = 16;
const ROW_H = 22;
const OPEN_X = 28; // x of open-string notes (left of the nut)
const LABEL_X = 9;

const STRINGS = getTuning("standard").strings; // low E .. high E
const ROWS = STRINGS.length; // 6
const NECK_RIGHT = LEFT + FRETS * FRET_W;
const NECK_BOTTOM = TOP + (ROWS - 1) * ROW_H;
const VIEW_W = NECK_RIGHT + 14;
const VIEW_H = NECK_BOTTOM + 22;

const MARKER_FRETS = [3, 5, 7, 9];

function rowY(row: number): number {
  return TOP + row * ROW_H;
}

function noteX(fret: number): number {
  return fret === 0 ? OPEN_X : LEFT + (fret - 0.5) * FRET_W;
}

/**
 * Horizontal fretboard that lights up the notes of a scale. The root note is
 * shown filled; the other scale notes are dimmer. High E is the top row.
 */
export function Fretboard({
  scale,
  rootPitchClass,
  notation,
}: {
  scale: Scale;
  rootPitchClass: number;
  notation: Notation;
}) {
  return (
    <svg
      viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}
      className="h-auto w-full min-w-[420px]"
      aria-label={`Hmatník — ${scale.name}`}
    >
      <rect
        x={LEFT}
        y={TOP}
        width={NECK_RIGHT - LEFT}
        height={NECK_BOTTOM - TOP}
        fill="#171717"
      />

      {/* Inlay markers. */}
      {MARKER_FRETS.map((fret) => (
        <circle
          key={`inlay-${fret}`}
          cx={noteX(fret)}
          cy={(rowY(2) + rowY(3)) / 2}
          r={3}
          fill="#404040"
        />
      ))}
      <circle cx={noteX(12)} cy={rowY(1)} r={3} fill="#404040" />
      <circle cx={noteX(12)} cy={rowY(4)} r={3} fill="#404040" />

      {/* Fret wires. */}
      {Array.from({ length: FRETS }, (_, index) => {
        const fret = index + 1;
        const x = LEFT + fret * FRET_W;
        return (
          <line
            key={`fret-${fret}`}
            x1={x}
            y1={TOP}
            x2={x}
            y2={NECK_BOTTOM}
            stroke="#525252"
            strokeWidth={1.5}
          />
        );
      })}
      {/* Nut. */}
      <line
        x1={LEFT}
        y1={TOP}
        x2={LEFT}
        y2={NECK_BOTTOM}
        stroke="#a3a3a3"
        strokeWidth={4}
      />

      {/* Strings, labels and note dots — top row is the high E string. */}
      {Array.from({ length: ROWS }, (_, row) => {
        const stringIndex = ROWS - 1 - row;
        const open = STRINGS[stringIndex];
        const y = rowY(row);

        return (
          <g key={`string-${stringIndex}`}>
            <line
              x1={LEFT}
              y1={y}
              x2={NECK_RIGHT}
              y2={y}
              stroke="#6b6b6b"
              strokeWidth={1.5}
            />
            <text
              x={LABEL_X}
              y={y}
              fill="#a3a3a3"
              fontSize="9"
              dominantBaseline="central"
            >
              {pitchClassName(open.midi, notation)}
            </text>
            {Array.from({ length: FRETS + 1 }, (_, fret) => {
              const pitchClass = (open.midi + fret) % 12;
              const offset = scaleOffset(scale, rootPitchClass, pitchClass);
              if (offset === null) return null;
              const isRoot = offset === 0;
              return (
                <g key={`note-${stringIndex}-${fret}`}>
                  <circle
                    cx={noteX(fret)}
                    cy={y}
                    r={8.5}
                    fill={isRoot ? "#fbbf24" : "#b45309"}
                  />
                  <text
                    x={noteX(fret)}
                    y={y}
                    fill={isRoot ? "#0a0a0a" : "#fafafa"}
                    fontSize="7"
                    fontWeight={isRoot ? "700" : "400"}
                    textAnchor="middle"
                    dominantBaseline="central"
                  >
                    {pitchClassName(pitchClass, notation)}
                  </text>
                </g>
              );
            })}
          </g>
        );
      })}

      {/* Fret numbers. */}
      {[...MARKER_FRETS, 12].map((fret) => (
        <text
          key={`num-${fret}`}
          x={noteX(fret)}
          y={NECK_BOTTOM + 14}
          fill="#6b6b6b"
          fontSize="9"
          textAnchor="middle"
        >
          {fret}
        </text>
      ))}
    </svg>
  );
}
