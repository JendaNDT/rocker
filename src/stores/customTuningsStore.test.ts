import { beforeEach, describe, expect, it } from "vitest";

import {
  MAX_CUSTOM_TUNINGS,
  MAX_TUNING_MIDI,
  MIN_TUNING_MIDI,
  STRINGS_PER_TUNING,
  _internal,
  isCustomTuningId,
  useCustomTuningsStore,
} from "./customTuningsStore";

function makeStrings(midis: number[]) {
  return midis.map((midi) => ({ note: `m${midi}`, midi }));
}

const STANDARD_MIDIS = [40, 45, 50, 55, 59, 64];

beforeEach(() => {
  // Reset the persisted store state between tests so they don't leak
  // tunings into each other.
  useCustomTuningsStore.setState({ tunings: [] });
});

describe("isCustomTuningId", () => {
  it("recognises the custom- prefix", () => {
    expect(isCustomTuningId("custom-abc")).toBe(true);
    expect(isCustomTuningId("standard")).toBe(false);
    expect(isCustomTuningId("")).toBe(false);
  });
});

describe("sanitizeList", () => {
  it("returns an empty list for non-array input", () => {
    expect(_internal.sanitizeList(undefined)).toEqual([]);
    expect(_internal.sanitizeList(null)).toEqual([]);
    expect(_internal.sanitizeList("oops")).toEqual([]);
  });

  it("drops entries with wrong string count", () => {
    const got = _internal.sanitizeList([
      {
        id: "custom-a",
        label: "Too few",
        strings: makeStrings([40, 45]),
      },
    ]);
    expect(got).toEqual([]);
  });

  it("drops entries with MIDI out of range", () => {
    const got = _internal.sanitizeList([
      {
        id: "custom-a",
        label: "Too low",
        strings: makeStrings([
          MIN_TUNING_MIDI - 1,
          45,
          50,
          55,
          59,
          64,
        ]),
      },
      {
        id: "custom-b",
        label: "Too high",
        strings: makeStrings([
          40,
          45,
          50,
          55,
          59,
          MAX_TUNING_MIDI + 1,
        ]),
      },
    ]);
    expect(got).toEqual([]);
  });

  it("drops entries without a custom- prefixed id", () => {
    const got = _internal.sanitizeList([
      {
        id: "standard",
        label: "Faked standard",
        strings: makeStrings(STANDARD_MIDIS),
      },
    ]);
    expect(got).toEqual([]);
  });

  it("drops entries with empty labels", () => {
    const got = _internal.sanitizeList([
      {
        id: "custom-a",
        label: "   ",
        strings: makeStrings(STANDARD_MIDIS),
      },
    ]);
    expect(got).toEqual([]);
  });

  it("dedupes ids", () => {
    const got = _internal.sanitizeList([
      {
        id: "custom-dup",
        label: "First",
        strings: makeStrings(STANDARD_MIDIS),
      },
      {
        id: "custom-dup",
        label: "Second",
        strings: makeStrings(STANDARD_MIDIS),
      },
    ]);
    expect(got).toHaveLength(1);
    expect(got[0].label).toBe("First");
  });

  it("trims labels and accepts valid entries", () => {
    const got = _internal.sanitizeList([
      {
        id: "custom-a",
        label: "  Open A  ",
        strings: makeStrings(STANDARD_MIDIS),
      },
    ]);
    expect(got).toEqual([
      {
        id: "custom-a",
        label: "Open A",
        strings: makeStrings(STANDARD_MIDIS),
      },
    ]);
  });

  it("caps the list at MAX_CUSTOM_TUNINGS", () => {
    const entries = Array.from({ length: MAX_CUSTOM_TUNINGS + 5 }, (_, i) => ({
      id: `custom-${i}`,
      label: `Tuning ${i}`,
      strings: makeStrings(STANDARD_MIDIS),
    }));
    const got = _internal.sanitizeList(entries);
    expect(got).toHaveLength(MAX_CUSTOM_TUNINGS);
  });
});

describe("addTuning", () => {
  it("rejects empty labels", () => {
    const id = useCustomTuningsStore
      .getState()
      .addTuning("   ", makeStrings(STANDARD_MIDIS));
    expect(id).toBeNull();
    expect(useCustomTuningsStore.getState().tunings).toHaveLength(0);
  });

  it("rejects strings with the wrong shape", () => {
    const id = useCustomTuningsStore
      .getState()
      .addTuning("Open A", makeStrings([40, 45, 50]));
    expect(id).toBeNull();
  });

  it("returns the new id and persists the entry", () => {
    const id = useCustomTuningsStore
      .getState()
      .addTuning("  Open A  ", makeStrings(STANDARD_MIDIS));
    expect(id).not.toBeNull();
    expect(id?.startsWith("custom-")).toBe(true);
    const stored = useCustomTuningsStore.getState().tunings;
    expect(stored).toHaveLength(1);
    expect(stored[0].label).toBe("Open A");
    expect(stored[0].strings).toHaveLength(STRINGS_PER_TUNING);
  });

  it("refuses to add past MAX_CUSTOM_TUNINGS", () => {
    const state = useCustomTuningsStore.getState();
    for (let i = 0; i < MAX_CUSTOM_TUNINGS; i += 1) {
      state.addTuning(`Tuning ${i}`, makeStrings(STANDARD_MIDIS));
    }
    expect(useCustomTuningsStore.getState().tunings).toHaveLength(
      MAX_CUSTOM_TUNINGS,
    );
    const overflow = useCustomTuningsStore
      .getState()
      .addTuning("Overflow", makeStrings(STANDARD_MIDIS));
    expect(overflow).toBeNull();
    expect(useCustomTuningsStore.getState().tunings).toHaveLength(
      MAX_CUSTOM_TUNINGS,
    );
  });
});

describe("removeTuning", () => {
  it("returns false when nothing matched", () => {
    expect(useCustomTuningsStore.getState().removeTuning("custom-nope")).toBe(
      false,
    );
  });

  it("removes the matching tuning and returns true", () => {
    const id =
      useCustomTuningsStore
        .getState()
        .addTuning("Open A", makeStrings(STANDARD_MIDIS)) ?? "";
    expect(useCustomTuningsStore.getState().tunings).toHaveLength(1);
    expect(useCustomTuningsStore.getState().removeTuning(id)).toBe(true);
    expect(useCustomTuningsStore.getState().tunings).toHaveLength(0);
  });
});
