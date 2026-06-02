// Test helpers — only imported from *.test.ts. Not part of the runtime.
//
// Provides a minimal AudioContext stub and a manual requestAnimationFrame
// queue so tests can drive audio-singleton lifecycles deterministically.

import { vi } from "vitest";

/** A minimal AudioContext that satisfies every node-factory the players touch. */
export interface FakeAudioContext {
  ctx: AudioContext;
  bumpTime: (deltaSeconds: number) => void;
}

export function makeFakeContext(initialTime = 0): FakeAudioContext {
  let currentTime = initialTime;
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
    bumpTime: (dt) => {
      currentTime += dt;
    },
  };
}

/** Hand-rolled rAF queue — vitest fake timers don't patch rAF. */
export interface FakeRafQueue {
  size: () => number;
  /** Fires the frames pending at the moment of the call. */
  flushOne: () => void;
}

export function installFakeRaf(): FakeRafQueue {
  const pending = new Map<number, FrameRequestCallback>();
  let nextId = 0;
  vi.stubGlobal("requestAnimationFrame", (cb: FrameRequestCallback): number => {
    nextId += 1;
    pending.set(nextId, cb);
    return nextId;
  });
  vi.stubGlobal("cancelAnimationFrame", (id: number): void => {
    pending.delete(id);
  });
  return {
    size: () => pending.size,
    flushOne: () => {
      const snapshot = Array.from(pending.entries());
      for (const [id, cb] of snapshot) {
        pending.delete(id);
        cb(performance.now());
      }
    },
  };
}
