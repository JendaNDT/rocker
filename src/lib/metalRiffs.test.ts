import { describe, expect, it } from "vitest";
import {
  METAL_RIFFS,
  type MetalRiff,
  TUNING_OPEN_MIDI,
  getMetalRiff,
  riffDurationBeats,
  riffNoteMidi,
  validateRiff,
} from "./metalRiffs";

describe("metal riff library", () => {
  it("has unique ids", () => {
    const ids = METAL_RIFFS.map((r) => r.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("has a sizeable library", () => {
    expect(METAL_RIFFS.length).toBeGreaterThanOrEqual(12);
  });

  it("every riff passes validation", () => {
    for (const riff of METAL_RIFFS) {
      expect(validateRiff(riff), riff.id).toEqual([]);
    }
  });

  it("every riff uses a known tuning", () => {
    for (const riff of METAL_RIFFS) {
      expect(TUNING_OPEN_MIDI[riff.tuning], riff.id).toBeDefined();
    }
  });

  it("every riff has a style and a focus", () => {
    for (const riff of METAL_RIFFS) {
      expect(riff.style, riff.id).toBeTruthy();
      expect(riff.focus, riff.id).toBeTruthy();
    }
  });
});

describe("getMetalRiff", () => {
  it("returns a riff by id", () => {
    expect(getMetalRiff("smoke-on-the-water")?.artist).toBe("Deep Purple");
  });

  it("returns undefined for an unknown id", () => {
    expect(getMetalRiff("nope")).toBeUndefined();
  });
});

describe("validateRiff", () => {
  it("reports an out-of-range string index", () => {
    const riff: MetalRiff = {
      id: "x",
      title: "x",
      style: "Heavy metal",
      focus: "Palm mute",
      tuning: "standard",
      bpm: 100,
      difficulty: "beginner",
      notes: [
        { stringIndex: 9, fret: 0, start: 0, duration: 1, technique: "normal" },
      ],
    };
    expect(validateRiff(riff).length).toBeGreaterThan(0);
  });

  it("reports an unknown tuning", () => {
    const riff: MetalRiff = {
      id: "x",
      title: "x",
      style: "Heavy metal",
      focus: "Palm mute",
      tuning: "weird",
      bpm: 100,
      difficulty: "beginner",
      notes: [
        { stringIndex: 0, fret: 0, start: 0, duration: 1, technique: "normal" },
      ],
    };
    expect(validateRiff(riff).some((p) => p.includes("ladění"))).toBe(true);
  });

  it("reports a missing style", () => {
    const riff: MetalRiff = {
      id: "x",
      title: "x",
      style: "",
      focus: "Palm mute",
      tuning: "standard",
      bpm: 100,
      difficulty: "beginner",
      notes: [
        { stringIndex: 0, fret: 0, start: 0, duration: 1, technique: "normal" },
      ],
    };
    expect(validateRiff(riff).some((p) => p.includes("styl"))).toBe(true);
  });
});

describe("riffNoteMidi", () => {
  it("computes the MIDI note of a fretted note in standard tuning", () => {
    const riff = getMetalRiff("smoke-on-the-water");
    // D string (index 2) open is MIDI 50; fret 3 -> 53.
    expect(
      riff &&
        riffNoteMidi(riff, {
          stringIndex: 2,
          fret: 3,
          start: 0,
          duration: 1,
          technique: "normal",
        }),
    ).toBe(53);
  });

  it("uses the riff's tuning for the open-string base", () => {
    const riff = getMetalRiff("drop-d-crusher");
    // Drop D: low string (index 0) open is MIDI 38.
    expect(
      riff &&
        riffNoteMidi(riff, {
          stringIndex: 0,
          fret: 0,
          start: 0,
          duration: 1,
          technique: "palm-mute",
        }),
    ).toBe(38);
  });
});

describe("riffDurationBeats", () => {
  it("returns the end of the last-finishing note", () => {
    const riff: MetalRiff = {
      id: "x",
      title: "x",
      style: "Heavy metal",
      focus: "Palm mute",
      tuning: "standard",
      bpm: 100,
      difficulty: "beginner",
      notes: [
        { stringIndex: 0, fret: 0, start: 0, duration: 1, technique: "normal" },
        { stringIndex: 0, fret: 0, start: 2, duration: 2, technique: "normal" },
      ],
    };
    expect(riffDurationBeats(riff)).toBe(4);
  });
});
