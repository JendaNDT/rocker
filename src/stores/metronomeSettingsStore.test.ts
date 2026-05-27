import { describe, expect, it } from "vitest";
import { sanitizeVisualMode } from "./metronomeSettingsStore";

describe("sanitizeVisualMode", () => {
  it("accepts the two valid modes", () => {
    expect(sanitizeVisualMode("dots")).toBe("dots");
    expect(sanitizeVisualMode("pendulum")).toBe("pendulum");
  });

  it("falls back to dots for unknown strings", () => {
    expect(sanitizeVisualMode("bogus")).toBe("dots");
    expect(sanitizeVisualMode("")).toBe("dots");
  });

  it("falls back to dots for non-string input", () => {
    expect(sanitizeVisualMode(undefined)).toBe("dots");
    expect(sanitizeVisualMode(null)).toBe("dots");
    expect(sanitizeVisualMode(42)).toBe("dots");
    expect(sanitizeVisualMode({})).toBe("dots");
  });
});
