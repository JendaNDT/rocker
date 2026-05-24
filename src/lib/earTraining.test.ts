import { describe, expect, it } from "vitest";
import {
  CHORD_QUALITIES,
  INTERVALS,
  pickIndex,
  pickRoot,
} from "./earTraining";

describe("ear training data", () => {
  it("offers intervals with unique names and positive semitones", () => {
    expect(INTERVALS.length).toBeGreaterThan(1);
    const names = INTERVALS.map((interval) => interval.name);
    expect(new Set(names).size).toBe(names.length);
    for (const interval of INTERVALS) {
      expect(interval.semitones).toBeGreaterThan(0);
    }
  });

  it("offers chord qualities, each rooted at offset 0", () => {
    expect(CHORD_QUALITIES.length).toBeGreaterThan(1);
    const names = CHORD_QUALITIES.map((chord) => chord.name);
    expect(new Set(names).size).toBe(names.length);
    for (const chord of CHORD_QUALITIES) {
      expect(chord.intervals[0]).toBe(0);
    }
  });
});

describe("pickIndex", () => {
  it("maps the random value into 0..count-1", () => {
    expect(pickIndex(8, null, () => 0)).toBe(0);
    expect(pickIndex(8, null, () => 0.99)).toBe(7);
  });

  it("never repeats the previous index", () => {
    expect(pickIndex(8, 3, () => 3 / 8)).toBe(4);
    expect(pickIndex(8, 3, () => 0)).toBe(0);
  });
});

describe("pickRoot", () => {
  it("stays inside the C3..B3 range", () => {
    expect(pickRoot(() => 0)).toBe(48);
    expect(pickRoot(() => 0.99)).toBe(59);
  });
});
