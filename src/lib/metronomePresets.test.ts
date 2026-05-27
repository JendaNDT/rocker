import { describe, expect, it } from "vitest";
import { defaultMetronomeConfig } from "./metronomeConfig";
import {
  createPreset,
  FACTORY_PRESETS,
  isFactoryPreset,
  sanitizePresetList,
} from "./metronomePresets";

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

describe("isFactoryPreset", () => {
  it("recognises the factory- id prefix", () => {
    expect(isFactoryPreset("factory-blues-shuffle")).toBe(true);
    expect(isFactoryPreset("factory-")).toBe(true);
  });

  it("rejects user-shaped ids", () => {
    expect(isFactoryPreset("some-uuid")).toBe(false);
    expect(isFactoryPreset("")).toBe(false);
    expect(isFactoryPreset("Factory-Blues")).toBe(false); // case-sensitive
  });
});

describe("FACTORY_PRESETS", () => {
  it("every factory preset has a factory- id and a name", () => {
    for (const preset of FACTORY_PRESETS) {
      expect(isFactoryPreset(preset.id)).toBe(true);
      expect(preset.name.length).toBeGreaterThan(0);
    }
  });

  it("every factory preset has an accent pattern matching beatsPerBar", () => {
    for (const preset of FACTORY_PRESETS) {
      expect(preset.accentPattern).toHaveLength(preset.beatsPerBar);
    }
  });

  it("factory ids are unique", () => {
    const ids = FACTORY_PRESETS.map((preset) => preset.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("ships the six expected presets", () => {
    const ids = FACTORY_PRESETS.map((preset) => preset.id);
    expect(ids).toEqual([
      "factory-blues-shuffle",
      "factory-metal-gallop",
      "factory-funk-groove",
      "factory-waltz-3-4",
      "factory-jazz-swing",
      "factory-polyrhythm-3-4",
    ]);
  });

  it("Blues shuffle has swing applied", () => {
    const blues = FACTORY_PRESETS.find(
      (preset) => preset.id === "factory-blues-shuffle",
    );
    expect(blues).toBeDefined();
    expect(blues!.swing).toBeGreaterThan(0.5);
  });

  it("Metal gallop runs the ramp trainer", () => {
    const metal = FACTORY_PRESETS.find(
      (preset) => preset.id === "factory-metal-gallop",
    );
    expect(metal).toBeDefined();
    expect(metal!.ramp.enabled).toBe(true);
    expect(metal!.ramp.targetBpm).toBeGreaterThan(metal!.bpm);
  });

  it("Polyrhythm preset enables the secondary layer", () => {
    const poly = FACTORY_PRESETS.find(
      (preset) => preset.id === "factory-polyrhythm-3-4",
    );
    expect(poly).toBeDefined();
    expect(poly!.polyrhythm.enabled).toBe(true);
    expect(poly!.polyrhythm.ticksPerBeat).toBe(3);
  });
});
