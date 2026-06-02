import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { riffPlayer } from "@/audio/riffPlayer";
import { type FakeRafQueue, installFakeRaf, makeFakeContext } from "@/audio/testHelpers";
import type { MetalRiff } from "@/lib/metalRiffs";

// A two-note one-bar riff is enough to drive the scheduler — the player
// only needs the notes and tuning info, not the surrounding metadata.
const TINY_RIFF: MetalRiff = {
  id: "test-tiny",
  title: "Tiny test riff",
  style: "test",
  focus: "test",
  tuning: "standard",
  bpm: 120,
  difficulty: "beginner",
  notes: [
    { stringIndex: 0, fret: 0, start: 0, duration: 1, technique: "normal" },
    { stringIndex: 0, fret: 0, start: 1, duration: 1, technique: "normal" },
  ],
};

let frames: FakeRafQueue;

beforeEach(() => {
  vi.useFakeTimers();
  frames = installFakeRaf();
  riffPlayer.stop();
  riffPlayer.setRiff(TINY_RIFF);
});

afterEach(() => {
  riffPlayer.stop();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("RiffPlayer lifecycle (LoopRunner-backed)", () => {
  it("starts and stops cleanly", () => {
    const { ctx } = makeFakeContext();
    expect(riffPlayer.getState().isPlaying).toBe(false);
    riffPlayer.start(ctx);
    expect(riffPlayer.getState().isPlaying).toBe(true);
    riffPlayer.stop();
    expect(riffPlayer.getState().isPlaying).toBe(false);
    expect(riffPlayer.getState().positionBeats).toBe(0);
  });

  it("start() is a no-op when no riff is loaded", () => {
    // Construct an isolated state: no riff means start should refuse.
    // The TINY_RIFF set in beforeEach guarantees we can recover; clear it
    // by setting an empty-notes riff and ensuring the player refuses.
    riffPlayer.setRiff({ ...TINY_RIFF, notes: [] });
    const { ctx } = makeFakeContext();
    riffPlayer.start(ctx);
    expect(riffPlayer.getState().isPlaying).toBe(false);
    expect(frames.size()).toBe(0);
  });

  it("stop() cancels both the scheduler interval and the rAF loop", () => {
    const { ctx } = makeFakeContext();
    riffPlayer.start(ctx);
    expect(frames.size()).toBeGreaterThan(0);
    riffPlayer.stop();
    expect(frames.size()).toBe(0);
    const stateBefore = riffPlayer.getState();
    vi.advanceTimersByTime(500);
    frames.flushOne();
    expect(riffPlayer.getState()).toEqual(stateBefore);
  });

  it("survives 5 start-stop cycles without leaking parallel loops", () => {
    const { ctx, bumpTime } = makeFakeContext();
    for (let i = 0; i < 5; i += 1) {
      riffPlayer.start(ctx);
      bumpTime(0.05);
      vi.advanceTimersByTime(50);
      frames.flushOne();
      riffPlayer.stop();
    }
    expect(riffPlayer.getState().isPlaying).toBe(false);
    expect(frames.size()).toBe(0);
    const listener = vi.fn();
    const unsubscribe = riffPlayer.subscribe(listener);
    vi.advanceTimersByTime(1000);
    frames.flushOne();
    expect(listener).not.toHaveBeenCalled();
    unsubscribe();
  });

  it("calling start() twice in a row keeps a single loop running", () => {
    const { ctx } = makeFakeContext();
    riffPlayer.start(ctx);
    const firstFrameCount = frames.size();
    riffPlayer.start(ctx);
    expect(frames.size()).toBe(firstFrameCount);
    riffPlayer.stop();
    expect(frames.size()).toBe(0);
  });
});
