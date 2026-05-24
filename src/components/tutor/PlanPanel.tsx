"use client";

import Link from "next/link";
import { useState } from "react";
import { generatePlan, type GeneratedPlan } from "@/lib/ai/planGenerator";
import { STAGES } from "@/lib/lessons";
import { currentStreak } from "@/lib/progress";
import { useAiStore } from "@/stores/aiStore";
import { useLessonProgressStore } from "@/stores/lessonProgressStore";
import { useProgressStore } from "@/stores/progressStore";

const TIME_OPTIONS = [10, 15, 20, 30];
const TOTAL_LESSONS = STAGES.flatMap((stage) => stage.lessons).length;

const SEGMENT_ACTIVE =
  "rounded-md bg-amber-500 px-3 py-1.5 text-sm font-semibold text-neutral-950";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-neutral-400 transition-colors hover:text-neutral-100";

/**
 * The "Plán cvičení" panel of the AI tutor screen: collects today's available
 * time and an optional focus, then shows an AI-generated practice plan.
 * Assumes an API key is set — the parent screen gates on that. Part of Bod 14.
 */
export function PlanPanel() {
  const apiKey = useAiStore((state) => state.apiKey);
  const model = useAiStore((state) => state.model);
  const completedLessonIds = useLessonProgressStore(
    (state) => state.completedLessonIds,
  );
  const dailyMinutes = useProgressStore((state) => state.dailyMinutes);

  const [minutes, setMinutes] = useState(15);
  const [focus, setFocus] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [plan, setPlan] = useState<GeneratedPlan | null>(null);

  async function generate() {
    if (busy) return;
    setBusy(true);
    setError(null);
    const result = await generatePlan({
      apiKey,
      model,
      minutes,
      focus: focus.trim() || undefined,
      completedLessons: completedLessonIds.length,
      totalLessons: TOTAL_LESSONS,
      streakDays: currentStreak(dailyMinutes),
    });
    setBusy(false);
    if (result.ok) {
      setPlan(result.plan);
    } else {
      setPlan(null);
      setError(result.error);
    }
  }

  return (
    <div>
      <div className="rounded-lg border border-neutral-800 bg-neutral-950 p-4">
        <div className="text-sm font-medium text-neutral-300">
          Kolik máš dnes času?
        </div>
        <div className="mt-2 inline-flex rounded-lg border border-neutral-800 p-1">
          {TIME_OPTIONS.map((option) => (
            <button
              key={option}
              type="button"
              onClick={() => setMinutes(option)}
              className={minutes === option ? SEGMENT_ACTIVE : SEGMENT_IDLE}
            >
              {option} min
            </button>
          ))}
        </div>

        <label
          htmlFor="plan-focus"
          className="mt-4 block text-sm font-medium text-neutral-300"
        >
          Na co se chceš zaměřit? (nepovinné)
        </label>
        <input
          id="plan-focus"
          type="text"
          value={focus}
          onChange={(event) => setFocus(event.target.value)}
          placeholder="např. přechody mezi akordy, palm mute, rychlost"
          className="mt-2 w-full rounded-lg border border-neutral-800 bg-neutral-900 px-3 py-2 text-sm text-neutral-100"
        />

        <button
          type="button"
          onClick={() => void generate()}
          disabled={busy}
          className="mt-4 rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-neutral-950 transition-colors hover:bg-amber-400 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {busy
            ? "Sestavuji plán…"
            : plan
              ? "Vygenerovat znovu"
              : "Vygenerovat plán"}
        </button>
      </div>

      {error && <p className="mt-3 text-sm text-red-400">{error}</p>}

      {plan && (
        <div className="mt-5">
          <div className="flex items-baseline justify-between">
            <h2 className="font-semibold">Tvůj plán na dnešek</h2>
            <span className="text-xs text-neutral-400">
              {plan.totalMinutes} min
            </span>
          </div>
          <ol className="mt-3 space-y-2">
            {plan.items.map((item, index) => {
              const row = (
                <div className="flex items-center gap-3">
                  <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-neutral-800 text-sm font-semibold text-amber-500">
                    {index + 1}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-baseline justify-between gap-2">
                      <div className="font-medium">{item.title}</div>
                      <div className="shrink-0 text-xs text-neutral-400">
                        {item.minutes} min
                      </div>
                    </div>
                    <div className="mt-0.5 text-sm text-neutral-400">
                      {item.detail}
                    </div>
                  </div>
                </div>
              );
              return (
                <li key={index}>
                  {item.href ? (
                    <Link
                      href={item.href}
                      className="block rounded-md border border-neutral-800 bg-neutral-950 p-3 transition-colors hover:border-amber-600"
                    >
                      {row}
                    </Link>
                  ) : (
                    <div className="rounded-md border border-neutral-800 bg-neutral-950 p-3">
                      {row}
                    </div>
                  )}
                </li>
              );
            })}
          </ol>
        </div>
      )}
    </div>
  );
}
