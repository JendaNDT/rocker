import { describe, expect, it } from "vitest";
import { getTuning, nearestString, TUNINGS } from "./tunings";

const standard = getTuning("standard");

describe("getTuning", () => {
  it("returns the requested tuning", () => {
    expect(getTuning("drop-c").id).toBe("drop-c");
  });

  it("falls back to the first tuning for an unknown id", () => {
    expect(getTuning("does-not-exist").id).toBe(TUNINGS[0].id);
  });

  it("every tuning has six strings", () => {
    for (const tuning of TUNINGS) {
      expect(tuning.strings).toHaveLength(6);
    }
  });
});

describe("nearestString", () => {
  it("matches the low E string frequency", () => {
    const result = nearestString(standard, 82.41);
    expect(result?.index).toBe(0);
    expect(result?.string.note).toBe("E2");
    expect(Math.abs(result?.cents ?? 99)).toBeLessThanOrEqual(2);
  });

  it("matches the A string frequency", () => {
    const result = nearestString(standard, 110);
    expect(result?.index).toBe(1);
    expect(result?.string.note).toBe("A2");
  });

  it("reports a sharp string with positive cents", () => {
    const result = nearestString(standard, 113);
    expect(result?.index).toBe(1);
    expect(result?.cents).toBeGreaterThan(0);
  });

  it("returns null for invalid input", () => {
    expect(nearestString(standard, 0)).toBeNull();
    expect(nearestString(standard, -10)).toBeNull();
  });
});
