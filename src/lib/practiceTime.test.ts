import { describe, expect, it } from "vitest";
import { isPracticeRoute, msToMinutes, PRACTICE_ROUTES } from "./practiceTime";

describe("isPracticeRoute", () => {
  it("is true for every practice route", () => {
    for (const route of PRACTICE_ROUTES) {
      expect(isPracticeRoute(route)).toBe(true);
    }
  });

  it("is false for non-practice routes", () => {
    expect(isPracticeRoute("/")).toBe(false);
    expect(isPracticeRoute("/library")).toBe(false);
    expect(isPracticeRoute("/profile")).toBe(false);
    expect(isPracticeRoute("/recorder")).toBe(false);
    expect(isPracticeRoute("/live")).toBe(false);
  });

  it("tolerates a trailing slash", () => {
    expect(isPracticeRoute("/metronome/")).toBe(true);
  });
});

describe("msToMinutes", () => {
  it("converts milliseconds to minutes", () => {
    expect(msToMinutes(60000)).toBe(1);
    expect(msToMinutes(90000)).toBe(1.5);
  });

  it("returns 0 for non-positive or invalid input", () => {
    expect(msToMinutes(0)).toBe(0);
    expect(msToMinutes(-1000)).toBe(0);
    expect(msToMinutes(Number.NaN)).toBe(0);
  });
});
