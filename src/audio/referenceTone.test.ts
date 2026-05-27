import { describe, expect, it } from "vitest";

import {
  DEFAULT_REFERENCE_TONE_VOLUME,
  clampReferenceVolume,
  isPlayableFrequency,
} from "./referenceTone";

describe("clampReferenceVolume", () => {
  it("returns the default for non-numeric input", () => {
    expect(clampReferenceVolume("loud")).toBe(DEFAULT_REFERENCE_TONE_VOLUME);
    expect(clampReferenceVolume(null)).toBe(DEFAULT_REFERENCE_TONE_VOLUME);
    expect(clampReferenceVolume(undefined)).toBe(
      DEFAULT_REFERENCE_TONE_VOLUME,
    );
    expect(clampReferenceVolume({})).toBe(DEFAULT_REFERENCE_TONE_VOLUME);
  });

  it("returns the default for NaN and infinity", () => {
    expect(clampReferenceVolume(Number.NaN)).toBe(
      DEFAULT_REFERENCE_TONE_VOLUME,
    );
    expect(clampReferenceVolume(Number.POSITIVE_INFINITY)).toBe(
      DEFAULT_REFERENCE_TONE_VOLUME,
    );
    expect(clampReferenceVolume(Number.NEGATIVE_INFINITY)).toBe(
      DEFAULT_REFERENCE_TONE_VOLUME,
    );
  });

  it("clamps numbers to [0, 1]", () => {
    expect(clampReferenceVolume(-1)).toBe(0);
    expect(clampReferenceVolume(0)).toBe(0);
    expect(clampReferenceVolume(0.42)).toBe(0.42);
    expect(clampReferenceVolume(1)).toBe(1);
    expect(clampReferenceVolume(5)).toBe(1);
  });
});

describe("isPlayableFrequency", () => {
  it("rejects non-finite or non-positive input", () => {
    expect(isPlayableFrequency(Number.NaN)).toBe(false);
    expect(isPlayableFrequency(0)).toBe(false);
    expect(isPlayableFrequency(-100)).toBe(false);
    expect(isPlayableFrequency(Number.POSITIVE_INFINITY)).toBe(false);
  });

  it("rejects frequencies outside the audible guitar / tuner range", () => {
    expect(isPlayableFrequency(5)).toBe(false);
    expect(isPlayableFrequency(19.99)).toBe(false);
    expect(isPlayableFrequency(5000.01)).toBe(false);
    expect(isPlayableFrequency(20_000)).toBe(false);
  });

  it("accepts every standard guitar string in the default tunings", () => {
    // From src/audio/tunings.ts — the lowest standard low E2 up to high E4
    // and a margin on each side that should still pass.
    expect(isPlayableFrequency(82.4)).toBe(true); // E2
    expect(isPlayableFrequency(110)).toBe(true); // A2
    expect(isPlayableFrequency(146.8)).toBe(true); // D3
    expect(isPlayableFrequency(196)).toBe(true); // G3
    expect(isPlayableFrequency(246.9)).toBe(true); // B3
    expect(isPlayableFrequency(329.6)).toBe(true); // E4
    expect(isPlayableFrequency(440)).toBe(true); // A4 reference
  });
});
