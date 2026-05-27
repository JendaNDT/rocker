"use client";

import Link from "next/link";
import { useState } from "react";
import { buildInstrumentContext } from "@/lib/ai/context";
import { generatePlan, type GeneratedPlan } from "@/lib/ai/planGenerator";
import { STAGES } from "@/lib/lessons";
import { currentStreak } from "@/lib/progress";
import { resolveTuning } from "@/lib/resolveTuning";
import { useAiStore } from "@/stores/aiStore";
import { useCustomTuningsStore } from "@/stores/customTuningsStore";
import { useInstrumentStore } from "@/stores/instrumentStore";
import { useLessonProgressStore } from "@/stores/lessonProgressStore";
import { useProgressStore } from "@/stores/progressStore";
import { useSettingsStore } from "@/stores/settingsStore";

const TIME_OPTIONS = [10, 15, 20, 30];
const TOTAL_LESSONS = STAGES.flatMap((stage) => stage.lessons).length;

const SEGMENT_ACTIVE =
  "rounded-md bg-accent px-3 py-1.5 text-sm font-semibold text-accent-fg";
const SEGMENT_IDLE =
  "rounded-md px-3 py-1.5 text-sm text-dim transition-colors hover:text-text";

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
  const profile = useInstrumentStore((state) => state.profile);
  const tuningId = useSettingsStore((state) => state.tuningId);
  const customTunings = useCustomTuningsStore((state) => state.tunings);

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
      instrumentSummary: buildInstrumentContext(
        profile,
        resolveTuning(tuningId, customTunings).label,
      ),
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
      <div className="rounded-lg border border-line bg-bg p-4">
        <div className="text-sm font-medium text-text">
          Kolik máš dnes času?
        </div>
        <div className="mt-2 inline-flex rounded-lg border border-line p-1">
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
          className="mt-4 block text-sm font-medium text-text"
        >
          Na co se chceš zaměřit? (nepovinné)
        </label>
        <input
          id="plan-focus"
          type="text"
          value={focus}
          onChange={(event) => setFocus(event.target.value)}
          placeholder="např. přechody mezi akordy, palm mute, rychlost"
          className="mt-2 w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-text"
        />

        <button
          type="button"
          onClick={() => void generate()}
          disabled={busy}
          className="mt-4 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-accent-fg transition-colors hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {busy
            ? "Sestavuji plán…"
            : plan
              ? "Vygenerovat znovu"
              : "Vygenerovat plán"}
        </button>
      </div>

      {error && <p className="mt-3 text-sm text-danger">{error}</p>}

      {plan && (
        <div className="mt-5">
          <div className="flex items-baseline justify-between">
            <h2 className="font-semibold">Tvůj plán na dnešek</h2>
            <span className="text-xs text-dim">
              {plan.totalMinutes} min
            </span>
          </div>
          <ol className="mt-3 space-y-2">
            {plan.items.map((item, index) => {
              const row = (
                <div className="flex items-center gap-3">
                  <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-surface-2 text-sm font-semibold text-accent">
                    {index + 1}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-baseline justify-between gap-2">
                      <div className="font-medium">{item.title}</div>
                      <div className="shrink-0 text-xs text-dim">
                        {item.minutes} min
                      </div>
                    </div>
                    <div className="mt-0.5 text-sm text-dim">
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
                      className="block rounded-md border border-line bg-bg p-3 transition-colors hover:border-accent"
                    >
                      {row}
                    </Link>
                  ) : (
                    <div className="rounded-md border border-line bg-bg p-3">
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
