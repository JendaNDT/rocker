import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { Track } from "@/lib/multitrack";

vi.mock("./audioEngine", () => ({
  audioEngine: {
    ensureListening: vi.fn().mockResolvedValue(true),
    getOutputContext: vi.fn(),
    getRecordingStream: vi.fn(),
  },
}));

vi.mock("./recorder", () => ({
  recorder: {
    start: vi.fn().mockReturnValue(true),
    stop: vi.fn(),
  },
}));

import { audioEngine } from "./audioEngine";
import { multitrackEngine } from "./multitrackEngine";
import { recorder } from "./recorder";

const FAKE_STREAM = {} as unknown as MediaStream;
const FAKE_TAKE = {
  blob: new Blob([new Uint8Array([1, 2, 3])]),
  durationSec: 1,
  mimeType: "audio/webm",
};

// Builds a context whose decodeAudioData resolves to a buffer of the
// given duration. createBufferSource captures onended so the test can
// fire "end of playback" deterministically.
type FakeSource = {
  buffer: AudioBuffer | null;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
  connect: (target: unknown) => unknown;
  onended: (() => void) | null;
};

let lastSources: FakeSource[] = [];

function makeContext(): AudioContext {
  return {
    currentTime: 0,
    decodeAudioData: vi.fn(async () =>
      ({ duration: 1, length: 48000, sampleRate: 48000 }) as unknown as AudioBuffer,
    ),
    createBufferSource: () => {
      const node: FakeSource = {
        buffer: null,
        start: vi.fn(),
        stop: vi.fn(),
        connect: (target) => target,
        onended: null,
      };
      lastSources.push(node);
      return node;
    },
    createGain: () => ({
      gain: { value: 0 },
      connect: (target: unknown) => target,
    }),
    destination: {},
  } as unknown as AudioContext;
}

function makeTrack(id: string, gain = 1): Track {
  return {
    id,
    name: `Stopa ${id}`,
    blob: new Blob([new Uint8Array([1])]),
    mimeType: "audio/webm",
    durationSec: 1,
    gain,
    muted: false,
  };
}

beforeEach(() => {
  lastSources = [];
  multitrackEngine.stopPlayback();
  vi.mocked(audioEngine.getOutputContext).mockResolvedValue(makeContext());
  vi.mocked(audioEngine.ensureListening).mockResolvedValue(true);
  vi.mocked(audioEngine.getRecordingStream).mockReturnValue(FAKE_STREAM);
  vi.mocked(recorder.start).mockReturnValue(true);
});

afterEach(() => {
  multitrackEngine.stopPlayback();
  vi.clearAllMocks();
});

describe("MultitrackEngine isBusy", () => {
  it("idle by default", () => {
    expect(multitrackEngine.isBusy()).toBe(false);
  });
});

describe("MultitrackEngine playback", () => {
  it("play() starts a buffer source per track and reports busy", async () => {
    const tracks = [makeTrack("a"), makeTrack("b")];
    const onEnded = vi.fn();
    const ok = await multitrackEngine.play(tracks, onEnded);
    expect(ok).toBe(true);
    expect(multitrackEngine.isBusy()).toBe(true);
    expect(lastSources).toHaveLength(2);
    for (const source of lastSources) {
      expect(source.start).toHaveBeenCalledOnce();
    }
  });

  it("play() refuses an empty track list", async () => {
    const ok = await multitrackEngine.play([], vi.fn());
    expect(ok).toBe(false);
    expect(multitrackEngine.isBusy()).toBe(false);
  });

  it("play() refuses to start while already busy", async () => {
    await multitrackEngine.play([makeTrack("a")], vi.fn());
    const ok = await multitrackEngine.play([makeTrack("b")], vi.fn());
    expect(ok).toBe(false);
  });

  it("stopPlayback() stops every running source and clears busy", async () => {
    await multitrackEngine.play([makeTrack("a"), makeTrack("b")], vi.fn());
    const snapshot = [...lastSources];
    multitrackEngine.stopPlayback();
    expect(multitrackEngine.isBusy()).toBe(false);
    for (const source of snapshot) {
      expect(source.stop).toHaveBeenCalledOnce();
      // onended is cleared so the natural-end callback does NOT fire
      // when stop() is what ended the source.
      expect(source.onended).toBeNull();
    }
  });
});

describe("MultitrackEngine recording lifecycle", () => {
  it("startRecording → stopRecording resolves with the recorder result", async () => {
    vi.mocked(recorder.stop).mockResolvedValue(FAKE_TAKE);

    const started = await multitrackEngine.startRecording([makeTrack("a")]);
    expect(started).toBe(true);
    expect(multitrackEngine.isBusy()).toBe(true);
    expect(vi.mocked(recorder.start)).toHaveBeenCalledWith(FAKE_STREAM);

    const result = await multitrackEngine.stopRecording();
    expect(result).toEqual(FAKE_TAKE);
    expect(multitrackEngine.isBusy()).toBe(false);
  });

  it("startRecording rolls back monitor playback when recorder.start fails", async () => {
    vi.mocked(recorder.start).mockReturnValue(false);
    const ok = await multitrackEngine.startRecording([makeTrack("a")]);
    expect(ok).toBe(false);
    // Playback was rolled back, no sources should be left running.
    expect(multitrackEngine.isBusy()).toBe(false);
  });

  it("startRecording refuses when the input cannot be enabled", async () => {
    vi.mocked(audioEngine.ensureListening).mockResolvedValue(false);
    const ok = await multitrackEngine.startRecording([makeTrack("a")]);
    expect(ok).toBe(false);
    expect(vi.mocked(recorder.start)).not.toHaveBeenCalled();
  });

  it("stopRecording returns null when nothing is recording", async () => {
    const result = await multitrackEngine.stopRecording();
    expect(result).toBeNull();
  });
});
