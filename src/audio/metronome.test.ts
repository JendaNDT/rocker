import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { metronome } from "@/audio/metronome";
import { type FakeRafQueue, installFakeRaf, makeFakeContext } from "@/audio/testHelpers";

let frames: FakeRafQueue;

beforeEach(() => {
  vi.useFakeTimers();
  frames = installFakeRaf();
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
    expect(frames.size()).toBeGreaterThan(0);
    metronome.stop();
    expect(frames.size()).toBe(0);
    // The scheduler interval is also gone — bumping the timer fires nothing
    // that touches state.
    const stateBefore = metronome.getState();
    vi.advanceTimersByTime(500);
    frames.flushOne();
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
      frames.flushOne();
      metronome.stop();
    }
    // After the loop, exactly one stop is in effect: nothing is pending.
    expect(metronome.getState().isPlaying).toBe(false);
    expect(frames.size()).toBe(0);
    // And no zombie callback can resuscitate it.
    const listener = vi.fn();
    const unsubscribe = metronome.subscribe(listener);
    vi.advanceTimersByTime(1000);
    frames.flushOne();
    expect(listener).not.toHaveBeenCalled();
    unsubscribe();
  });

  it("calling start() twice in a row keeps a single loop running", () => {
    const { ctx } = makeFakeContext();
    metronome.start(ctx);
    const firstFrameCount = frames.size();
    metronome.start(ctx);
    // After the second start, there is still exactly one pending frame —
    // the first run was torn down before the second was scheduled.
    expect(frames.size()).toBe(firstFrameCount);
    metronome.stop();
    expect(frames.size()).toBe(0);
  });
});
