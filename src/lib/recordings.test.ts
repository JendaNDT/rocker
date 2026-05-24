import { describe, expect, it } from "vitest";
import {
  type Marker,
  type Recording,
  extensionForMimeType,
  formatDuration,
  recordingFileName,
  sanitizeMarker,
  sanitizeRecording,
  sortMarkers,
  toMeta,
  withMarker,
  withoutMarker,
} from "./recordings";

function marker(id: string, timeSec: number): Marker {
  return { id, timeSec, label: `Marker ${id}` };
}

function sampleRecording(): Recording {
  return {
    id: "rec-1",
    name: "Pokus 1",
    createdAt: 1_700_000_000_000,
    durationSec: 42,
    mimeType: "audio/webm",
    markers: [marker("a", 5)],
    blob: new Blob(["audio"], { type: "audio/webm" }),
  };
}

describe("formatDuration", () => {
  it("formats minutes and seconds", () => {
    expect(formatDuration(83)).toBe("1:23");
  });

  it("zero-pads seconds", () => {
    expect(formatDuration(65)).toBe("1:05");
  });

  it("handles zero and invalid input", () => {
    expect(formatDuration(0)).toBe("0:00");
    expect(formatDuration(-4)).toBe("0:00");
    expect(formatDuration(Number.NaN)).toBe("0:00");
  });
});

describe("sortMarkers", () => {
  it("sorts by time ascending without mutating the input", () => {
    const input = [marker("a", 9), marker("b", 2), marker("c", 5)];
    const sorted = sortMarkers(input);
    expect(sorted.map((m) => m.id)).toEqual(["b", "c", "a"]);
    expect(input[0].id).toBe("a");
  });
});

describe("withMarker", () => {
  it("adds a marker and keeps the list sorted", () => {
    const result = withMarker([marker("a", 10)], marker("b", 4));
    expect(result.map((m) => m.id)).toEqual(["b", "a"]);
  });
});

describe("withoutMarker", () => {
  it("removes the marker with the given id", () => {
    const result = withoutMarker([marker("a", 1), marker("b", 2)], "a");
    expect(result.map((m) => m.id)).toEqual(["b"]);
  });
});

describe("extensionForMimeType", () => {
  it("maps known MIME types to extensions", () => {
    expect(extensionForMimeType("audio/webm")).toBe(".webm");
    expect(extensionForMimeType("audio/webm;codecs=opus")).toBe(".webm");
    expect(extensionForMimeType("audio/ogg;codecs=opus")).toBe(".ogg");
    expect(extensionForMimeType("audio/mp4")).toBe(".m4a");
  });

  it("falls back to .webm for unknown types", () => {
    expect(extensionForMimeType("audio/unknown")).toBe(".webm");
  });
});

describe("recordingFileName", () => {
  it("appends the format extension", () => {
    expect(recordingFileName("Pokus 1", "audio/webm")).toBe("Pokus 1.webm");
  });

  it("replaces characters that are illegal in filenames", () => {
    expect(recordingFileName('a/b:c?d"', "audio/ogg")).toBe("a_b_c_d_.ogg");
  });

  it("falls back to a default name when the name is blank", () => {
    expect(recordingFileName("   ", "audio/webm")).toBe("nahravka.webm");
  });
});

describe("toMeta", () => {
  it("strips the audio blob", () => {
    const meta = toMeta(sampleRecording());
    expect("blob" in meta).toBe(false);
    expect(meta.id).toBe("rec-1");
    expect(meta.durationSec).toBe(42);
  });
});

describe("sanitizeMarker", () => {
  it("accepts a valid marker", () => {
    expect(sanitizeMarker(marker("a", 3))).toEqual(marker("a", 3));
  });

  it("rejects malformed markers", () => {
    expect(sanitizeMarker(null)).toBeNull();
    expect(sanitizeMarker({ id: "a", timeSec: -1, label: "x" })).toBeNull();
    expect(sanitizeMarker({ id: "a", timeSec: 1 })).toBeNull();
    expect(sanitizeMarker({ id: 5, timeSec: 1, label: "x" })).toBeNull();
  });
});

describe("sanitizeRecording", () => {
  it("accepts a valid recording", () => {
    const recording = sampleRecording();
    expect(sanitizeRecording(recording)).toEqual(recording);
  });

  it("rejects a recording without a Blob", () => {
    const broken = { ...sampleRecording(), blob: "not-a-blob" };
    expect(sanitizeRecording(broken)).toBeNull();
  });

  it("rejects a recording with a bad duration", () => {
    const broken = { ...sampleRecording(), durationSec: Number.NaN };
    expect(sanitizeRecording(broken)).toBeNull();
  });

  it("drops corrupt markers and sorts the rest", () => {
    const recording = {
      ...sampleRecording(),
      markers: [marker("late", 9), { id: "bad" }, marker("early", 1)],
    };
    const result = sanitizeRecording(recording);
    expect(result?.markers.map((m) => m.id)).toEqual(["early", "late"]);
  });

  it("rejects a non-object", () => {
    expect(sanitizeRecording(null)).toBeNull();
    expect(sanitizeRecording("nope")).toBeNull();
  });
});
