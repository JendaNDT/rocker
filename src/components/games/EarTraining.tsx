"use client";

import { useState } from "react";
import { playChord } from "@/audio/chordPlayer";
import {
  CHORD_QUALITIES,
  INTERVALS,
  pickIndex,
  pickRoot,
} from "@/lib/earTraining";

/** Which kind of question the ear-training game asks. */
type Mode = "interval" | "chord";

const SEGMENT_ACTIVE =
  "rounded-md bg-amber-500 px-3 py-1.5 text-sm font-semibold text-neutral-950";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:text-neutral-100";

const ANSWER_IDLE =
  "rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-2 text-sm transition-colors hover:border-amber-600";
const ANSWER_RIGHT =
  "rounded-lg border border-emerald-600 bg-emerald-600/20 px-3 py-2 text-sm";
const ANSWER_WRONG =
  "rounded-lg border border-red-600 bg-red-600/20 px-3 py-2 text-sm";

/**
 * Ear training game (Bod 19): plays an interval or a chord, the player picks
 * which one it was. Uses the chord synthesis from Bod 5.
 */
export function EarTraining() {
  const [mode, setMode] = useState<Mode>("interval");
  const [started, setStarted] = useState(false);
  const [index, setIndex] = useState(0);
  const [root, setRoot] = useState(48);
  const [score, setScore] = useState(0);
  const [streak, setStreak] = useState(0);
  const [feedback, setFeedback] = useState<{
    chosen: number;
    correct: boolean;
  } | null>(null);

  const options = mode === "interval" ? INTERVALS : CHORD_QUALITIES;

  function begin(nextMode: Mode) {
    const count =
      nextMode === "interval" ? INTERVALS.length : CHORD_QUALITIES.length;
    setMode(nextMode);
    setStarted(true);
    setIndex(pickIndex(count, null));
    setRoot(pickRoot());
    setScore(0);
    setStreak(0);
    setFeedback(null);
  }

  function chooseMode(nextMode: Mode) {
    if (started) {
      begin(nextMode);
    } else {
      setMode(nextMode);
    }
  }

  /** MIDI notes of the current question. */
  function currentMidi(): number[] {
    if (mode === "interval") {
      return [root, root + INTERVALS[index].semitones];
    }
    return CHORD_QUALITIES[index].intervals.map((offset) => root + offset);
  }

  function play() {
    void playChord(currentMidi());
  }

  function answer(chosen: number) {
    const correct = chosen === index;
    setFeedback({ chosen, correct });
    if (correct) {
      setScore((value) => value + 1);
      setStreak((value) => value + 1);
      const count =
        mode === "interval" ? INTERVALS.length : CHORD_QUALITIES.length;
      setIndex((previous) => pickIndex(count, previous));
      setRoot(pickRoot());
    } else {
      setStreak(0);
    }
  }

  function answerClass(i: number): string {
    if (feedback) {
      if (i === feedback.chosen) {
        return feedback.correct ? ANSWER_RIGHT : ANSWER_WRONG;
      }
      if (!feedback.correct && i === index) return ANSWER_RIGHT;
    }
    return ANSWER_IDLE;
  }

  return (
    <div className="rounded-lg border border-neutral-800 bg-neutral-900 p-4">
      <div className="inline-flex rounded-lg border border-neutral-800 p-1">
        <button
          type="button"
          onClick={() => chooseMode("interval")}
          className={mode === "interval" ? SEGMENT_ACTIVE : SEGMENT_IDLE}
        >
          Intervaly
        </button>
        <button
          type="button"
          onClick={() => chooseMode("chord")}
          className={mode === "chord" ? SEGMENT_ACTIVE : SEGMENT_IDLE}
        >
          Akordy
        </button>
      </div>

      {!started ? (
        <div className="mt-4">
          <p className="text-sm text-neutral-300">
            Appka přehraje{" "}
            {mode === "interval" ? "dva tóny" : "akord"} a ty poznáš sluchem,
            o co šlo. Tóny si můžeš přehrát kolikrát chceš.
          </p>
          <button
            type="button"
            onClick={() => begin(mode)}
            className="mt-4 rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
          >
            Začít hru
          </button>
        </div>
      ) : (
        <div className="mt-4">
          <div className="flex items-center justify-between gap-4">
            <button
              type="button"
              onClick={play}
              className="rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
            >
              Přehrát
            </button>
            <span className="text-sm text-neutral-400">
              Skóre {score} · série {streak}
            </span>
          </div>

          <div className="mt-4 grid grid-cols-2 gap-2 sm:grid-cols-4">
            {options.map((option, i) => (
              <button
                key={option.name}
                type="button"
                onClick={() => answer(i)}
                className={answerClass(i)}
              >
                {option.name}
              </button>
            ))}
          </div>

          {feedback && (
            <p
              className={
                feedback.correct
                  ? "mt-3 text-sm text-emerald-400"
                  : "mt-3 text-sm text-red-400"
              }
            >
              {feedback.correct
                ? "Správně!"
                : `Bylo to: ${options[index].name} — zkus to znovu.`}
            </p>
          )}

          <button
            type="button"
            onClick={() => begin(mode)}
            className="mt-3 rounded-lg border border-neutral-800 bg-neutral-950 px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:border-neutral-700"
          >
            Nová hra
          </button>
        </div>
      )}
    </div>
  );
}
