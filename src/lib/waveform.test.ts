import { describe, expect, it } from "vitest";
import { computePeaks } from "./waveform";

// Float32Array stores values at single precision, so peak values are compared
// with a tolerance rather than for exact equality.

describe("computePeaks", () => {
  it("returns the peak amplitude of each bucket", () => {
    const data = new Float32Array([0, 0.5, -1, 0.2, 0, 0, 0.8, -0.3]);
    const peaks = computePeaks(data, 2);
    expect(peaks).toHaveLength(2);
    expect(peaks[0]).toBeCloseTo(1, 5);
    expect(peaks[1]).toBeCloseTo(0.8, 5);
  });

  it("uses absolute values", () => {
    const peaks = computePeaks(new Float32Array([-0.7, -0.2]), 1);
    expect(peaks[0]).toBeCloseTo(0.7, 5);
  });

  it("returns an empty array for zero buckets", () => {
    expect(computePeaks(new Float32Array([0.5]), 0)).toEqual([]);
  });

  it("returns an empty array for empty data", () => {
    expect(computePeaks(new Float32Array([]), 8)).toEqual([]);
  });
});
