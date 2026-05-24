import { describe, expect, it } from "vitest";
import {
  centsBetween,
  frequencyToNote,
  localizeChordName,
  midiToFrequency,
  midiToLabel,
  midiToLabelIn,
  pitchClassName,
} from "./pitch";

describe("midiToFrequency", () => {
  it("maps A4 (MIDI 69) to 440 Hz", () => {
    expect(midiToFrequency(69)).toBeCloseTo(440, 5);
  });

  it("maps low E (MIDI 40) to ~82.41 Hz", () => {
    expect(midiToFrequency(40)).toBeCloseTo(82.41, 1);
  });

  it("doubles frequency one octave up", () => {
    expect(midiToFrequency(81)).toBeCloseTo(880, 5);
  });
});

describe("midiToLabel", () => {
  it("labels the standard tuning open strings", () => {
    expect(midiToLabel(40)).toBe("E2");
    expect(midiToLabel(45)).toBe("A2");
    expect(midiToLabel(50)).toBe("D3");
    expect(midiToLabel(55)).toBe("G3");
    expect(midiToLabel(59)).toBe("B3");
    expect(midiToLabel(64)).toBe("E4");
  });
});

describe("frequencyToNote", () => {
  it("resolves 440 Hz to A4 with no deviation", () => {
    const note = frequencyToNote(440);
    expect(note?.label).toBe("A4");
    expect(note?.cents).toBe(0);
  });

  it("resolves low E string frequency to E2", () => {
    const note = frequencyToNote(82.41);
    expect(note?.label).toBe("E2");
    expect(Math.abs(note?.cents ?? 99)).toBeLessThanOrEqual(1);
  });

  it("reports a sharp note with positive cents", () => {
    const note = frequencyToNote(448);
    expect(note?.name).toBe("A");
    expect(note?.cents).toBeGreaterThan(0);
  });

  it("reports a flat note with negative cents", () => {
    const note = frequencyToNote(433);
    expect(note?.name).toBe("A");
    expect(note?.cents).toBeLessThan(0);
  });

  it("returns null for invalid input", () => {
    expect(frequencyToNote(0)).toBeNull();
    expect(frequencyToNote(-100)).toBeNull();
    expect(frequencyToNote(Number.NaN)).toBeNull();
  });
});

describe("centsBetween", () => {
  it("is zero for identical frequencies", () => {
    expect(centsBetween(440, 440)).toBe(0);
  });

  it("is 1200 cents for one octave up", () => {
    expect(centsBetween(880, 440)).toBe(1200);
  });

  it("is negative when the played note is flat", () => {
    expect(centsBetween(437, 440)).toBeLessThan(0);
  });
});

describe("pitchClassName", () => {
  it("uses B for pitch class 11 in international notation", () => {
    expect(pitchClassName(11, "english")).toBe("B");
  });

  it("uses H for pitch class 11 in German notation", () => {
    expect(pitchClassName(11, "german")).toBe("H");
  });

  it("uses B for pitch class 10 in German notation", () => {
    expect(pitchClassName(10, "german")).toBe("B");
    expect(pitchClassName(10, "english")).toBe("A#");
  });

  it("leaves other notes unchanged", () => {
    expect(pitchClassName(0, "german")).toBe("C");
    expect(pitchClassName(4, "german")).toBe("E");
  });
});

describe("midiToLabelIn", () => {
  it("labels B4 as H4 in German notation", () => {
    expect(midiToLabelIn(71, "english")).toBe("B4");
    expect(midiToLabelIn(71, "german")).toBe("H4");
  });

  it("labels A#4 as B4 in German notation", () => {
    expect(midiToLabelIn(70, "german")).toBe("B4");
  });
});

describe("localizeChordName", () => {
  it("turns B into H and Bb into B in German notation", () => {
    expect(localizeChordName("B", "german")).toBe("H");
    expect(localizeChordName("Bb", "german")).toBe("B");
    expect(localizeChordName("Bm", "german")).toBe("Hm");
    expect(localizeChordName("B5", "german")).toBe("H5");
  });

  it("leaves chord names unchanged in international notation", () => {
    expect(localizeChordName("B", "english")).toBe("B");
    expect(localizeChordName("Bb", "english")).toBe("Bb");
  });

  it("leaves non-B chords unchanged", () => {
    expect(localizeChordName("C", "german")).toBe("C");
    expect(localizeChordName("Am", "german")).toBe("Am");
  });
});
