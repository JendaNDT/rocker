import { describe, expect, it } from "vitest";
import { defaultMetronomeConfig } from "./metronomeConfig";
import { createPreset, sanitizePresetList } from "./metronomePresets";

describe("createPreset", () => {
  it("copies the configuration and trims the name", () => {
    const config = { ...defaultMetronomeConfig(), bpm: 144, sound: "hihat" as const };
    const preset = createPreset("  Gallop  ", config);
    expect(preset.name).toBe("Gallop");
    expect(preset.bpm).toBe(144);
    expect(preset.sound).toBe("hihat");
    expect(typeof preset.id).toBe("string");
    expect(preset.id.length).toBeGreaterThan(0);
  });

  it("gives each preset a distinct id", () => {
    const config = defaultMetronomeConfig();
    const a = createPreset("A", config);
    const b = createPreset("B", config);
    expect(a.id).not.toBe(b.id);
  });

  it("sanitises an invalid configuration", () => {
    const preset = createPreset("Divný", {
      bpm: 9999,
      beatsPerBar: 99,
    } as never);
    expect(preset.bpm).toBe(240);
    expect(preset.beatsPerBar).toBe(16);
  });
});

describe("sanitizePresetList", () => {
  it("returns an empty list for non-array input", () => {
    expect(sanitizePresetList("nope")).toEqual([]);
    expect(sanitizePresetList(null)).toEqual([]);
  });

  it("keeps valid presets and drops invalid ones", () => {
    const list = sanitizePresetList([
      { id: "a", name: "Valid", bpm: 90 },
      { id: "b", name: "   " },
      { bpm: 120 },
      null,
    ]);
    expect(list).toHaveLength(1);
    expect(list[0].name).toBe("Valid");
    expect(list[0].bpm).toBe(90);
  });
});
