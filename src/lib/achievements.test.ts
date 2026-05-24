import { describe, expect, it } from "vitest";
import {
  ACHIEVEMENTS,
  type AchievementContext,
  buildAchievementContext,
  evaluateAchievements,
  getAchievement,
  newlyUnlockedIds,
} from "./achievements";
import { emptyProgress } from "./progress";

/** A context with nothing unlocked. */
function blankContext(): AchievementContext {
  return {
    longestStreak: 0,
    totalMinutes: 0,
    totalPracticeDays: 0,
    completedLessonIds: [],
    bpmRecords: {},
  };
}

describe("ACHIEVEMENTS catalogue", () => {
  it("has unique ids", () => {
    const ids = ACHIEVEMENTS.map((achievement) => achievement.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("unlocks nothing for a blank context", () => {
    expect(evaluateAchievements(blankContext())).toEqual([]);
  });
});

describe("getAchievement", () => {
  it("finds an achievement by id", () => {
    expect(getAchievement("first-practice")?.title).toBe("První cvičení");
  });

  it("returns undefined for an unknown id", () => {
    expect(getAchievement("nope")).toBeUndefined();
  });
});

describe("streak achievements", () => {
  it("unlocks streak-3 at three days but not streak-7", () => {
    const ctx = { ...blankContext(), longestStreak: 3 };
    const unlocked = evaluateAchievements(ctx);
    expect(unlocked).toContain("streak-3");
    expect(unlocked).not.toContain("streak-7");
  });

  it("unlocks all streak tiers up to the reached length", () => {
    const ctx = { ...blankContext(), longestStreak: 30 };
    const unlocked = evaluateAchievements(ctx);
    expect(unlocked).toContain("streak-3");
    expect(unlocked).toContain("streak-7");
    expect(unlocked).toContain("streak-30");
  });
});

describe("minute achievements", () => {
  it("unlocks minutes-60 at 60 minutes", () => {
    const ctx = { ...blankContext(), totalMinutes: 60 };
    expect(evaluateAchievements(ctx)).toContain("minutes-60");
  });

  it("does not unlock minutes-600 below the threshold", () => {
    const ctx = { ...blankContext(), totalMinutes: 599 };
    expect(evaluateAchievements(ctx)).not.toContain("minutes-600");
  });
});

describe("lesson achievements", () => {
  it("unlocks first-lesson and first-power-chord for the power chord lesson", () => {
    const ctx = { ...blankContext(), completedLessonIds: ["power-chord"] };
    const unlocked = evaluateAchievements(ctx);
    expect(unlocked).toContain("first-lesson");
    expect(unlocked).toContain("first-power-chord");
  });

  it("unlocks first-lesson but not first-power-chord for another lesson", () => {
    const ctx = { ...blankContext(), completedLessonIds: ["akord-em"] };
    const unlocked = evaluateAchievements(ctx);
    expect(unlocked).toContain("first-lesson");
    expect(unlocked).not.toContain("first-power-chord");
  });

  it("unlocks stage-1-done only when every Stage 1 lesson is complete", () => {
    const stage1 = [
      "drzeni",
      "prazdne-struny",
      "akord-em",
      "akord-e",
      "akordy-am-a",
      "prechody",
      "akordy-d-g",
      "strumming",
    ];
    expect(
      evaluateAchievements({ ...blankContext(), completedLessonIds: stage1 }),
    ).toContain("stage-1-done");
    expect(
      evaluateAchievements({
        ...blankContext(),
        completedLessonIds: stage1.slice(0, -1),
      }),
    ).not.toContain("stage-1-done");
  });
});

describe("tempo-120", () => {
  it("unlocks when any BPM record reaches 120", () => {
    const ctx = { ...blankContext(), bpmRecords: { "riff-a": 80, "riff-b": 130 } };
    expect(evaluateAchievements(ctx)).toContain("tempo-120");
  });

  it("stays locked below 120", () => {
    const ctx = { ...blankContext(), bpmRecords: { "riff-a": 119 } };
    expect(evaluateAchievements(ctx)).not.toContain("tempo-120");
  });
});

describe("buildAchievementContext", () => {
  it("derives stats from progress and lesson data", () => {
    const progress = {
      ...emptyProgress(),
      dailyMinutes: { "2026-05-23": 30, "2026-05-24": 35 },
    };
    const ctx = buildAchievementContext(progress, ["akord-em"]);
    expect(ctx.totalMinutes).toBe(65);
    expect(ctx.totalPracticeDays).toBe(2);
    expect(ctx.longestStreak).toBe(2);
    expect(ctx.completedLessonIds).toEqual(["akord-em"]);
  });
});

describe("newlyUnlockedIds", () => {
  it("returns only achievements not already unlocked", () => {
    const ctx = { ...blankContext(), totalPracticeDays: 1, totalMinutes: 60 };
    const fresh = newlyUnlockedIds(ctx, ["first-practice"]);
    expect(fresh).toContain("minutes-60");
    expect(fresh).not.toContain("first-practice");
  });

  it("returns nothing when everything earned is already unlocked", () => {
    const ctx = { ...blankContext(), totalPracticeDays: 1 };
    expect(newlyUnlockedIds(ctx, ["first-practice"])).toEqual([]);
  });
});
