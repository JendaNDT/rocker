"use client";

import { useState } from "react";
import { AmpWidget } from "@/components/amp/AmpWidget";
import { MetronomeWidget } from "@/components/metronome/MetronomeWidget";
import { LessonPlayer } from "@/components/practice/LessonPlayer";
import { RiffTrainer } from "@/components/practice/RiffTrainer";
import { SurgicalTrainer } from "@/components/practice/SurgicalTrainer";
import { PageContainer } from "@/components/PageContainer";
import { useWakeLock } from "@/components/useWakeLock";
import { YouTubePlayer } from "@/components/youtube/YouTubePlayer";
import { useResolvedLayoutMode } from "@/lib/layout";
import { stageCompletion } from "@/lib/lessonEngine";
import { getLesson, STAGES } from "@/lib/lessons";
import { useHydrated } from "@/lib/useHydrated";
import { useLessonProgressStore } from "@/stores/lessonProgressStore";

type Tab = "lessons" | "play" | "surgical";
type PlaySource = "file" | "youtube";

const TABS: { id: Tab; label: string }[] = [
  { id: "lessons", label: "Lekce" },
  { id: "play", label: "Hraj se skladbou" },
  { id: "surgical", label: "Přesný trénink" },
];

const SEGMENT_ACTIVE =
  "rounded-md bg-surface-2 px-3 py-1.5 text-sm text-accent";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

// Lesson buttons within a stage: two columns on desktop, stacked on mobile.
const LESSON_GRID = {
  desktop: "mt-3 grid grid-cols-2 gap-2",
  mobile: "mt-3 space-y-2",
  auto: "mt-3 space-y-2 sm:grid sm:grid-cols-2 sm:gap-2 sm:space-y-0",
};

export default function PracticePage() {
  useWakeLock();

  const [tab, setTab] = useState<Tab>("lessons");
  const [playSource, setPlaySource] = useState<PlaySource>("file");
  const [activeLessonId, setActiveLessonId] = useState<string | null>(null);

  const completedLessonIds = useLessonProgressStore(
    (state) => state.completedLessonIds,
  );
  const markLessonComplete = useLessonProgressStore(
    (state) => state.markLessonComplete,
  );
  const hydrated = useHydrated();
  const layoutMode = useResolvedLayoutMode();
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
    <PageContainer size="focused">
      <h1 className="text-3xl font-bold tracking-tight">Cvičení</h1>
      <p className="mt-1 text-dim">Výuková cesta a trénink.</p>

      <div className="mt-6 inline-flex flex-wrap rounded-lg border border-line p-1">
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

      <div className="mt-6 space-y-3">
        <MetronomeWidget />
        <AmpWidget />
      </div>

      {tab === "lessons" && (
        <div>
          {STAGES.map((stage) => {
            const completion = stageCompletion(stage, completed);
            return (
              <section key={stage.id} className="mt-8">
                <div className="flex items-baseline justify-between">
                  <h2 className="text-lg font-semibold">{stage.title}</h2>
                  <span className="text-sm text-muted">
                    {completion.done}/{completion.total} lekcí
                  </span>
                </div>
                <p className="mt-1 text-sm text-dim">
                  {stage.description}
                </p>

                <div className={LESSON_GRID[layoutMode]}>
                  {stage.lessons.map((lesson) => {
                    const done = completed.includes(lesson.id);
                    return (
                      <button
                        key={lesson.id}
                        type="button"
                        onClick={() => setActiveLessonId(lesson.id)}
                        className="flex w-full items-center justify-between rounded-lg border border-line bg-surface px-4 py-3 text-left transition-colors hover:border-accent"
                      >
                        <span className="font-medium">{lesson.title}</span>
                        <span className="ml-3 shrink-0 text-sm text-muted">
                          {done ? (
                            <span className="text-success">hotovo ✓</span>
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

      {tab === "play" && (
        <div className="mt-8">
          <div className="text-sm font-medium text-text">Zdroj</div>
          <div className="mt-2 inline-flex rounded-lg border border-line p-1">
            <button
              type="button"
              onClick={() => setPlaySource("file")}
              className={
                playSource === "file" ? SEGMENT_ACTIVE : SEGMENT_IDLE
              }
            >
              Vlastní soubor
            </button>
            <button
              type="button"
              onClick={() => setPlaySource("youtube")}
              className={
                playSource === "youtube" ? SEGMENT_ACTIVE : SEGMENT_IDLE
              }
            >
              YouTube
            </button>
          </div>
          {playSource === "file" ? <RiffTrainer /> : <YouTubePlayer />}
        </div>
      )}

      {tab === "surgical" && (
        <div className="mt-8">
          <SurgicalTrainer />
        </div>
      )}
    </PageContainer>
  );
}
