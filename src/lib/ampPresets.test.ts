import { describe, expect, it } from "vitest";
import { AMP_PRESETS, DEFAULT_AMP_PRESET } from "./ampPresets";

describe("AMP_PRESETS", () => {
  it("offers presets, each with a unique id and a name", () => {
    expect(AMP_PRESETS.length).toBeGreaterThan(0);
    const ids = AMP_PRESETS.map((preset) => preset.id);
    expect(new Set(ids).size).toBe(ids.length);
    for (const preset of AMP_PRESETS) {
      expect(preset.name.trim().length).toBeGreaterThan(0);
    }
  });

  it("keeps every preset's values within the expected ranges", () => {
    for (const preset of AMP_PRESETS) {
      const { drive, bass, mid, treble, delay, level } = preset.settings;
      for (const value of [drive, delay, level]) {
        expect(value).toBeGreaterThanOrEqual(0);
        expect(value).toBeLessThanOrEqual(1);
      }
      for (const value of [bass, mid, treble]) {
        expect(value).toBeGreaterThanOrEqual(-1);
        expect(value).toBeLessThanOrEqual(1);
      }
    }
  });

  it("uses one of the presets as the default", () => {
    expect(AMP_PRESETS).toContain(DEFAULT_AMP_PRESET);
  });
});
