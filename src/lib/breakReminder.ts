// Break-reminder logic — pure, no DOM. Unit-tested in breakReminder.test.ts.
// Part of Bod 13: after a long unbroken practice stretch the app gently
// suggests a rest.

/** Continuous practice after which a break is suggested. */
export const BREAK_LIMIT_MS = 45 * 60 * 1000;

/** A pause from practice at least this long counts as a real break. */
export const BREAK_GAP_MS = 5 * 60 * 1000;

export interface BreakState {
  /** Continuous practice time accumulated since the last break, in ms. */
  practicedMs: number;
  /** Time spent not practicing since the last practice, in ms. */
  idleMs: number;
  /** Whether the reminder has already fired for the current stretch. */
  reminded: boolean;
}

export const initialBreakState: BreakState = {
  practicedMs: 0,
  idleMs: 0,
  reminded: false,
};

export interface BreakAdvance {
  state: BreakState;
  /** True only on the tick where the limit is first reached. */
  remind: boolean;
}

/**
 * Advances the break state by one tick. While practicing, practice time
 * accumulates; once it crosses BREAK_LIMIT_MS the reminder fires exactly
 * once. While not practicing, a gap of BREAK_GAP_MS or more resets the
 * stretch (a real break was taken).
 */
export function advanceBreak(
  state: BreakState,
  deltaMs: number,
  isPracticing: boolean,
): BreakAdvance {
  if (!Number.isFinite(deltaMs) || deltaMs <= 0) {
    return { state, remind: false };
  }

  if (isPracticing) {
    const practicedMs = state.practicedMs + deltaMs;
    const remind = !state.reminded && practicedMs >= BREAK_LIMIT_MS;
    return {
      state: {
        practicedMs,
        idleMs: 0,
        reminded: state.reminded || remind,
      },
      remind,
    };
  }

  const idleMs = state.idleMs + deltaMs;
  if (idleMs >= BREAK_GAP_MS) {
    return { state: initialBreakState, remind: false };
  }
  return { state: { ...state, idleMs }, remind: false };
}
