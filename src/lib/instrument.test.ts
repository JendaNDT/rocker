import { describe, expect, it } from "vitest";
import {
  DEFAULT_PROFILE,
  GAIN_LABELS,
  POSITION_LABELS,
  STYLES,
  recommendTone,
  sanitizeProfile,
  type GainLevel,
  type InstrumentProfile,
  type PickupPosition,
} from "./instrument";

const GAIN_LEVELS: GainLevel[] = ["low", "medium", "high"];
const POSITIONS: PickupPosition[] = ["neck", "middle", "bridge"];

/** A profile with only the pickups given, all else from the default. */
function profileWith(pickups: InstrumentProfile["pickups"]): InstrumentProfile {
  return { ...DEFAULT_PROFILE, pickups };
}

describe("styles", () => {
  it("offers styles with unique ids", () => {
    expect(STYLES.length).toBeGreaterThan(1);
    const ids = STYLES.map((style) => style.id);
    expect(new Set(ids).size).toBe(ids.length);
  });
});

describe("recommendTone", () => {
  it("returns a valid recommendation for every style", () => {
    for (const style of STYLES) {
      const advice = recommendTone(DEFAULT_PROFILE, style.id);
      expect(POSITIONS).toContain(advice.pickup);
      expect(GAIN_LEVELS).toContain(advice.gain);
      expect(advice.eq.length).toBeGreaterThan(0);
      expect(advice.note.length).toBeGreaterThan(0);
    }
  });

  it("uses the bridge humbucker for metal rhythm on the default profile", () => {
    const advice = recommendTone(DEFAULT_PROFILE, "metal-rhythm");
    expect(advice.pickup).toBe("bridge");
    expect(advice.gain).toBe("high");
    expect(advice.note).toContain("sedí");
  });

  it("warns when a single coil is used for a high-gain style", () => {
    const advice = recommendTone(
      profileWith([{ position: "bridge", type: "single" }]),
      "metal-rhythm",
    );
    expect(advice.pickup).toBe("bridge");
    expect(advice.note).toContain("single");
  });

  it("falls back to the nearest pickup when the ideal one is missing", () => {
    const advice = recommendTone(
      profileWith([{ position: "neck", type: "humbucker" }]),
      "metal-rhythm",
    );
    expect(advice.pickup).toBe("neck");
    expect(advice.note).toContain("nejbližší");
  });

  it("reports when the profile has no pickups", () => {
    const advice = recommendTone(profileWith([]), "rock-lead");
    expect(advice.note).toContain("nemá zadané snímače");
  });
});

describe("labels", () => {
  it("has a label for every position and gain level", () => {
    for (const position of POSITIONS) {
      expect(POSITION_LABELS[position].length).toBeGreaterThan(0);
    }
    for (const gain of GAIN_LEVELS) {
      expect(GAIN_LABELS[gain].length).toBeGreaterThan(0);
    }
  });
});

describe("sanitizeProfile", () => {
  it("returns the default profile for non-object input", () => {
    expect(sanitizeProfile(null)).toEqual(DEFAULT_PROFILE);
    expect(sanitizeProfile("nonsense")).toEqual(DEFAULT_PROFILE);
  });

  it("fills missing fields from the default profile", () => {
    const result = sanitizeProfile({ name: "Moje kytara" });
    expect(result.name).toBe("Moje kytara");
    expect(result.tuning).toBe(DEFAULT_PROFILE.tuning);
  });

  it("drops malformed pickups but keeps valid ones", () => {
    const result = sanitizeProfile({
      pickups: [
        { position: "neck", type: "humbucker" },
        { position: "nonsense", type: "single" },
        { position: "bridge", type: "lasergun" },
        { position: "bridge", type: "p90" },
      ],
    });
    expect(result.pickups).toEqual([
      { position: "neck", type: "humbucker" },
      { position: "bridge", type: "p90" },
    ]);
  });

  it("keeps an explicitly empty pickup list", () => {
    expect(sanitizeProfile({ pickups: [] }).pickups).toEqual([]);
  });
});
