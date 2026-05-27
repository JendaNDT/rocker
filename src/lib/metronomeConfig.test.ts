import { describe, expect, it } from "vitest";
import {
  CLICK_SOUNDS,
  clamp01,
  clampBeatsPerBar,
  clampSubdivision,
  cycleAccent,
  DEFAULT_GAP,
  DEFAULT_RAMP,
  defaultAccentPattern,
  defaultMetronomeConfig,
  isClickSoundId,
  isMetronomePreset,
  isSilentBar,
  nextRampBpm,
  resizeAccentPattern,
  resizeSoundPattern,
  sanitizeAccentPattern,
  sanitizeGap,
  sanitizeMetronomeConfig,
  sanitizePreset,
  sanitizeRamp,
  sanitizeSoundPattern,
  sanitizeSubdivisionSound,
  TIME_SIGNATURES,
} from "./metronomeConfig";

describe("cycleAccent", () => {
  it("cycles normal -> accent -> muted -> normal", () => {
    expect(cycleAccent("normal")).toBe("accent");
    expect(cycleAccent("accent")).toBe("muted");
    expect(cycleAccent("muted")).toBe("normal");
  });
});

describe("clampBeatsPerBar", () => {
  it("keeps an in-range value", () => {
    expect(clampBeatsPerBar(7)).toBe(7);
  });

  it("clamps below 1 and above 16", () => {
    expect(clampBeatsPerBar(0)).toBe(1);
    expect(clampBeatsPerBar(99)).toBe(16);
  });

  it("falls back to 4 for invalid input", () => {
    expect(clampBeatsPerBar(Number.NaN)).toBe(4);
  });
});

describe("clampSubdivision", () => {
  it("accepts the supported values", () => {
    expect(clampSubdivision(3)).toBe(3);
  });

  it("falls back to 1 for unsupported values", () => {
    expect(clampSubdivision(5)).toBe(1);
    expect(clampSubdivision(0)).toBe(1);
  });
});

describe("clamp01", () => {
  it("keeps an in-range value", () => {
    expect(clamp01(0.5)).toBe(0.5);
  });

  it("clamps outside 0..1", () => {
    expect(clamp01(-1)).toBe(0);
    expect(clamp01(2)).toBe(1);
  });

  it("falls back to 1 for invalid input", () => {
    expect(clamp01(Number.NaN)).toBe(1);
  });
});

describe("defaultAccentPattern", () => {
  it("accents only the first beat", () => {
    expect(defaultAccentPattern(4)).toEqual([
      "accent",
      "normal",
      "normal",
      "normal",
    ]);
  });

  it("matches the requested length", () => {
    expect(defaultAccentPattern(7)).toHaveLength(7);
  });
});

describe("resizeAccentPattern", () => {
  it("keeps existing entries when growing", () => {
    expect(resizeAccentPattern(["accent", "muted"], 4)).toEqual([
      "accent",
      "muted",
      "normal",
      "normal",
    ]);
  });

  it("truncates when shrinking", () => {
    expect(resizeAccentPattern(["accent", "muted", "normal"], 2)).toEqual([
      "accent",
      "muted",
    ]);
  });
});

describe("sanitizeAccentPattern", () => {
  it("replaces invalid entries with defaults", () => {
    expect(sanitizeAccentPattern(["accent", "bogus", 7], 3)).toEqual([
      "accent",
      "normal",
      "normal",
    ]);
  });

  it("returns a default pattern for non-array input", () => {
    expect(sanitizeAccentPattern("nope", 2)).toEqual(["accent", "normal"]);
  });
});

describe("TIME_SIGNATURES", () => {
  it("has a default accent pattern matching its bar length", () => {
    for (const sig of TIME_SIGNATURES) {
      expect(sig.defaultAccents).toHaveLength(sig.beatsPerBar);
      expect(sig.defaultAccents[0]).toBe("accent");
    }
  });
});

describe("isClickSoundId", () => {
  it("accepts every listed sound", () => {
    for (const sound of CLICK_SOUNDS) {
      expect(isClickSoundId(sound.id)).toBe(true);
    }
  });

  it("rejects unknown values", () => {
    expect(isClickSoundId("trumpet")).toBe(false);
    expect(isClickSoundId(42)).toBe(false);
  });
});

describe("sanitizeRamp", () => {
  it("clamps values into range", () => {
    const ramp = sanitizeRamp({
      enabled: true,
      everyBars: 99,
      stepBpm: -3,
      targetBpm: 9999,
    });
    expect(ramp.enabled).toBe(true);
    expect(ramp.everyBars).toBe(16);
    expect(ramp.stepBpm).toBe(1);
    expect(ramp.targetBpm).toBe(240);
  });

  it("returns defaults for garbage input", () => {
    expect(sanitizeRamp("nope")).toEqual(DEFAULT_RAMP);
  });
});

describe("sanitizeGap", () => {
  it("clamps values into range", () => {
    const gap = sanitizeGap({ enabled: true, audibleBars: 0, silentBars: 50 });
    expect(gap.enabled).toBe(true);
    expect(gap.audibleBars).toBe(1);
    expect(gap.silentBars).toBe(16);
  });

  it("returns defaults for garbage input", () => {
    expect(sanitizeGap(null)).toEqual(DEFAULT_GAP);
  });
});

describe("isSilentBar", () => {
  it("is always false when the gap trainer is disabled", () => {
    const gap = { enabled: false, audibleBars: 2, silentBars: 2 };
    expect(isSilentBar(3, gap)).toBe(false);
  });

  it("marks the silent bars of each cycle (4 audible, 1 silent)", () => {
    const gap = { enabled: true, audibleBars: 4, silentBars: 1 };
    expect([0, 1, 2, 3].map((bar) => isSilentBar(bar, gap))).toEqual([
      false,
      false,
      false,
      false,
    ]);
    expect(isSilentBar(4, gap)).toBe(true);
    expect(isSilentBar(5, gap)).toBe(false);
    expect(isSilentBar(9, gap)).toBe(true);
  });

  it("handles a two-and-two cycle", () => {
    const gap = { enabled: true, audibleBars: 2, silentBars: 2 };
    expect([0, 1, 2, 3, 4, 5].map((bar) => isSilentBar(bar, gap))).toEqual([
      false,
      false,
      true,
      true,
      false,
      false,
    ]);
  });
});

describe("nextRampBpm", () => {
  const ramp = { enabled: true, everyBars: 4, stepBpm: 10, targetBpm: 150 };

  it("raises the tempo by one step below the target", () => {
    expect(nextRampBpm(100, ramp)).toBe(110);
  });

  it("caps the raise at the target", () => {
    expect(nextRampBpm(145, ramp)).toBe(150);
  });

  it("leaves the tempo unchanged once the target is reached", () => {
    expect(nextRampBpm(150, ramp)).toBe(150);
    expect(nextRampBpm(160, ramp)).toBe(160);
  });
});

describe("sanitizeMetronomeConfig", () => {
  it("turns garbage into a valid config", () => {
    const config = sanitizeMetronomeConfig("nonsense");
    expect(config).toEqual(defaultMetronomeConfig());
  });

  it("keeps valid fields and clamps the rest", () => {
    const config = sanitizeMetronomeConfig({
      bpm: 130,
      beatsPerBar: 5,
      subdivision: 2,
      sound: "hihat",
      volume: 0.4,
      accentVolume: 5,
    });
    expect(config.bpm).toBe(130);
    expect(config.beatsPerBar).toBe(5);
    expect(config.subdivision).toBe(2);
    expect(config.sound).toBe("hihat");
    expect(config.volume).toBe(0.4);
    expect(config.accentVolume).toBe(1);
    expect(config.accentPattern).toHaveLength(5);
  });
});

describe("isMetronomePreset", () => {
  it("accepts a value with id and name", () => {
    expect(isMetronomePreset({ id: "a", name: "Gallop" })).toBe(true);
  });

  it("rejects a value missing id or name", () => {
    expect(isMetronomePreset({ name: "Gallop" })).toBe(false);
    expect(isMetronomePreset(null)).toBe(false);
  });
});

describe("sanitizePreset", () => {
  it("coerces a valid preset", () => {
    const preset = sanitizePreset({
      id: "p1",
      name: "  Rozcvička  ",
      bpm: 80,
      beatsPerBar: 3,
    });
    expect(preset).not.toBeNull();
    expect(preset?.name).toBe("Rozcvička");
    expect(preset?.bpm).toBe(80);
    expect(preset?.beatsPerBar).toBe(3);
  });

  it("rejects a preset with an empty name", () => {
    expect(sanitizePreset({ id: "p1", name: "   " })).toBeNull();
  });

  it("rejects a non-preset value", () => {
    expect(sanitizePreset({ bpm: 120 })).toBeNull();
  });
});

describe("sanitizeSoundPattern", () => {
  it("returns undefined for missing input", () => {
    expect(sanitizeSoundPattern(undefined, 4)).toBeUndefined();
    expect(sanitizeSoundPattern(null, 4)).toBeUndefined();
  });

  it("returns undefined for non-array input", () => {
    expect(sanitizeSoundPattern("nope", 4)).toBeUndefined();
  });

  it("returns undefined when every entry is null (= no override)", () => {
    expect(sanitizeSoundPattern([null, null, null, null], 4)).toBeUndefined();
  });

  it("keeps valid sound ids and nulls invalid entries", () => {
    expect(
      sanitizeSoundPattern(["wood", "bogus", "hihat", 42], 4),
    ).toEqual(["wood", null, "hihat", null]);
  });

  it("pads shorter input with null", () => {
    expect(sanitizeSoundPattern(["wood"], 3)).toEqual(["wood", null, null]);
  });

  it("truncates longer input", () => {
    expect(
      sanitizeSoundPattern(["wood", "click", "hihat", "beep"], 2),
    ).toEqual(["wood", "click"]);
  });
});

describe("resizeSoundPattern", () => {
  it("keeps existing entries when growing", () => {
    expect(resizeSoundPattern(["wood", "click"], 4)).toEqual([
      "wood",
      "click",
      null,
      null,
    ]);
  });

  it("truncates when shrinking", () => {
    expect(resizeSoundPattern(["wood", "click", "hihat"], 2)).toEqual([
      "wood",
      "click",
    ]);
  });
});

describe("sanitizeSubdivisionSound", () => {
  it("accepts a valid sound id", () => {
    expect(sanitizeSubdivisionSound("hihat")).toBe("hihat");
  });

  it("returns undefined for unknown values", () => {
    expect(sanitizeSubdivisionSound("bogus")).toBeUndefined();
    expect(sanitizeSubdivisionSound(undefined)).toBeUndefined();
    expect(sanitizeSubdivisionSound(null)).toBeUndefined();
  });
});

describe("sanitizeMetronomeConfig — per-beat extensions", () => {
  it("keeps a valid soundPattern", () => {
    const config = sanitizeMetronomeConfig({
      beatsPerBar: 4,
      soundPattern: ["wood", "click", null, "click"],
    });
    expect(config.soundPattern).toEqual(["wood", "click", null, "click"]);
  });

  it("resizes soundPattern to match beatsPerBar", () => {
    const config = sanitizeMetronomeConfig({
      beatsPerBar: 3,
      soundPattern: ["wood", "click", "hihat", "beep"],
    });
    expect(config.soundPattern).toEqual(["wood", "click", "hihat"]);
  });

  it("drops an all-null soundPattern", () => {
    const config = sanitizeMetronomeConfig({
      beatsPerBar: 4,
      soundPattern: [null, null, null, null],
    });
    expect(config.soundPattern).toBeUndefined();
  });

  it("keeps a valid subdivisionSound", () => {
    const config = sanitizeMetronomeConfig({ subdivisionSound: "hihat" });
    expect(config.subdivisionSound).toBe("hihat");
  });

  it("drops an unknown subdivisionSound", () => {
    const config = sanitizeMetronomeConfig({ subdivisionSound: "bogus" });
    expect(config.subdivisionSound).toBeUndefined();
  });

  it("default config has no per-beat overrides", () => {
    const config = defaultMetronomeConfig();
    expect(config.soundPattern).toBeUndefined();
    expect(config.subdivisionSound).toBeUndefined();
  });
});
