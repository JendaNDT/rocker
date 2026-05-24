import { describe, expect, it } from "vitest";
import { generateDailyPlan } from "./dailyPlan";
import { STAGES } from "./lessons";

const ALL_LESSON_IDS = STAGES.flatMap((stage) =>
  stage.lessons.map((lesson) => lesson.id),
);

describe("generateDailyPlan", () => {
  it("produces four blocks", () => {
    const plan = generateDailyPlan({
      completedLessonIds: [],
      dayKey: "2026-05-24",
    });
    expect(plan.items.map((item) => item.id)).toEqual([
      "tune",
      "warmup",
      "lesson",
      "focus",
    ]);
  });

  it("totals a 10-20 minute routine", () => {
    const plan = generateDailyPlan({
      completedLessonIds: [],
      dayKey: "2026-05-24",
    });
    expect(plan.totalMinutes).toBeGreaterThanOrEqual(10);
    expect(plan.totalMinutes).toBeLessThanOrEqual(20);
    const sum = plan.items.reduce((acc, item) => acc + item.minutes, 0);
    expect(plan.totalMinutes).toBe(sum);
  });

  it("points the lesson block at the first uncompleted lesson", () => {
    const plan = generateDailyPlan({
      completedLessonIds: [],
      dayKey: "2026-05-24",
    });
    const lesson = plan.items.find((item) => item.id === "lesson");
    expect(lesson?.detail).toContain(STAGES[0].lessons[0].title);
  });

  it("advances the lesson block past completed lessons", () => {
    const firstId = STAGES[0].lessons[0].id;
    const plan = generateDailyPlan({
      completedLessonIds: [firstId],
      dayKey: "2026-05-24",
    });
    const lesson = plan.items.find((item) => item.id === "lesson");
    expect(lesson?.detail).toContain(STAGES[0].lessons[1].title);
  });

  it("falls back to a review message when all lessons are done", () => {
    const plan = generateDailyPlan({
      completedLessonIds: ALL_LESSON_IDS,
      dayKey: "2026-05-24",
    });
    const lesson = plan.items.find((item) => item.id === "lesson");
    expect(lesson?.detail).toContain("hotové");
  });

  it("rotates the focus block across the week", () => {
    // Seven consecutive days should expose more than one focus title.
    const titles = new Set<string>();
    for (let day = 18; day <= 24; day += 1) {
      const plan = generateDailyPlan({
        completedLessonIds: [],
        dayKey: `2026-05-${day}`,
      });
      const focus = plan.items.find((item) => item.id === "focus");
      if (focus) titles.add(focus.title);
    }
    expect(titles.size).toBeGreaterThan(1);
  });

  it("is deterministic for the same day", () => {
    const a = generateDailyPlan({
      completedLessonIds: [],
      dayKey: "2026-05-24",
    });
    const b = generateDailyPlan({
      completedLessonIds: [],
      dayKey: "2026-05-24",
    });
    expect(a).toEqual(b);
  });
});
