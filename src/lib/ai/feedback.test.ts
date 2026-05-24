import { describe, expect, it } from "vitest";
import { summarizePractice } from "./feedback";

const TODAY = "2026-05-24";

describe("summarizePractice", () => {
  it("includes lesson progress and the achievement count", () => {
    const text = summarizePractice(
      {
        completedLessons: 5,
        totalLessons: 12,
        dailyMinutes: {},
        bpmRecords: {},
        achievementCount: 2,
      },
      TODAY,
    );
    expect(text).toContain("Rocker");
    expect(text).toContain("5 z 12");
    expect(text).toContain("Odemčené achievementy: 2");
  });

  it("reports an active streak when recent days were practiced", () => {
    const text = summarizePractice(
      {
        completedLessons: 0,
        totalLessons: 12,
        dailyMinutes: {
          "2026-05-24": 20,
          "2026-05-23": 15,
          "2026-05-22": 10,
        },
        bpmRecords: {},
        achievementCount: 0,
      },
      TODAY,
    );
    expect(text).toContain("3 dní v řadě");
  });

  it("notes the absence of a streak when nothing recent was practiced", () => {
    const text = summarizePractice(
      {
        completedLessons: 0,
        totalLessons: 12,
        dailyMinutes: { "2026-01-01": 30 },
        bpmRecords: {},
        achievementCount: 0,
      },
      TODAY,
    );
    expect(text).toContain("nemám rozjetou sérii");
  });

  it("counts only the last seven days as this week's minutes", () => {
    const text = summarizePractice(
      {
        completedLessons: 0,
        totalLessons: 12,
        dailyMinutes: { "2026-05-24": 25, "2026-01-01": 100 },
        bpmRecords: {},
        achievementCount: 0,
      },
      TODAY,
    );
    expect(text).toContain("Tento týden: 25 minut");
    expect(text).toContain("Odcvičeno celkem: 125 minut");
  });

  it("includes BPM records when present and omits them otherwise", () => {
    const withBpm = summarizePractice(
      {
        completedLessons: 0,
        totalLessons: 12,
        dailyMinutes: {},
        bpmRecords: { gallop: 120, power: 90 },
        achievementCount: 0,
      },
      TODAY,
    );
    expect(withBpm).toContain("nejvyšší 120 BPM");

    const withoutBpm = summarizePractice(
      {
        completedLessons: 0,
        totalLessons: 12,
        dailyMinutes: {},
        bpmRecords: {},
        achievementCount: 0,
      },
      TODAY,
    );
    expect(withoutBpm).not.toContain("BPM");
  });
});
