import { describe, expect, it } from "vitest";
import {
  MAX_POWER_CHORD_FRET,
  POWER_CHORDS,
  getPowerChord,
  midiToPitchClass,
  powerChordAt,
  powerChordMidiNotes,
} from "./powerChords";

describe("midiToPitchClass", () => {
  it("names the open low E string", () => {
    expect(midiToPitchClass(40)).toBe("E");
  });

  it("names a sharp pitch class", () => {
    expect(midiToPitchClass(42)).toBe("F#");
  });
});

describe("powerChordAt", () => {
  it("builds G5 on the low E string at fret 3", () => {
    const chord = powerChordAt(0, 3);
    expect(chord.name).toBe("G5");
    expect(chord.rootName).toBe("G");
    expect(chord.rootMidi).toBe(43);
    expect(chord.id).toBe("e-3");
  });

  it("builds A5 on the open A string", () => {
    const chord = powerChordAt(1, 0);
    expect(chord.name).toBe("A5");
    expect(chord.rootMidi).toBe(45);
    expect(chord.id).toBe("a-0");
  });
});

describe("powerChordMidiNotes", () => {
  it("returns root, fifth and octave", () => {
    expect(powerChordMidiNotes(powerChordAt(0, 3))).toEqual([43, 50, 55]);
  });
});

describe("POWER_CHORDS", () => {
  it("covers both strings from open to the max fret", () => {
    expect(POWER_CHORDS).toHaveLength((MAX_POWER_CHORD_FRET + 1) * 2);
  });

  it("has unique ids", () => {
    const ids = POWER_CHORDS.map((chord) => chord.id);
    expect(new Set(ids).size).toBe(ids.length);
  });
});

describe("getPowerChord", () => {
  it("looks up a power chord by id", () => {
    expect(getPowerChord("e-3")?.name).toBe("G5");
  });

  it("returns undefined for an unknown id", () => {
    expect(getPowerChord("zzz")).toBeUndefined();
  });
});
