import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { gallopPlayer } from "@/audio/gallopPlayer";
import { type FakeRafQueue, installFakeRaf, makeFakeContext } from "@/audio/testHelpers";

let frames: FakeRafQueue;

beforeEach(() => {
  vi.useFakeTimers();
  frames = installFakeRaf();
  gallopPlayer.stop();
});

afterEach(() => {
  gallopPlayer.stop();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("GallopPlayer lifecycle (LoopRunner-backed)", () => {
  it("starts and stops cleanly", () => {
    const { ctx } = makeFakeContext();
    expect(gallopPlayer.getState().isPlaying).toBe(false);
    gallopPlayer.start(ctx);
    expect(gallopPlayer.getState().isPlaying).toBe(true);
    gallopPlayer.stop();
    expect(gallopPlayer.getState().isPlaying).toBe(false);
    expect(gallopPlayer.getState().currentOnset).toBe(-1);
  });

  it("stop() cancels both the scheduler interval and the rAF loop", () => {
    const { ctx } = makeFakeContext();
    gallopPlayer.start(ctx);
    expect(frames.size()).toBeGreaterThan(0);
    gallopPlayer.stop();
    expect(frames.size()).toBe(0);
    const stateBefore = gallopPlayer.getState();
    vi.advanceTimersByTime(500);
    frames.flushOne();
    expect(gallopPlayer.getState()).toEqual(stateBefore);
  });

  it("survives 5 start-stop cycles without leaking parallel loops", () => {
    const { ctx, bumpTime } = makeFakeContext();
    for (let i = 0; i < 5; i += 1) {
      gallopPlayer.start(ctx);
      bumpTime(0.05);
      vi.advanceTimersByTime(50);
      frames.flushOne();
      gallopPlayer.stop();
    }
    expect(gallopPlayer.getState().isPlaying).toBe(false);
    expect(frames.size()).toBe(0);
    const listener = vi.fn();
    const unsubscribe = gallopPlayer.subscribe(listener);
    vi.advanceTimersByTime(1000);
    frames.flushOne();
    expect(listener).not.toHaveBeenCalled();
    unsubscribe();
  });

  it("calling start() twice in a row keeps a single loop running", () => {
    const { ctx } = makeFakeContext();
    gallopPlayer.start(ctx);
    const firstFrameCount = frames.size();
    gallopPlayer.start(ctx);
    expect(frames.size()).toBe(firstFrameCount);
    gallopPlayer.stop();
    expect(frames.size()).toBe(0);
  });
});
