import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { audioEngine } from "@/audio/audioEngine";

// A controllable fake MediaStreamTrack: lets the test simulate the
// device-unplugged "ended" event by calling fireEnded().
interface FakeTrack {
  stop: ReturnType<typeof vi.fn>;
  fireEnded: () => void;
  addEventListener: (event: string, cb: () => void) => void;
}

function makeFakeStream(): { stream: MediaStream; track: FakeTrack } {
  let endedHandler: (() => void) | null = null;
  const track: FakeTrack = {
    stop: vi.fn(),
    addEventListener: (event, cb) => {
      if (event === "ended") endedHandler = cb;
    },
    fireEnded: () => {
      endedHandler?.();
    },
  };
  const stream = {
    getAudioTracks: () => [track],
    getTracks: () => [track],
  } as unknown as MediaStream;
  return { stream, track };
}

// Minimal AudioContext that satisfies AudioEngine.start():
//   - state, resume, baseLatency, outputLatency
//   - createMediaStreamSource, createAnalyser, createChannelSplitter,
//     createGain, createMediaStreamDestination
// Every node is a no-op with .connect/.disconnect.
function makeFakeAudioContext(): AudioContext {
  const node = {
    connect: () => node,
    disconnect: () => {},
    fftSize: 0,
    stream: {} as MediaStream,
  };
  return {
    state: "running",
    resume: vi.fn(async () => {}),
    baseLatency: 0.01,
    outputLatency: 0.02,
    createMediaStreamSource: () => node,
    createAnalyser: () => node,
    createChannelSplitter: () => node,
    createGain: () => node,
    createMediaStreamDestination: () => node,
  } as unknown as AudioContext;
}

let streams: ReturnType<typeof makeFakeStream>[] = [];

beforeEach(async () => {
  streams = [];
  // Each getUserMedia call gets a fresh fake stream — tests inspect them
  // individually by reading `streams[i]`.
  vi.stubGlobal("navigator", {
    mediaDevices: {
      getUserMedia: vi.fn(async () => {
        const s = makeFakeStream();
        streams.push(s);
        return s.stream;
      }),
      enumerateDevices: vi.fn(async () => []),
    },
  });
  // window.AudioContext used through getContext(). One context per session.
  // The engine calls `new Ctor()`, so the stub has to be a real function
  // (a vi.fn does not work as a constructor — vitest warns and the call
  // returns undefined).
  const ctx = makeFakeAudioContext();
  function AudioContextCtor(this: unknown) {
    return ctx;
  }
  vi.stubGlobal("window", {
    AudioContext: AudioContextCtor,
  });
  // Reset the singleton to a known state.
  await audioEngine.stop();
});

afterEach(async () => {
  await audioEngine.stop();
  vi.unstubAllGlobals();
});

describe("AudioEngine — start/stop race (generation guard)", () => {
  it("a stale 'ended' from the previous start does not kill a fresh engine", async () => {
    await audioEngine.start();
    expect(audioEngine.getState().status).toBe("running");
    expect(streams).toHaveLength(1);
    const firstStream = streams[0];

    // Simulate the fast device-switch flow: stop the first session, start
    // a second one. The second start opens a new MediaStream and registers
    // a fresh "ended" listener — but the first listener is still attached
    // to firstStream's track in our fake.
    await audioEngine.stop();
    await audioEngine.start();
    expect(audioEngine.getState().status).toBe("running");
    expect(streams).toHaveLength(2);

    // Now fire the stale 'ended' event from the first stream's track. With
    // the generation guard, this is ignored — the engine stays running.
    firstStream.track.fireEnded();
    expect(audioEngine.getState().status).toBe("running");
    expect(audioEngine.getState().error).toBeNull();
  });

  it("a real device unplug on the current stream still surfaces the error", async () => {
    await audioEngine.start();
    expect(audioEngine.getState().status).toBe("running");
    expect(streams).toHaveLength(1);

    // Fire 'ended' on the current track — this is a genuine device-lost
    // event from the active generation, so the engine should react.
    streams[0].track.fireEnded();
    expect(audioEngine.getState().status).toBe("error");
    expect(audioEngine.getState().error).toContain("odpojeno");
  });

  it("a late 'ended' after a manual stop does not raise a spurious error", async () => {
    await audioEngine.start();
    await audioEngine.stop();
    expect(audioEngine.getState().status).toBe("idle");

    // OS sometimes emits 'ended' on a stopped track. The guard in
    // handleDeviceLost ignores it when we are no longer running.
    streams[0].track.fireEnded();
    expect(audioEngine.getState().status).toBe("idle");
    expect(audioEngine.getState().error).toBeNull();
  });
});
