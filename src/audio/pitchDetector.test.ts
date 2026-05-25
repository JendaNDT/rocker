import { describe, expect, it } from "vitest";
import {
  NOISE_GATE_MAX,
  computeRms,
  recommendedNoiseGate,
} from "./pitchDetector";

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

describe("recommendedNoiseGate", () => {
  it("dá při tichu jen bezpečnostní rezervu", () => {
    expect(recommendedNoiseGate(0)).toBeCloseTo(0.03);
  });

  it("vrátí rezervu i pro vadný vstup", () => {
    expect(recommendedNoiseGate(Number.NaN)).toBeCloseTo(0.03);
    expect(recommendedNoiseGate(-1)).toBeCloseTo(0.03);
  });

  it("nastaví práh nad naměřenou špičku hluku", () => {
    // 0.04 RMS × měřítko 3 = 0.12, + rezerva 0.03 = 0.15
    expect(recommendedNoiseGate(0.04)).toBeCloseTo(0.15);
  });

  it("ořízne hlasité pozadí na maximum brány", () => {
    expect(recommendedNoiseGate(5)).toBe(NOISE_GATE_MAX);
  });

  it("zaokrouhlí na krok posuvníku (0.01)", () => {
    expect(recommendedNoiseGate(0.017)).toBe(0.08);
  });
});
