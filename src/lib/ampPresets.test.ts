import { describe, expect, it } from "vitest";
import {
  AMP_PRESETS,
  DEFAULT_AMP_PRESET,
  sanitizeAmpSettings,
  sanitizeUserPresets,
} from "./ampPresets";

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
      const { compression, drive, bass, mid, treble, delay, level } =
        preset.settings;
      for (const value of [compression, drive, delay, level]) {
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

describe("sanitizeAmpSettings", () => {
  it("keeps a valid settings object", () => {
    const settings = {
      compression: 0.25,
      drive: 0.5,
      bass: -0.3,
      mid: 0.2,
      treble: 0.1,
      delay: 0.4,
      level: 0.6,
    };
    expect(sanitizeAmpSettings(settings)).toEqual(settings);
  });

  it("falls back to defaults for missing or invalid fields", () => {
    const result = sanitizeAmpSettings({ drive: 0.8, mid: "loud" });
    expect(result.drive).toBe(0.8);
    expect(result.mid).toBe(DEFAULT_AMP_PRESET.settings.mid);
    expect(result.level).toBe(DEFAULT_AMP_PRESET.settings.level);
  });

  it("clamps out-of-range numbers into their range", () => {
    const result = sanitizeAmpSettings({ drive: 5, bass: -9, treble: 2 });
    expect(result.drive).toBe(1);
    expect(result.bass).toBe(-1);
    expect(result.treble).toBe(1);
  });

  it("returns all defaults for non-object input", () => {
    expect(sanitizeAmpSettings(null)).toEqual(DEFAULT_AMP_PRESET.settings);
    expect(sanitizeAmpSettings("nonsense")).toEqual(
      DEFAULT_AMP_PRESET.settings,
    );
  });
});

describe("sanitizeUserPresets", () => {
  it("keeps valid presets and drops malformed ones", () => {
    const result = sanitizeUserPresets([
      { id: "user-1", name: "Můj zvuk", settings: { drive: 0.7 } },
      { id: "", name: "Bez id", settings: {} },
      { id: "user-2", name: "   ", settings: {} },
      "garbage",
      { id: "user-3", name: "Druhý" },
    ]);
    expect(result.map((preset) => preset.id)).toEqual(["user-1", "user-3"]);
    expect(result[0].settings.drive).toBe(0.7);
    expect(result[0].settings.level).toBe(DEFAULT_AMP_PRESET.settings.level);
  });

  it("returns an empty array for non-array input", () => {
    expect(sanitizeUserPresets(null)).toEqual([]);
    expect(sanitizeUserPresets({ presets: [] })).toEqual([]);
  });
});
