import { describe, expect, it } from "vitest";
import {
  currentStreak,
  emptyProgress,
  formatDayKey,
  longestStreak,
  practicedToday,
  sanitizeProgress,
  shiftDayKey,
  todayKey,
  totalMinutes,
  totalPracticeDays,
} from "./progress";

describe("formatDayKey", () => {
  it("formats a date as YYYY-MM-DD in local time", () => {
    expect(formatDayKey(new Date(2026, 4, 24))).toBe("2026-05-24");
  });

  it("zero-pads month and day", () => {
    expect(formatDayKey(new Date(2026, 0, 3))).toBe("2026-01-03");
  });
});

describe("todayKey", () => {
  it("uses the supplied date", () => {
    expect(todayKey(new Date(2026, 11, 31))).toBe("2026-12-31");
  });
});

describe("shiftDayKey", () => {
  it("moves forward by days", () => {
    expect(shiftDayKey("2026-05-24", 1)).toBe("2026-05-25");
  });

  it("moves backward by days", () => {
    expect(shiftDayKey("2026-05-24", -1)).toBe("2026-05-23");
  });

  it("crosses a month boundary", () => {
    expect(shiftDayKey("2026-05-31", 1)).toBe("2026-06-01");
  });

  it("crosses a year boundary", () => {
    expect(shiftDayKey("2026-01-01", -1)).toBe("2025-12-31");
  });
});

describe("practicedToday", () => {
  it("is true when today has minutes", () => {
    expect(practicedToday({ "2026-05-24": 12 }, "2026-05-24")).toBe(true);
  });

  it("is false when today has zero or is absent", () => {
    expect(practicedToday({ "2026-05-24": 0 }, "2026-05-24")).toBe(false);
    expect(practicedToday({}, "2026-05-24")).toBe(false);
  });
});

describe("currentStreak", () => {
  it("is 0 with no practice", () => {
    expect(currentStreak({}, "2026-05-24")).toBe(0);
  });

  it("counts consecutive days ending today", () => {
    const days = {
      "2026-05-22": 10,
      "2026-05-23": 15,
      "2026-05-24": 20,
    };
    expect(currentStreak(days, "2026-05-24")).toBe(3);
  });

  it("stays alive when today is not yet practiced but yesterday was", () => {
    const days = { "2026-05-22": 10, "2026-05-23": 15 };
    expect(currentStreak(days, "2026-05-24")).toBe(2);
  });

  it("is 0 when neither today nor yesterday has practice", () => {
    const days = { "2026-05-20": 10, "2026-05-21": 10 };
    expect(currentStreak(days, "2026-05-24")).toBe(0);
  });

  it("stops at a gap", () => {
    const days = {
      "2026-05-20": 10,
      "2026-05-23": 15,
      "2026-05-24": 20,
    };
    expect(currentStreak(days, "2026-05-24")).toBe(2);
  });
});

describe("longestStreak", () => {
  it("is 0 with no practice", () => {
    expect(longestStreak({})).toBe(0);
  });

  it("finds the longest run across gaps", () => {
    const days = {
      "2026-05-01": 10,
      "2026-05-02": 10,
      "2026-05-03": 10,
      "2026-05-10": 10,
      "2026-05-11": 10,
    };
    expect(longestStreak(days)).toBe(3);
  });

  it("ignores days with zero minutes", () => {
    const days = { "2026-05-01": 10, "2026-05-02": 0, "2026-05-03": 10 };
    expect(longestStreak(days)).toBe(1);
  });
});

describe("totalMinutes", () => {
  it("sums all days", () => {
    expect(totalMinutes({ "2026-05-23": 15, "2026-05-24": 20 })).toBe(35);
  });

  it("is 0 for empty progress", () => {
    expect(totalMinutes({})).toBe(0);
  });
});

describe("totalPracticeDays", () => {
  it("counts distinct practiced days", () => {
    const days = { "2026-05-23": 15, "2026-05-24": 20, "2026-05-25": 0 };
    expect(totalPracticeDays(days)).toBe(2);
  });
});

describe("sanitizeProgress", () => {
  it("returns empty progress for non-objects", () => {
    expect(sanitizeProgress(null)).toEqual(emptyProgress());
    expect(sanitizeProgress("nonsense")).toEqual(emptyProgress());
    expect(sanitizeProgress(42)).toEqual(emptyProgress());
  });

  it("keeps valid data", () => {
    const data = {
      dailyMinutes: { "2026-05-24": 20 },
      bpmRecords: { "riff-1": 120 },
      unlockedAchievements: ["first-power-chord"],
    };
    expect(sanitizeProgress(data)).toEqual(data);
  });

  it("drops malformed numeric entries", () => {
    const data = {
      dailyMinutes: { "2026-05-24": 20, bad: "x", zero: 0, neg: -5 },
      bpmRecords: {},
      unlockedAchievements: [],
    };
    expect(sanitizeProgress(data).dailyMinutes).toEqual({ "2026-05-24": 20 });
  });

  it("drops non-string achievements and de-duplicates", () => {
    const data = {
      dailyMinutes: {},
      bpmRecords: {},
      unlockedAchievements: ["a", "a", 5, null, "b"],
    };
    expect(sanitizeProgress(data).unlockedAchievements).toEqual(["a", "b"]);
  });

  it("fills missing sections with empty defaults", () => {
    expect(sanitizeProgress({})).toEqual(emptyProgress());
  });
});
