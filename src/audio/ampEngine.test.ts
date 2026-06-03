import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// Mock the modules ampEngine reaches into BEFORE importing it, so the
// singleton captures the mocks.

vi.mock("./audioEngine", () => ({
  audioEngine: {
    ensureListening: vi.fn().mockResolvedValue(true),
    getInputSource: vi.fn(),
    getOutputContext: vi.fn(),
  },
}));

// AmpChain owns real Web Audio nodes; stub it with a no-op class that
// satisfies the surface ampEngine touches (input.connect, dispose, the
// applyAmpSettings setters, outputAnalyser/recordingStream getters).
vi.mock("./ampChain", () => {
  return {
    AmpChain: class {
      input = { connect: vi.fn(), disconnect: vi.fn() } as unknown;
      outputAnalyser = {} as AnalyserNode;
      recordingStream = {} as MediaStream;
      setCompression() {}
      setDrive() {}
      setDistortionType() {}
      setBass() {}
      setMid() {}
      setTreble() {}
      setCabinet() {}
      setDelayMix() {}
      setReverbMix() {}
      setChorusMix() {}
      setLevel() {}
      dispose = vi.fn();
    },
  };
});

import { ampEngine } from "./ampEngine";
import { audioEngine } from "./audioEngine";

// Bare object that pretends to be an AudioContext / AudioNode for the
// mocked AmpChain — ampEngine only forwards them.
const FAKE_CONTEXT = {} as unknown as AudioContext;
const FAKE_SOURCE = {
  connect: vi.fn(),
  disconnect: vi.fn(),
} as unknown as AudioNode;

beforeEach(async () => {
  await ampEngine.stop();
  vi.mocked(audioEngine.ensureListening).mockResolvedValue(true);
  vi.mocked(audioEngine.getInputSource).mockReturnValue(FAKE_SOURCE);
  vi.mocked(audioEngine.getOutputContext).mockResolvedValue(FAKE_CONTEXT);
});

afterEach(async () => {
  await ampEngine.stop();
  vi.clearAllMocks();
});

describe("AmpEngine lifecycle", () => {
  it("starts and stops cleanly", async () => {
    expect(ampEngine.getState().active).toBe(false);
    await ampEngine.start();
    expect(ampEngine.getState().active).toBe(true);
    expect(ampEngine.getState().status).toBe("running");
    await ampEngine.stop();
    expect(ampEngine.getState().active).toBe(false);
    expect(ampEngine.getState().status).toBe("idle");
  });

  it("start() is a no-op when already running", async () => {
    await ampEngine.start();
    const firstCallCount = vi.mocked(audioEngine.ensureListening).mock.calls.length;
    await ampEngine.start();
    // The second call should bail at the `if (active) return` guard, so
    // ensureListening is NOT re-invoked.
    expect(vi.mocked(audioEngine.ensureListening).mock.calls.length).toBe(
      firstCallCount,
    );
  });

  it("falls into error when the input cannot be enabled", async () => {
    vi.mocked(audioEngine.ensureListening).mockResolvedValue(false);
    await ampEngine.start();
    expect(ampEngine.getState().status).toBe("error");
    expect(ampEngine.getState().active).toBe(false);
    expect(ampEngine.getState().error).toContain("mikrofon");
  });

  it("falls into error when the input source is missing", async () => {
    vi.mocked(audioEngine.getInputSource).mockReturnValue(null);
    await ampEngine.start();
    expect(ampEngine.getState().status).toBe("error");
    expect(ampEngine.getState().active).toBe(false);
  });

  it("stop() is idempotent — calling twice is safe", async () => {
    await ampEngine.start();
    await ampEngine.stop();
    await expect(ampEngine.stop()).resolves.toBeUndefined();
    expect(ampEngine.getState().active).toBe(false);
  });

  it("survives 5 start-stop cycles without leaking state", async () => {
    for (let i = 0; i < 5; i += 1) {
      await ampEngine.start();
      expect(ampEngine.getState().active).toBe(true);
      await ampEngine.stop();
      expect(ampEngine.getState().active).toBe(false);
    }
    // After the loop, no listener gets a stray callback.
    const listener = vi.fn();
    const unsubscribe = ampEngine.subscribe(listener);
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(listener).not.toHaveBeenCalled();
    unsubscribe();
  });
});
