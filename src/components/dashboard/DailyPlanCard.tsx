"use client";

import Link from "next/link";
import { generateDailyPlan } from "@/lib/dailyPlan";
import { useHydrated } from "@/lib/useHydrated";
import { useLessonProgressStore } from "@/stores/lessonProgressStore";

/**
 * The recommended ~15-minute routine for today, rendered as a numbered list
 * of links to the relevant screens. The plan depends on the local date and
 * on persisted lesson progress, so the whole card is gated behind
 * useHydrated to avoid a server/client mismatch.
 */
export function DailyPlanCard() {
  const hydrated = useHydrated();
  const completedLessonIds = useLessonProgressStore(
    (state) => state.completedLessonIds,
  );

  if (!hydrated) {
    return (
      <section className="rounded-lg border border-neutral-800 bg-neutral-900 p-4">
        <h2 className="font-semibold">Dnešní plán</h2>
        <p className="mt-2 text-sm text-neutral-500">Načítám denní plán…</p>
      </section>
    );
  }

  const plan = generateDailyPlan({ completedLessonIds });

  return (
    <section className="rounded-lg border border-neutral-800 bg-neutral-900 p-4">
      <div className="flex items-baseline justify-between">
        <h2 className="font-semibold">Dnešní plán</h2>
        <span className="text-xs text-neutral-400">
          {plan.totalMinutes} min
        </span>
      </div>

      <ol className="mt-3 space-y-2">
        {plan.items.map((item, index) => (
          <li key={item.id}>
            <Link
              href={item.href}
              className="flex items-center gap-3 rounded-md border border-neutral-800 bg-neutral-950 p-3 transition-colors hover:border-amber-600"
            >
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
            </Link>
          </li>
        ))}
      </ol>
    </section>
  );
}
