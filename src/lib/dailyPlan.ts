// Daily practice plan — pure logic, no DOM. Unit-tested in dailyPlan.test.ts.
// Part of Bod 10: a recommended ~15-minute routine shown on the dashboard.

import { type DayKey, todayKey } from "./progress";
import { STAGES } from "./lessons";

/** One block of the daily routine. */
export interface PlanItem {
  /** Stable id for React keys. */
  id: string;
  /** Czech display title. */
  title: string;
  /** Czech one-line instruction. */
  detail: string;
  /** Suggested duration in minutes. */
  minutes: number;
  /** Screen route the block sends the learner to. */
  href: string;
}

/** A full day's recommended routine. */
export interface DailyPlan {
  items: PlanItem[];
  /** Sum of every block's minutes. */
  totalMinutes: number;
}

/** Lessons flattened in roadmap order (Stage 1 → Stage 2 → …). */
function lessonsInOrder() {
  return STAGES.flatMap((stage) => stage.lessons);
}

/** The first lesson the learner has not completed, or undefined if all done. */
function nextLesson(completedLessonIds: string[]) {
  return lessonsInOrder().find(
    (lesson) => !completedLessonIds.includes(lesson.id),
  );
}

/**
 * Main-practice blocks rotated by day of week, so the routine does not feel
 * identical every day. One is picked per day.
 */
const FOCUS_BLOCKS: Omit<PlanItem, "id">[] = [
  {
    title: "Riff trainer",
    detail: "Vyber si riff a projeď ho zpomalený, pak přidej tempo.",
    minutes: 5,
    href: "/practice",
  },
  {
    title: "YouTube cvičení",
    detail: "Pusť si tutoriál nebo skladbu a procvič jednu pasáž ve smyčce.",
    minutes: 5,
    href: "/youtube",
  },
  {
    title: "Přesný trénink",
    detail: "Najdi chybové místo a vyčisti ho pár soustředěnými repeticemi.",
    minutes: 5,
    href: "/practice",
  },
];

/** Index into FOCUS_BLOCKS derived from the day of week of a DayKey. */
function focusIndexForDay(dayKey: DayKey): number {
  const [year, month, day] = dayKey.split("-").map(Number);
  const weekday = new Date(year, month - 1, day).getDay();
  return weekday % FOCUS_BLOCKS.length;
}

/**
 * Builds the recommended routine for a day. The lesson block adapts to the
 * learner's progress; the main-practice block rotates by weekday. The plan
 * describes a suggested routine — it does not track which blocks were done.
 */
export function generateDailyPlan(opts: {
  completedLessonIds: string[];
  dayKey?: DayKey;
}): DailyPlan {
  const dayKey = opts.dayKey ?? todayKey();
  const lesson = nextLesson(opts.completedLessonIds);

  const lessonBlock: PlanItem = lesson
    ? {
        id: "lesson",
        title: "Lekce",
        detail: `Pokračuj lekcí „${lesson.title}".`,
        minutes: 7,
        href: "/practice",
      }
    : {
        id: "lesson",
        title: "Lekce",
        detail: "Všechny lekce máš hotové — zopakuj si svou oblíbenou.",
        minutes: 7,
        href: "/practice",
      };

  const focus = FOCUS_BLOCKS[focusIndexForDay(dayKey)];

  const items: PlanItem[] = [
    {
      id: "tune",
      title: "Naladění",
      detail: "Nalaď kytaru, ať cvičíš na čistém zvuku.",
      minutes: 2,
      href: "/tuner",
    },
    {
      id: "warmup",
      title: "Rozehřátí",
      detail: "Chromatika přes všechny prsty v pomalém tempu s metronomem.",
      minutes: 3,
      href: "/metronome",
    },
    lessonBlock,
    { id: "focus", ...focus },
  ];

  return {
    items,
    totalMinutes: items.reduce((sum, item) => sum + item.minutes, 0),
  };
}
