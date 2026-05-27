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

  it("every tuning's strings ascend in MIDI low to high", () => {
    // The Guided tuner labels strings 6→1 left to right; the data is stored
    // low (6th) to high (1st) and every entry should be strictly monotonic
    // so the UI doesn't show a string out of order.
    for (const tuning of TUNINGS) {
      for (let i = 1; i < tuning.strings.length; i += 1) {
        expect(tuning.strings[i].midi).toBeGreaterThan(
          tuning.strings[i - 1].midi,
        );
      }
    }
  });

  it("contains the three open / alternate tunings added in Phase 3A", () => {
    expect(TUNINGS.some((t) => t.id === "open-g")).toBe(true);
    expect(TUNINGS.some((t) => t.id === "open-d")).toBe(true);
    expect(TUNINGS.some((t) => t.id === "dadgad")).toBe(true);
  });

  it("Open G is D G D G B D", () => {
    const tuning = getTuning("open-g");
    expect(tuning.strings.map((s) => s.note)).toEqual([
      "D2",
      "G2",
      "D3",
      "G3",
      "B3",
      "D4",
    ]);
  });

  it("Open D is D A D F# A D", () => {
    const tuning = getTuning("open-d");
    expect(tuning.strings.map((s) => s.note)).toEqual([
      "D2",
      "A2",
      "D3",
      "F#3",
      "A3",
      "D4",
    ]);
  });

  it("DADGAD is D A D G A D", () => {
    const tuning = getTuning("dadgad");
    expect(tuning.strings.map((s) => s.note)).toEqual([
      "D2",
      "A2",
      "D3",
      "G3",
      "A3",
      "D4",
    ]);
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
