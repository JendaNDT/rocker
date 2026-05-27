import { describe, expect, it } from "vitest";

import { resolveAllTunings, resolveTuning } from "./resolveTuning";
import { TUNINGS } from "@/audio/tunings";
import type { CustomTuning } from "@/stores/customTuningsStore";

const customs: CustomTuning[] = [
  {
    id: "custom-aaa",
    label: "Open A custom",
    strings: [
      { note: "E2", midi: 40 },
      { note: "A2", midi: 45 },
      { note: "E3", midi: 52 },
      { note: "A3", midi: 57 },
      { note: "C#4", midi: 61 },
      { note: "E4", midi: 64 },
    ],
  },
];

describe("resolveTuning", () => {
  it("returns the factory entry for a known factory id", () => {
    const tuning = resolveTuning("drop-d", customs);
    expect(tuning.id).toBe("drop-d");
  });

  it("returns the custom entry for a known custom id", () => {
    const tuning = resolveTuning("custom-aaa", customs);
    expect(tuning.label).toBe("Open A custom");
    expect(tuning.strings).toHaveLength(6);
  });

  it("falls back to the default for an unknown factory id", () => {
    expect(resolveTuning("does-not-exist", customs).id).toBe("standard");
  });

  it("falls back to the default for a deleted custom id", () => {
    // "custom-…" but not in the customs array.
    expect(resolveTuning("custom-deleted", customs).id).toBe("standard");
  });

  it("works with an empty customs list", () => {
    expect(resolveTuning("standard", []).id).toBe("standard");
    expect(resolveTuning("custom-anything", []).id).toBe("standard");
  });
});

describe("resolveAllTunings", () => {
  it("concatenates factory then custom", () => {
    const all = resolveAllTunings(customs);
    expect(all.slice(0, TUNINGS.length).map((t) => t.id)).toEqual(
      TUNINGS.map((t) => t.id),
    );
    expect(all.at(-1)?.id).toBe("custom-aaa");
  });

  it("returns just the factory list when customs is empty", () => {
    expect(resolveAllTunings([])).toHaveLength(TUNINGS.length);
  });
});
