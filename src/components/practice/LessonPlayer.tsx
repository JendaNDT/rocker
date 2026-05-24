"use client";

import { useEffect, useState } from "react";
import { audioEngine } from "@/audio/AudioEngine";
import { StepView } from "@/components/practice/StepView";
import type { Lesson } from "@/lib/lessons";

/** Walks the learner through a lesson's steps. */
export function LessonPlayer({
  lesson,
  onComplete,
  onExit,
}: {
  lesson: Lesson;
  onComplete: () => void;
  onExit: () => void;
}) {
  const [stepIndex, setStepIndex] = useState(0);

  // Release the microphone when the lesson is left.
  useEffect(() => {
    return () => {
      void audioEngine.stop();
    };
  }, []);

  const step = lesson.steps[stepIndex];
  const isFirst = stepIndex === 0;
  const isLast = stepIndex === lesson.steps.length - 1;
  const progress = ((stepIndex + 1) / lesson.steps.length) * 100;

  return (
    <div className="mx-auto max-w-2xl p-6">
      <div className="flex items-center justify-between">
        <button
          type="button"
          onClick={onExit}
          className="text-sm text-neutral-400 transition-colors hover:text-neutral-100"
        >
          Zpět na lekce
        </button>
        <span className="text-sm text-neutral-500">
          Krok {stepIndex + 1} / {lesson.steps.length}
        </span>
      </div>

      <h1 className="mt-3 text-2xl font-bold tracking-tight">{lesson.title}</h1>

      <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-neutral-800">
        <div
          className="h-full rounded-full bg-amber-500 transition-all"
          style={{ width: `${progress}%` }}
        />
      </div>

      <div className="mt-6">
        <StepView key={stepIndex} step={step} />
      </div>

      <div className="mt-8 flex justify-between">
        <button
          type="button"
          disabled={isFirst}
          onClick={() => setStepIndex((index) => index - 1)}
          className={
            isFirst
              ? "rounded-lg border border-neutral-900 px-4 py-2 text-sm text-neutral-700"
              : "rounded-lg border border-neutral-800 bg-neutral-900 px-4 py-2 text-sm transition-colors hover:border-neutral-700"
          }
        >
          Zpět
        </button>
        {isLast ? (
          <button
            type="button"
            onClick={onComplete}
            className="rounded-lg bg-amber-500 px-5 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
          >
            Dokončit lekci
          </button>
        ) : (
          <button
            type="button"
            onClick={() => setStepIndex((index) => index + 1)}
            className="rounded-lg bg-amber-500 px-5 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400"
          >
            Další
          </button>
        )}
      </div>
    </div>
  );
}
