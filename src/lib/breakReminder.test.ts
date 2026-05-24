import { describe, expect, it } from "vitest";
import {
  BREAK_GAP_MS,
  BREAK_LIMIT_MS,
  type BreakState,
  advanceBreak,
  initialBreakState,
} from "./breakReminder";

describe("advanceBreak — practicing", () => {
  it("accumulates practice time and clears idle", () => {
    const start: BreakState = {
      practicedMs: 1000,
      idleMs: 5000,
      reminded: false,
    };
    const { state } = advanceBreak(start, 2000, true);
    expect(state.practicedMs).toBe(3000);
    expect(state.idleMs).toBe(0);
  });

  it("fires the reminder once when the limit is crossed", () => {
    const first = advanceBreak(initialBreakState, BREAK_LIMIT_MS, true);
    expect(first.remind).toBe(true);
    expect(first.state.reminded).toBe(true);
    const second = advanceBreak(first.state, 1000, true);
    expect(second.remind).toBe(false);
  });

  it("does not fire before the limit", () => {
    const { remind } = advanceBreak(
      initialBreakState,
      BREAK_LIMIT_MS - 1000,
      true,
    );
    expect(remind).toBe(false);
  });
});

describe("advanceBreak — not practicing", () => {
  it("accumulates idle without touching practice time", () => {
    const start: BreakState = {
      practicedMs: 10000,
      idleMs: 0,
      reminded: false,
    };
    const { state } = advanceBreak(start, 1000, false);
    expect(state.practicedMs).toBe(10000);
    expect(state.idleMs).toBe(1000);
  });

  it("resets the stretch after a long enough break", () => {
    const start: BreakState = {
      practicedMs: 999999,
      idleMs: 0,
      reminded: true,
    };
    const { state } = advanceBreak(start, BREAK_GAP_MS, false);
    expect(state).toEqual(initialBreakState);
  });

  it("keeps practice time through a short gap", () => {
    const start: BreakState = {
      practicedMs: 10000,
      idleMs: 0,
      reminded: false,
    };
    const { state } = advanceBreak(start, 1000, false);
    expect(state.practicedMs).toBe(10000);
  });
});

describe("advanceBreak — bad input", () => {
  it("ignores a non-positive delta", () => {
    const { state, remind } = advanceBreak(initialBreakState, 0, true);
    expect(state).toBe(initialBreakState);
    expect(remind).toBe(false);
  });
});
