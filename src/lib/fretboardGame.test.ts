import { describe, expect, it } from "vitest";
import { noteAtFret, pickTarget } from "./fretboardGame";

describe("noteAtFret", () => {
  it("returns the open-string pitch class at fret 0", () => {
    // Low E string is MIDI 40 — pitch class 4 (E).
    expect(noteAtFret(40, 0)).toBe(4);
    // A string is MIDI 45 — pitch class 9 (A).
    expect(noteAtFret(45, 0)).toBe(9);
  });

  it("moves up a semitone per fret and wraps at the octave", () => {
    expect(noteAtFret(40, 1)).toBe(5); // F
    expect(noteAtFret(40, 12)).toBe(4); // E again, one octave up
    expect(noteAtFret(40, 8)).toBe(0); // C
  });
});

describe("pickTarget", () => {
  it("maps the random value into the 0..11 range", () => {
    expect(pickTarget(null, () => 0)).toBe(0);
    expect(pickTarget(null, () => 0.99)).toBe(11);
  });

  it("never repeats the previous target", () => {
    // random would land on 3, which equals previous → shifts to 4.
    expect(pickTarget(3, () => 3 / 12)).toBe(4);
    // random lands on 0, different from previous → kept.
    expect(pickTarget(3, () => 0)).toBe(0);
  });
});
