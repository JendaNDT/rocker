import { describe, expect, it } from "vitest";
import { clampGainOrFull, clampGainOrSilent, clampUnit } from "./math";

describe("clampUnit", () => {
  it("keeps an in-range value", () => {
    expect(clampUnit(0.5, 0)).toBe(0.5);
    expect(clampUnit(0, 1)).toBe(0);
    expect(clampUnit(1, 0)).toBe(1);
  });

  it("clamps below 0 and above 1", () => {
    expect(clampUnit(-1, 0)).toBe(0);
    expect(clampUnit(2, 0)).toBe(1);
  });

  it("uses fallback for non-finite input", () => {
    expect(clampUnit(Number.NaN, 0.7)).toBe(0.7);
    expect(clampUnit(Number.POSITIVE_INFINITY, 0.7)).toBe(0.7);
    expect(clampUnit(Number.NEGATIVE_INFINITY, 0.7)).toBe(0.7);
  });
});

describe("clampGainOrFull", () => {
  it("invalid input falls back to 1 (engine should not silently mute itself)", () => {
    expect(clampGainOrFull(Number.NaN)).toBe(1);
  });

  it("still clamps real values into 0..1", () => {
    expect(clampGainOrFull(-0.5)).toBe(0);
    expect(clampGainOrFull(1.5)).toBe(1);
    expect(clampGainOrFull(0.3)).toBe(0.3);
  });
});

describe("clampGainOrSilent", () => {
  it("invalid input falls back to 0 (per-asset gain should not boost on corruption)", () => {
    expect(clampGainOrSilent(Number.NaN)).toBe(0);
  });

  it("still clamps real values into 0..1", () => {
    expect(clampGainOrSilent(-0.5)).toBe(0);
    expect(clampGainOrSilent(1.5)).toBe(1);
    expect(clampGainOrSilent(0.3)).toBe(0.3);
  });
});
