import { afterEach, describe, expect, it, vi } from "vitest";

import {
  DEFAULT_CONFIRMATION_VOLUME,
  clampConfirmationVolume,
  isHapticAvailable,
  playConfirmationChime,
  pulseHaptic,
} from "./confirmationSound";

describe("clampConfirmationVolume", () => {
  it("returns the default for non-numeric input", () => {
    expect(clampConfirmationVolume("loud")).toBe(
      DEFAULT_CONFIRMATION_VOLUME,
    );
    expect(clampConfirmationVolume(null)).toBe(DEFAULT_CONFIRMATION_VOLUME);
    expect(clampConfirmationVolume(undefined)).toBe(
      DEFAULT_CONFIRMATION_VOLUME,
    );
  });

  it("returns the default for NaN/infinity", () => {
    expect(clampConfirmationVolume(Number.NaN)).toBe(
      DEFAULT_CONFIRMATION_VOLUME,
    );
    expect(clampConfirmationVolume(Number.POSITIVE_INFINITY)).toBe(
      DEFAULT_CONFIRMATION_VOLUME,
    );
  });

  it("clamps numbers to [0, 1]", () => {
    expect(clampConfirmationVolume(-2)).toBe(0);
    expect(clampConfirmationVolume(0)).toBe(0);
    expect(clampConfirmationVolume(0.5)).toBe(0.5);
    expect(clampConfirmationVolume(1)).toBe(1);
    expect(clampConfirmationVolume(2.5)).toBe(1);
  });
});

describe("playConfirmationChime", () => {
  it("is a no-op outside a browser (no window)", () => {
    // jsdom (vitest's default) provides window, so we patch it. The point
    // is to prove the guard exists — a missing window mustn't throw.
    const original = globalThis.window;
    delete (globalThis as { window?: unknown }).window;
    try {
      expect(() => playConfirmationChime(0.5)).not.toThrow();
    } finally {
      (globalThis as { window?: unknown }).window = original;
    }
  });
});

describe("haptic helpers", () => {
  const originalVibrate = navigator.vibrate;

  afterEach(() => {
    if (originalVibrate) {
      Object.defineProperty(navigator, "vibrate", {
        configurable: true,
        value: originalVibrate,
      });
    } else {
      // Some jsdom versions don't have vibrate at all — restore that state.
      Object.defineProperty(navigator, "vibrate", {
        configurable: true,
        value: undefined,
      });
    }
  });

  it("reports false when vibrate is unavailable", () => {
    Object.defineProperty(navigator, "vibrate", {
      configurable: true,
      value: undefined,
    });
    expect(isHapticAvailable()).toBe(false);
  });

  it("calls navigator.vibrate when available", () => {
    const vibrate = vi.fn();
    Object.defineProperty(navigator, "vibrate", {
      configurable: true,
      value: vibrate,
    });
    pulseHaptic(50);
    expect(vibrate).toHaveBeenCalledWith(50);
  });

  it("swallows exceptions from navigator.vibrate", () => {
    const vibrate = vi.fn(() => {
      throw new Error("frequency limit");
    });
    Object.defineProperty(navigator, "vibrate", {
      configurable: true,
      value: vibrate,
    });
    expect(() => pulseHaptic()).not.toThrow();
  });
});
