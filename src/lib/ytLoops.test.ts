import { describe, expect, it } from "vitest";
import { sanitizeSavedLoops } from "./ytLoops";

const valid = {
  id: "1",
  videoId: "abc12345678",
  label: "Sólo",
  start: 10,
  end: 20,
  rate: 1,
};

describe("sanitizeSavedLoops", () => {
  it("keeps valid loops unchanged", () => {
    expect(sanitizeSavedLoops([valid])).toEqual([valid]);
  });

  it("returns an empty array for non-array input", () => {
    expect(sanitizeSavedLoops(null)).toEqual([]);
    expect(sanitizeSavedLoops(undefined)).toEqual([]);
    expect(sanitizeSavedLoops({})).toEqual([]);
    expect(sanitizeSavedLoops("x")).toEqual([]);
  });

  it("drops entries with missing or wrong-typed fields", () => {
    expect(
      sanitizeSavedLoops([
        valid,
        { ...valid, id: "" }, // empty id
        { ...valid, videoId: 123 }, // wrong type
        { ...valid, start: NaN }, // not finite
        { ...valid, end: "x" }, // wrong type
        { ...valid, rate: undefined }, // missing
        null,
        "garbage",
        42,
      ]),
    ).toEqual([valid]);
  });
});
