import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// Mock the audio dependencies before importing loopPedal so the singleton
// captures the mocked modules. Paths must match the relative imports in
// loopPedal.ts ("./audioEngine" and "./recorder").
vi.mock("./audioEngine", () => ({
  audioEngine: {
    ensureListening: vi.fn().mockResolvedValue(true),
    getRecordingStream: vi.fn(),
    getOutputContext: vi.fn(),
  },
}));

vi.mock("./recorder", () => ({
  recorder: {
    start: vi.fn().mockReturnValue(true),
    stop: vi.fn(),
  },
}));

import { audioEngine } from "./audioEngine";
import { loopPedal } from "./loopPedal";
import { recorder } from "./recorder";

// Minimal MediaStream stand-in — loopPedal never reads from it.
const FAKE_STREAM = {} as unknown as MediaStream;

// Recorder.stop() returns this when a take is captured.
const FAKE_TAKE = {
  blob: new Blob([new Uint8Array([1, 2, 3])]),
  durationSec: 1,
  mimeType: "audio/webm",
};

// Builds a context whose decodeAudioData behaves as the test demands.
function makeContext(
  decode: (data: ArrayBuffer) => Promise<AudioBuffer>,
): AudioContext {
  return {
    decodeAudioData: decode,
    destination: {},
    createBufferSource: () => ({
      buffer: null,
      loop: false,
      connect: () => {},
      start: () => {},
      stop: () => {},
      disconnect: () => {},
    }),
    createBuffer: (channels: number, length: number) => ({
      numberOfChannels: channels,
      length,
      sampleRate: 48000,
      getChannelData: () => new Float32Array(length),
      copyToChannel: () => {},
    }),
  } as unknown as AudioContext;
}

function makeAudioBuffer(length = 48000, channels = 1): AudioBuffer {
  return {
    numberOfChannels: channels,
    length,
    sampleRate: 48000,
    getChannelData: () => new Float32Array(length),
  } as unknown as AudioBuffer;
}

beforeEach(() => {
  loopPedal.clear();
  vi.mocked(audioEngine.ensureListening).mockResolvedValue(true);
  vi.mocked(audioEngine.getRecordingStream).mockReturnValue(FAKE_STREAM);
  vi.mocked(recorder.start).mockReturnValue(true);
});

afterEach(() => {
  loopPedal.clear();
});

describe("LoopPedal — stopBase decode error", () => {
  it("when decodeAudioData rejects, pedal ends in idle with zero layers", async () => {
    const failingContext = makeContext(() => Promise.reject(new Error("bad webm")));
    vi.mocked(audioEngine.getOutputContext).mockResolvedValue(failingContext);
    vi.mocked(recorder.stop).mockResolvedValue(FAKE_TAKE);

    expect(await loopPedal.startBase()).toBe(true);
    expect(loopPedal.getState()).toBe("recordingBase");

    await expect(loopPedal.stopBase()).rejects.toThrow(/zpracovat/);

    expect(loopPedal.getState()).toBe("idle");
    expect(loopPedal.getLayerCount()).toBe(0);
  });

  it("happy path: decodeAudioData resolves → pedal goes to looping with 1 layer", async () => {
    const ctx = makeContext(() => Promise.resolve(makeAudioBuffer()));
    vi.mocked(audioEngine.getOutputContext).mockResolvedValue(ctx);
    vi.mocked(recorder.stop).mockResolvedValue(FAKE_TAKE);

    await loopPedal.startBase();
    await loopPedal.stopBase();

    expect(loopPedal.getState()).toBe("looping");
    expect(loopPedal.getLayerCount()).toBe(1);
  });
});

describe("LoopPedal — stopOverdub decode error", () => {
  it("when overlay decode rejects, base loop stays intact in looping", async () => {
    // First capture a base loop successfully.
    const okCtx = makeContext(() => Promise.resolve(makeAudioBuffer()));
    vi.mocked(audioEngine.getOutputContext).mockResolvedValue(okCtx);
    vi.mocked(recorder.stop).mockResolvedValue(FAKE_TAKE);

    await loopPedal.startBase();
    await loopPedal.stopBase();
    expect(loopPedal.getState()).toBe("looping");
    expect(loopPedal.getLayerCount()).toBe(1);

    // Now swap to a context whose decode rejects, simulate an overdub.
    const failingContext = makeContext(() => Promise.reject(new Error("bad")));
    vi.mocked(audioEngine.getOutputContext).mockResolvedValue(failingContext);

    expect(await loopPedal.startOverdub()).toBe(true);
    expect(loopPedal.getState()).toBe("recordingOverdub");

    await expect(loopPedal.stopOverdub()).rejects.toThrow(/Vrstvu/);

    // The base loop survives, layer count unchanged.
    expect(loopPedal.getState()).toBe("looping");
    expect(loopPedal.getLayerCount()).toBe(1);
  });
});
