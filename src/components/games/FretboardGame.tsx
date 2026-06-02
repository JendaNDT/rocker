"use client";

import { useState } from "react";
import { getTuning } from "@/audio/tunings";
import { noteAtFret, pickTarget } from "@/lib/fretboardGame";
import { pitchClassName } from "@/lib/pitch";
import { useHydrated } from "@/hooks/useHydrated";
import { useSettingsStore } from "@/stores/settingsStore";

// Standard tuning, low string first. Fretboard memory is a standard-tuning
// skill, so the game uses it regardless of the app's selected tuning.
const STRINGS = getTuning("standard").strings;
const ROWS = STRINGS.length;
const FRETS = 12;

// Fretboard geometry (SVG user units) — mirrors the library Fretboard.
const LEFT = 46;
const FRET_W = 34;
const TOP = 16;
const ROW_H = 22;
const OPEN_X = 28;
const LABEL_X = 9;
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

/** The last answered position and whether it was right. */
interface Feedback {
  stringIndex: number;
  fret: number;
  correct: boolean;
}

/**
 * Fretboard memory game (Bod 19): the app names a note, the player clicks
 * where it is on the fretboard. Standard tuning; score and streak tracked.
 */
export function FretboardGame() {
  const hydrated = useHydrated();
  const notation = useSettingsStore((state) => state.notation);
  const display = hydrated ? notation : "english";

  const [started, setStarted] = useState(false);
  const [target, setTarget] = useState(0);
  const [score, setScore] = useState(0);
  const [streak, setStreak] = useState(0);
  const [feedback, setFeedback] = useState<Feedback | null>(null);

  function start() {
    setStarted(true);
    setTarget(pickTarget(null));
    setScore(0);
    setStreak(0);
    setFeedback(null);
  }

  function answer(stringIndex: number, fret: number) {
    const correct = noteAtFret(STRINGS[stringIndex].midi, fret) === target;
    setFeedback({ stringIndex, fret, correct });
    if (correct) {
      setScore((value) => value + 1);
      setStreak((value) => value + 1);
      setTarget((previous) => pickTarget(previous));
    } else {
      setStreak(0);
    }
  }

  if (!started) {
    return (
      <div className="rounded-lg border border-line bg-surface p-4">
        <p className="text-sm text-text">
          Appka pojmenuje tón a ty klikni, kde je na hmatníku. Hraje se ve
          standardním ladění. Cvičí to orientaci po krku.
        </p>
        <button
          type="button"
          onClick={start}
          className="mt-4 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90"
        >
          Začít hru
        </button>
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-line bg-surface p-4">
      <div className="flex items-baseline justify-between gap-4">
        <div className="text-lg">
          Najdi tón:{" "}
          <span className="text-2xl font-bold text-accent">
            {pitchClassName(target, display)}
          </span>
        </div>
        <div className="text-sm text-dim">
          Skóre {score} · série {streak}
        </div>
      </div>

      {feedback && (
        <p
          className={
            feedback.correct
              ? "mt-2 text-sm text-success"
              : "mt-2 text-sm text-danger"
          }
        >
          {feedback.correct
            ? "Správně!"
            : `Tady je ${pitchClassName(
                noteAtFret(STRINGS[feedback.stringIndex].midi, feedback.fret),
                display,
              )} — zkus to znovu.`}
        </p>
      )}

      <svg
        viewBox={`0 0 ${VIEW_W} ${VIEW_H}`}
        className="mt-3 h-auto w-full min-w-[420px]"
        aria-label="Hmatník — hra"
      >
        <rect
          x={LEFT}
          y={TOP}
          width={NECK_RIGHT - LEFT}
          height={NECK_BOTTOM - TOP}
          fill="#171717"
        />
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

        {Array.from({ length: FRETS }, (_, index) => {
          const x = LEFT + (index + 1) * FRET_W;
          return (
            <line
              key={`fret-${index}`}
              x1={x}
              y1={TOP}
              x2={x}
              y2={NECK_BOTTOM}
              stroke="#525252"
              strokeWidth={1.5}
            />
          );
        })}
        <line
          x1={LEFT}
          y1={TOP}
          x2={LEFT}
          y2={NECK_BOTTOM}
          stroke="#a3a3a3"
          strokeWidth={4}
        />

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
                {pitchClassName(open.midi, display)}
              </text>
              {Array.from({ length: FRETS + 1 }, (_, fret) => {
                const isFb =
                  feedback !== null &&
                  feedback.stringIndex === stringIndex &&
                  feedback.fret === fret;
                return (
                  <g
                    key={`pos-${stringIndex}-${fret}`}
                    onClick={() => answer(stringIndex, fret)}
                    className="cursor-pointer"
                  >
                    <circle
                      cx={noteX(fret)}
                      cy={y}
                      r={9}
                      fill={
                        isFb
                          ? feedback.correct
                            ? "#16a34a"
                            : "#dc2626"
                          : "#2a2a2a"
                      }
                    />
                    {isFb && (
                      <text
                        x={noteX(fret)}
                        y={y}
                        fill="#fafafa"
                        fontSize="7"
                        fontWeight="700"
                        textAnchor="middle"
                        dominantBaseline="central"
                      >
                        {pitchClassName(noteAtFret(open.midi, fret), display)}
                      </text>
                    )}
                  </g>
                );
              })}
            </g>
          );
        })}

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

      <button
        type="button"
        onClick={start}
        className="mt-3 rounded-lg border border-line bg-bg px-3 py-1.5 text-sm text-dim transition-colors hover:border-line-strong"
      >
        Nová hra
      </button>
    </div>
  );
}
