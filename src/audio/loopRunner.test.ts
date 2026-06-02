import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  LOOKAHEAD_MS,
  LoopRunner,
  SCHEDULE_AHEAD_S,
} from "@/audio/loopRunner";

// Minimal AudioContext stand-in. The runner only ever reads `currentTime`,
// so we keep it as a plain object and bump the clock manually in tests.
function fakeContext(currentTime = 0): AudioContext {
  const obj = { currentTime };
  return obj as unknown as AudioContext;
}

// Drive requestAnimationFrame by hand. Vitest's fake timers do not patch
// rAF, so we install our own queue and step through it explicitly. This
// lets a test assert exactly how many frames have rendered.
interface FrameQueue {
  pending: Map<number, FrameRequestCallback>;
  nextId: number;
  /** Fires the currently pending frames (snapshot taken before firing). */
  flushOne: () => void;
  size: () => number;
}

let frames: FrameQueue;

beforeEach(() => {
  vi.useFakeTimers();
  const pending = new Map<number, FrameRequestCallback>();
  let nextId = 0;
  frames = {
    pending,
    nextId,
    flushOne: () => {
      // Snapshot the current set of pending frames and fire them. Frames
      // scheduled during the callback go to the next flush, mirroring the
      // browser's behaviour.
      const snapshot = Array.from(pending.entries());
      for (const [id, cb] of snapshot) {
        pending.delete(id);
        cb(performance.now());
      }
    },
    size: () => pending.size,
  };
  vi.stubGlobal("requestAnimationFrame", (cb: FrameRequestCallback): number => {
    nextId += 1;
    pending.set(nextId, cb);
    return nextId;
  });
  vi.stubGlobal("cancelAnimationFrame", (id: number): void => {
    pending.delete(id);
  });
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("LoopRunner constants", () => {
  it("exposes the look-ahead defaults so all players share one source of truth", () => {
    expect(LOOKAHEAD_MS).toBe(25);
    expect(SCHEDULE_AHEAD_S).toBeCloseTo(0.12);
  });
});

describe("LoopRunner start/stop lifecycle", () => {
  it("is not running before start", () => {
    const runner = new LoopRunner({ schedule: () => {}, draw: () => {} });
    expect(runner.isRunning()).toBe(false);
  });

  it("reports running between start and stop", () => {
    const runner = new LoopRunner({ schedule: () => {}, draw: () => {} });
    runner.start(fakeContext());
    expect(runner.isRunning()).toBe(true);
    runner.stop();
    expect(runner.isRunning()).toBe(false);
  });

  it("fires the schedule callback on every look-ahead interval", () => {
    const schedule = vi.fn();
    const runner = new LoopRunner({ schedule, draw: () => {} });
    runner.start(fakeContext());
    expect(schedule).not.toHaveBeenCalled();
    vi.advanceTimersByTime(LOOKAHEAD_MS);
    expect(schedule).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(LOOKAHEAD_MS * 4);
    expect(schedule).toHaveBeenCalledTimes(5);
    runner.stop();
  });

  it("fires the draw callback once per animation frame", () => {
    const draw = vi.fn();
    const runner = new LoopRunner({ schedule: () => {}, draw });
    runner.start(fakeContext());
    frames.flushOne();
    expect(draw).toHaveBeenCalledTimes(1);
    frames.flushOne();
    expect(draw).toHaveBeenCalledTimes(2);
    runner.stop();
  });

  it("passes the live AudioContext into both callbacks", () => {
    const ctx = fakeContext(42);
    const schedule = vi.fn();
    const draw = vi.fn();
    const runner = new LoopRunner({ schedule, draw });
    runner.start(ctx);
    vi.advanceTimersByTime(LOOKAHEAD_MS);
    frames.flushOne();
    expect(schedule).toHaveBeenCalledWith(ctx);
    expect(draw).toHaveBeenCalledWith(ctx);
    runner.stop();
  });
});

describe("LoopRunner stop() is decisive", () => {
  it("stops firing the scheduler after stop()", () => {
    const schedule = vi.fn();
    const runner = new LoopRunner({ schedule, draw: () => {} });
    runner.start(fakeContext());
    vi.advanceTimersByTime(LOOKAHEAD_MS * 2);
    const callsBefore = schedule.mock.calls.length;
    runner.stop();
    vi.advanceTimersByTime(LOOKAHEAD_MS * 10);
    expect(schedule.mock.calls.length).toBe(callsBefore);
  });

  it("stops firing the draw loop after stop() — no rAF re-arm", () => {
    const draw = vi.fn();
    const runner = new LoopRunner({ schedule: () => {}, draw });
    runner.start(fakeContext());
    frames.flushOne();
    frames.flushOne();
    const callsBefore = draw.mock.calls.length;
    runner.stop();
    // Even if frames continue to fire, the runner does not re-arm.
    frames.flushOne();
    frames.flushOne();
    expect(draw.mock.calls.length).toBe(callsBefore);
    // And there are no pending frames left.
    expect(frames.size()).toBe(0);
  });

  it("clears the AudioContext on stop so a stale tick cannot reach the callback", () => {
    const draw = vi.fn();
    const runner = new LoopRunner({ schedule: () => {}, draw });
    runner.start(fakeContext());
    runner.stop();
    // Even if a stray frame slipped past cancelAnimationFrame (browsers
    // sometimes batch), the running flag and the cleared context would
    // both keep the callback silent.
    expect(runner.isRunning()).toBe(false);
  });

  it("does not re-arm rAF if stop() is called from within the draw callback", () => {
    const runner = new LoopRunner({
      schedule: () => {},
      draw: () => {
        runner.stop();
      },
    });
    runner.start(fakeContext());
    frames.flushOne();
    // The callback called stop(); the runner must not have scheduled another
    // frame after the callback returned.
    expect(frames.size()).toBe(0);
    expect(runner.isRunning()).toBe(false);
  });

  it("stop() is idempotent — calling it twice is fine", () => {
    const runner = new LoopRunner({ schedule: () => {}, draw: () => {} });
    runner.start(fakeContext());
    runner.stop();
    expect(() => runner.stop()).not.toThrow();
    expect(runner.isRunning()).toBe(false);
  });

  it("stop() before any rAF fired still cancels the pending frame", () => {
    const draw = vi.fn();
    const runner = new LoopRunner({ schedule: () => {}, draw });
    runner.start(fakeContext());
    expect(frames.size()).toBe(1);
    runner.stop();
    expect(frames.size()).toBe(0);
    frames.flushOne();
    expect(draw).not.toHaveBeenCalled();
  });
});

describe("LoopRunner start() while running", () => {
  it("calling start() twice does not leave two loops running", () => {
    const schedule = vi.fn();
    const draw = vi.fn();
    const runner = new LoopRunner({ schedule, draw });
    runner.start(fakeContext());
    runner.start(fakeContext()); // would have leaked a second pair previously
    vi.advanceTimersByTime(LOOKAHEAD_MS);
    expect(schedule).toHaveBeenCalledTimes(1);
    frames.flushOne();
    expect(draw).toHaveBeenCalledTimes(1);
    runner.stop();
  });

  it("restarting after stop() uses the new context", () => {
    const draw = vi.fn();
    const ctxA = fakeContext(1);
    const ctxB = fakeContext(99);
    const runner = new LoopRunner({ schedule: () => {}, draw });
    runner.start(ctxA);
    frames.flushOne();
    runner.stop();
    runner.start(ctxB);
    frames.flushOne();
    expect(draw).toHaveBeenLastCalledWith(ctxB);
    runner.stop();
  });
});

describe("LoopRunner custom lookAheadMs", () => {
  it("honours the constructor option", () => {
    const schedule = vi.fn();
    const runner = new LoopRunner(
      { schedule, draw: () => {} },
      { lookAheadMs: 100 },
    );
    runner.start(fakeContext());
    vi.advanceTimersByTime(99);
    expect(schedule).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(schedule).toHaveBeenCalledTimes(1);
    runner.stop();
  });
});
