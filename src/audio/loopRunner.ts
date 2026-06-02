// LoopRunner — architecture layer 5 (audio).
//
// Shared "two clocks" runner for audio players. Three players (metronome,
// riffPlayer, gallopPlayer) all needed the same pair of loops:
//
//   1. A coarse setInterval scheduler that wakes up every LOOKAHEAD_MS and
//      queues events slightly ahead on the AudioContext clock (this is the
//      "schedule" callback — pattern from Chris Wilson's "A Tale of Two
//      Clocks").
//
//   2. A requestAnimationFrame loop that updates UI state from whichever
//      events are now due on the audio clock (this is the "draw" callback).
//
// The three players had hand-written copies of this pattern, which made the
// stop() contract fragile: each stop() cancelled the timer and rAF but left
// `this.context` non-null, and the rAF body unconditionally re-scheduled
// itself. Single-threaded JS keeps the current implementations from
// double-running, but the pattern is one event-listener-driven start away
// from spawning a parallel loop.
//
// LoopRunner makes the contract explicit:
//   - `start(context)` is idempotent: calling it while running first calls
//     stop() so the previous loop is fully torn down.
//   - `stop()` flips a `running` flag, cancels the timer and rAF, and clears
//     the context. The next tick of either callback returns immediately.
//   - The draw callback re-schedules itself only while `running` is true,
//     so a stop() between rAF dispatch and rAF fire cannot resuscitate the
//     loop.
//
// The runner owns the timer, rAF id, context and the running flag. Player
// classes only own their own event queues and audio-graph state, and feed
// the runner two callbacks.

export interface LoopRunnerCallbacks {
  /**
   * Called inside the setInterval scheduler. Use to push events ahead on
   * the AudioContext clock (lookahead window of about SCHEDULE_AHEAD_S).
   * Receives the live AudioContext so callers do not need to keep their
   * own copy.
   */
  schedule: (context: AudioContext) => void;
  /**
   * Called inside requestAnimationFrame. Use to drain queued events that
   * are now due on the audio clock and update UI state.
   */
  draw: (context: AudioContext) => void;
}

export interface LoopRunnerOptions {
  /** How often the scheduler wakes up (ms). Default 25. */
  lookAheadMs?: number;
}

/**
 * Default look-ahead window for the scheduler, in milliseconds. The scheduler
 * wakes up this often to top up the audio queue.
 */
export const LOOKAHEAD_MS = 25;

/**
 * Default schedule-ahead window for the scheduler, in seconds. Callers should
 * schedule any event whose time falls within `audioContext.currentTime + this`
 * during a scheduler tick. Exposed so all players share one source of truth.
 */
export const SCHEDULE_AHEAD_S = 0.12;

export class LoopRunner {
  private context: AudioContext | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;
  private rafId = 0;
  private running = false;
  private readonly lookAheadMs: number;

  constructor(
    private readonly callbacks: LoopRunnerCallbacks,
    options: LoopRunnerOptions = {},
  ) {
    this.lookAheadMs = options.lookAheadMs ?? LOOKAHEAD_MS;
  }

  /** True while the runner is between start() and stop(). */
  isRunning(): boolean {
    return this.running;
  }

  /**
   * Starts the runner. Calling start() while already running first stops
   * the previous run, so the runner is always single-loop. The given
   * AudioContext is owned by the runner until stop().
   */
  start(context: AudioContext): void {
    if (this.running) this.stop();
    this.context = context;
    this.running = true;
    this.timerId = setInterval(() => this.tickScheduler(), this.lookAheadMs);
    this.rafId = requestAnimationFrame(() => this.tickDraw());
  }

  /**
   * Stops the runner. Idempotent — calling stop() when not running is a
   * no-op. After stop() returns, neither callback can fire again until
   * the next start().
   */
  stop(): void {
    this.running = false;
    if (this.timerId !== null) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
    if (this.rafId !== 0) {
      cancelAnimationFrame(this.rafId);
      this.rafId = 0;
    }
    this.context = null;
  }

  private tickScheduler(): void {
    const context = this.context;
    if (!this.running || !context) return;
    this.callbacks.schedule(context);
  }

  private tickDraw(): void {
    const context = this.context;
    if (!this.running || !context) return;
    this.callbacks.draw(context);
    // Re-arm only while running. If stop() flipped the flag during the draw
    // callback (or before we got here), the loop ends cleanly.
    if (this.running) {
      this.rafId = requestAnimationFrame(() => this.tickDraw());
    }
  }
}
