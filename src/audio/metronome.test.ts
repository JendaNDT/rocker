import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { metronome } from "@/audio/metronome";

// Minimal AudioContext stub. The metronome only reads .currentTime,
// .destination and the audio-node factory methods; every node is a no-op.
function makeFakeContext(): { ctx: AudioContext; bumpTime: (dt: number) => void } {
  let currentTime = 0;
  const noopNode = {
    connect: () => {},
    start: () => {},
    stop: () => {},
    frequency: { value: 0 },
    gain: {
      setValueAtTime: () => {},
      exponentialRampToValueAtTime: () => {},
    },
    type: "sine",
    buffer: null as AudioBuffer | null,
  };
  const make = () => ({ ...noopNode, gain: { ...noopNode.gain } });
  const ctx = {
    get currentTime() {
      return currentTime;
    },
    sampleRate: 44100,
    destination: {},
    createOscillator: () => make(),
    createGain: () => make(),
    createBufferSource: () => make(),
    createBiquadFilter: () => ({ ...make(), frequency: { value: 0 } }),
    createBuffer: (
      _channels: number,
      length: number,
      _sampleRate: number,
    ) => ({
      getChannelData: () => new Float32Array(length),
    }),
  };
  return {
    ctx: ctx as unknown as AudioContext,
    bumpTime: (dt: number) => {
      currentTime += dt;
    },
  };
}

// Hand-rolled rAF queue, same shape as in loopRunner.test.ts — vitest's
// fake timers do not patch requestAnimationFrame.
let pendingFrames: Map<number, FrameRequestCallback>;
function flushFrames(): void {
  const snapshot = Array.from(pendingFrames.entries());
  for (const [id, cb] of snapshot) {
    pendingFrames.delete(id);
    cb(performance.now());
  }
}

beforeEach(() => {
  vi.useFakeTimers();
  pendingFrames = new Map();
  let nextId = 0;
  vi.stubGlobal("requestAnimationFrame", (cb: FrameRequestCallback): number => {
    nextId += 1;
    pendingFrames.set(nextId, cb);
    return nextId;
  });
  vi.stubGlobal("cancelAnimationFrame", (id: number): void => {
    pendingFrames.delete(id);
  });
  // Metronome is a singleton — leave it in a known state.
  metronome.stop();
});

afterEach(() => {
  metronome.stop();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("Metronome lifecycle (LoopRunner-backed)", () => {
  it("starts and stops cleanly", () => {
    const { ctx } = makeFakeContext();
    expect(metronome.getState().isPlaying).toBe(false);
    metronome.start(ctx);
    expect(metronome.getState().isPlaying).toBe(true);
    metronome.stop();
    expect(metronome.getState().isPlaying).toBe(false);
    expect(metronome.getState().currentBeat).toBe(-1);
    expect(metronome.getState().currentBar).toBe(-1);
  });

  it("stop() cancels both the scheduler interval and the rAF loop", () => {
    const { ctx } = makeFakeContext();
    metronome.start(ctx);
    expect(pendingFrames.size).toBeGreaterThan(0);
    metronome.stop();
    expect(pendingFrames.size).toBe(0);
    // The scheduler interval is also gone — bumping the timer fires nothing
    // that touches state.
    const stateBefore = metronome.getState();
    vi.advanceTimersByTime(500);
    flushFrames();
    expect(metronome.getState()).toEqual(stateBefore);
  });

  it("survives 5 start-stop cycles without leaking parallel loops", () => {
    const { ctx, bumpTime } = makeFakeContext();
    for (let i = 0; i < 5; i += 1) {
      metronome.start(ctx);
      bumpTime(0.05);
      // Let the scheduler run a couple of look-ahead windows so it can
      // queue at least one tick.
      vi.advanceTimersByTime(50);
      flushFrames();
      metronome.stop();
    }
    // After the loop, exactly one stop is in effect: nothing is pending.
    expect(metronome.getState().isPlaying).toBe(false);
    expect(pendingFrames.size).toBe(0);
    // And no zombie callback can resuscitate it.
    const listener = vi.fn();
    const unsubscribe = metronome.subscribe(listener);
    vi.advanceTimersByTime(1000);
    flushFrames();
    expect(listener).not.toHaveBeenCalled();
    unsubscribe();
  });

  it("calling start() twice in a row keeps a single loop running", () => {
    const { ctx } = makeFakeContext();
    metronome.start(ctx);
    const firstFrameCount = pendingFrames.size;
    metronome.start(ctx);
    // After the second start, there is still exactly one pending frame —
    // the first run was torn down before the second was scheduled.
    expect(pendingFrames.size).toBe(firstFrameCount);
    metronome.stop();
    expect(pendingFrames.size).toBe(0);
  });
});
