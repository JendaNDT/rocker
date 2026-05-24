"use client";

import { useState } from "react";
import { LessonPlayer } from "@/components/practice/LessonPlayer";
import { RiffTrainer } from "@/components/practice/RiffTrainer";
import { SurgicalTrainer } from "@/components/practice/SurgicalTrainer";
import { useWakeLock } from "@/components/useWakeLock";
import { stageCompletion } from "@/lib/lessonEngine";
import { getLesson, STAGES } from "@/lib/lessons";
import { useHydrated } from "@/lib/useHydrated";
import { useLessonProgressStore } from "@/stores/lessonProgressStore";

type Tab = "lessons" | "riff" | "surgical";

const TABS: { id: Tab; label: string }[] = [
  { id: "lessons", label: "Lekce" },
  { id: "riff", label: "Riff trainer" },
  { id: "surgical", label: "Přesný trénink" },
];

const SEGMENT_ACTIVE =
  "rounded-md bg-neutral-800 px-3 py-1.5 text-sm text-amber-500";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:text-neutral-100";

export default function PracticePage() {
  useWakeLock();

  const [tab, setTab] = useState<Tab>("lessons");
  const [activeLessonId, setActiveLessonId] = useState<string | null>(null);

  const completedLessonIds = useLessonProgressStore(
    (state) => state.completedLessonIds,
  );
  const markLessonComplete = useLessonProgressStore(
    (state) => state.markLessonComplete,
  );
  const hydrated = useHydrated();
  const completed = hydrated ? completedLessonIds : [];

  const activeLesson = activeLessonId ? getLesson(activeLessonId) : undefined;

  if (activeLesson) {
    return (
      <LessonPlayer
        lesson={activeLesson}
        onComplete={() => {
          markLessonComplete(activeLesson.id);
          setActiveLessonId(null);
        }}
        onExit={() => setActiveLessonId(null)}
      />
    );
  }

  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Cvičení</h1>
      <p className="mt-1 text-neutral-400">Výuková cesta a trénink.</p>

      <div className="mt-6 inline-flex flex-wrap rounded-lg border border-neutral-800 p-1">
        {TABS.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setTab(item.id)}
            className={tab === item.id ? SEGMENT_ACTIVE : SEGMENT_IDLE}
          >
            {item.label}
          </button>
        ))}
      </div>

      {tab === "lessons" && (
        <div>
          {STAGES.map((stage) => {
            const completion = stageCompletion(stage, completed);
            return (
              <section key={stage.id} className="mt-8">
                <div className="flex items-baseline justify-between">
                  <h2 className="text-lg font-semibold">{stage.title}</h2>
                  <span className="text-sm text-neutral-500">
                    {completion.done}/{completion.total} lekcí
                  </span>
                </div>
                <p className="mt-1 text-sm text-neutral-400">
                  {stage.description}
                </p>

                <div className="mt-3 space-y-2">
                  {stage.lessons.map((lesson) => {
                    const done = completed.includes(lesson.id);
                    return (
                      <button
                        key={lesson.id}
                        type="button"
                        onClick={() => setActiveLessonId(lesson.id)}
                        className="flex w-full items-center justify-between rounded-lg border border-neutral-800 bg-neutral-900 px-4 py-3 text-left transition-colors hover:border-amber-600"
                      >
                        <span className="font-medium">{lesson.title}</span>
                        <span className="ml-3 shrink-0 text-sm text-neutral-500">
                          {done ? (
                            <span className="text-emerald-400">hotovo ✓</span>
                          ) : (
                            `${lesson.estimatedMinutes} min`
                          )}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </section>
            );
          })}
        </div>
      )}

      {tab === "riff" && (
        <div className="mt-8">
          <RiffTrainer />
        </div>
      )}

      {tab === "surgical" && (
        <div className="mt-8">
          <SurgicalTrainer />
        </div>
      )}
    </div>
  );
}
