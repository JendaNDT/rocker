import { describe, expect, it } from "vitest";
import { getScale, SCALES, scaleOffset } from "./scales";

const minorPentatonic = getScale("minor-pentatonic");
const major = getScale("major");

describe("getScale", () => {
  it("returns the requested scale", () => {
    expect(getScale("major").id).toBe("major");
  });

  it("falls back to the first scale for an unknown id", () => {
    expect(getScale("nope").id).toBe(SCALES[0].id);
  });
});

describe("scaleOffset", () => {
  it("reports the root as offset 0", () => {
    // A minor pentatonic, root A (pitch class 9).
    expect(scaleOffset(minorPentatonic, 9, 9)).toBe(0);
  });

  it("reports an in-scale note with its degree offset", () => {
    // C is the minor third of an A minor pentatonic scale.
    expect(scaleOffset(minorPentatonic, 9, 0)).toBe(3);
  });

  it("returns null for a note outside the scale", () => {
    // C# is not in A minor pentatonic.
    expect(scaleOffset(minorPentatonic, 9, 1)).toBeNull();
  });

  it("works for the C major scale", () => {
    expect(scaleOffset(major, 0, 0)).toBe(0); // C root
    expect(scaleOffset(major, 0, 11)).toBe(11); // B
    expect(scaleOffset(major, 0, 1)).toBeNull(); // C#
  });
});
