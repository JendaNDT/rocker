import { describe, expect, it } from "vitest";
import { computeRms } from "./pitchDetector";

describe("computeRms", () => {
  it("je 0 pro prázdný buffer", () => {
    expect(computeRms(new Float32Array(0))).toBe(0);
  });

  it("je 0 pro ticho", () => {
    expect(computeRms(new Float32Array(128))).toBe(0);
  });

  it("vrátí konstantní amplitudu", () => {
    expect(computeRms(new Float32Array(64).fill(0.5))).toBeCloseTo(0.5);
  });

  it("spočítá RMS střídavého signálu", () => {
    expect(computeRms(new Float32Array([1, -1, 1, -1]))).toBeCloseTo(1);
  });

  it("tišší signál dá menší RMS než hlasitější", () => {
    const quiet = computeRms(new Float32Array(64).fill(0.02));
    const loud = computeRms(new Float32Array(64).fill(0.2));
    expect(quiet).toBeLessThan(loud);
  });
});
