import { describe, expect, it } from "vitest";
import { bpmFromTaps, clampBpm, MAX_BPM, MIN_BPM } from "./tempo";

describe("clampBpm", () => {
  it("keeps an in-range value", () => {
    expect(clampBpm(120)).toBe(120);
  });

  it("clamps below the minimum", () => {
    expect(clampBpm(10)).toBe(MIN_BPM);
  });

  it("clamps above the maximum", () => {
    expect(clampBpm(500)).toBe(MAX_BPM);
  });

  it("rounds fractional values", () => {
    expect(clampBpm(119.6)).toBe(120);
  });

  it("falls back to the minimum for invalid input", () => {
    expect(clampBpm(Number.NaN)).toBe(MIN_BPM);
  });
});

describe("bpmFromTaps", () => {
  it("returns null for fewer than two taps", () => {
    expect(bpmFromTaps([])).toBeNull();
    expect(bpmFromTaps([1000])).toBeNull();
  });

  it("computes 120 BPM from taps 500 ms apart", () => {
    expect(bpmFromTaps([0, 500, 1000, 1500])).toBe(120);
  });

  it("computes 60 BPM from taps 1000 ms apart", () => {
    expect(bpmFromTaps([0, 1000, 2000])).toBe(60);
  });

  it("averages uneven intervals", () => {
    // intervals 500 and 700 -> average 600 ms -> 100 BPM
    expect(bpmFromTaps([0, 500, 1200])).toBe(100);
  });
});
