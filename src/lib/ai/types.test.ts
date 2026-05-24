import { describe, expect, it } from "vitest";
import { sanitizeChatHistory, sanitizeChatMessage } from "./types";

describe("sanitizeChatMessage", () => {
  it("accepts a well-formed user message", () => {
    expect(sanitizeChatMessage({ role: "user", text: "Ahoj" })).toEqual({
      role: "user",
      text: "Ahoj",
    });
  });

  it("accepts a well-formed model message", () => {
    expect(sanitizeChatMessage({ role: "model", text: "Zdravím" })).toEqual({
      role: "model",
      text: "Zdravím",
    });
  });

  it("rejects an unknown role", () => {
    expect(sanitizeChatMessage({ role: "assistant", text: "x" })).toBeNull();
  });

  it("rejects a missing or non-string text", () => {
    expect(sanitizeChatMessage({ role: "user" })).toBeNull();
    expect(sanitizeChatMessage({ role: "user", text: 42 })).toBeNull();
  });

  it("rejects non-object input", () => {
    expect(sanitizeChatMessage(null)).toBeNull();
    expect(sanitizeChatMessage("nope")).toBeNull();
    expect(sanitizeChatMessage(["user", "x"])).toBeNull();
  });
});

describe("sanitizeChatHistory", () => {
  it("keeps valid messages and drops broken ones", () => {
    const input = [
      { role: "user", text: "první" },
      { role: "bogus", text: "vyhodit" },
      { role: "model", text: "druhá" },
      "garbage",
    ];
    expect(sanitizeChatHistory(input)).toEqual([
      { role: "user", text: "první" },
      { role: "model", text: "druhá" },
    ]);
  });

  it("returns an empty array for non-array input", () => {
    expect(sanitizeChatHistory(null)).toEqual([]);
    expect(sanitizeChatHistory({ role: "user", text: "x" })).toEqual([]);
  });
});
