import { describe, expect, it } from "vitest";
import { chordMidiNotes, getChord } from "./chords";

describe("getChord", () => {
  it("returns a chord by id", () => {
    expect(getChord("Am")?.name).toBe("Am");
  });

  it("returns undefined for an unknown id", () => {
    expect(getChord("Zzz")).toBeUndefined();
  });
});

describe("chordMidiNotes", () => {
  it("computes the notes of an open E chord", () => {
    const chord = getChord("E");
    // Frets [0,2,2,1,0,0] over standard tuning [40,45,50,55,59,64].
    expect(chord && chordMidiNotes(chord)).toEqual([40, 47, 52, 56, 59, 64]);
  });

  it("skips muted strings (open A chord)", () => {
    const chord = getChord("A");
    // Frets ["x",0,2,2,2,0] — the low E string is muted.
    expect(chord && chordMidiNotes(chord)).toEqual([45, 52, 57, 61, 64]);
  });

  it("returns only the fretted strings of a power chord", () => {
    const chord = getChord("E5");
    expect(chord && chordMidiNotes(chord)).toEqual([40, 47, 52]);
  });
});
