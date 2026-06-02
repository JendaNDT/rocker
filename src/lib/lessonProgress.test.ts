import { describe, expect, it } from "vitest";
import { sanitizeCompletedLessonIds } from "./lessonProgress";

describe("sanitizeCompletedLessonIds", () => {
  it("keeps a clean array of strings unchanged", () => {
    expect(sanitizeCompletedLessonIds(["a", "b", "c"])).toEqual([
      "a",
      "b",
      "c",
    ]);
  });

  it("returns an empty array for non-array input", () => {
    expect(sanitizeCompletedLessonIds(null)).toEqual([]);
    expect(sanitizeCompletedLessonIds(undefined)).toEqual([]);
    expect(sanitizeCompletedLessonIds({})).toEqual([]);
    expect(sanitizeCompletedLessonIds("a")).toEqual([]);
    expect(sanitizeCompletedLessonIds(42)).toEqual([]);
  });

  it("drops non-string and empty entries", () => {
    expect(
      sanitizeCompletedLessonIds(["a", 1, null, "", "b", {}, undefined]),
    ).toEqual(["a", "b"]);
  });

  it("dedupes repeated ids", () => {
    expect(sanitizeCompletedLessonIds(["a", "a", "b", "a"])).toEqual([
      "a",
      "b",
    ]);
  });
});
