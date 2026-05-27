// Confirmation sound — a quick two-note "ting-ting" played when the Guided
// tuner detects that a string has settled in the in-tune zone. Same Web
// Audio synthesis approach as the metronome and reference tone — no audio
// samples, no bundle weight.
//
// The two notes (1320 Hz, then 1980 Hz a perfect fifth higher) play in
// sequence; the second starts ~130 ms after the first so the user hears a
// clear "ting-ting" rather than a chord.

/** First chime — pitched lower so the second one feels like a rise. */
const FIRST_FREQ_HZ = 1320;
/** Second chime, a perfect fifth higher than the first. */
const SECOND_FREQ_HZ = 1980;
const SECOND_DELAY_S = 0.13;
/** Per-chime decay (exponential). Keeps each ting short but audible. */
const CHIME_DECAY_S = 0.12;
const CHIME_ATTACK_S = 0.004;
/** Combined level — both chimes briefly overlap, so we leave headroom. */
const PEAK_LEVEL = 0.45;

export const DEFAULT_CONFIRMATION_VOLUME = 1;

/**
 * Clamps a volume multiplier to [0, 1]; falls back to the default on
 * garbage. Mirrors the guard in referenceTone for consistency.
 */
export function clampConfirmationVolume(value: unknown): number {
  if (typeof value !== "number") return DEFAULT_CONFIRMATION_VOLUME;
  if (!Number.isFinite(value)) return DEFAULT_CONFIRMATION_VOLUME;
  if (value < 0) return 0;
  if (value > 1) return 1;
  return value;
}

/**
 * Tiny lazy AudioContext + chime player. One shared instance lives in
 * module scope — the tuner triggers it on every confirmation, but only
 * one chime sequence is sounding at a time.
 *
 * Owns its own context for the same reason as referenceTone: the chime
 * must work even when the main audioEngine is stopped (the user can ask
 * for confirmation chimes regardless of how the input pipeline looks).
 */
class ConfirmationPlayer {
  private context: AudioContext | null = null;
  private disposed = false;

  /** Plays the two-chime sequence. No-op after dispose or in non-browser. */
  play(volume: number = DEFAULT_CONFIRMATION_VOLUME): void {
    if (this.disposed) return;
    const ctx = this.getContext();
    if (!ctx) return;

    if (ctx.state === "suspended") {
      // The call site is always inside a setTimeout reacting to an earlier
      // user gesture (the user reached in-tune by playing the string).
      // Modern browsers accept resume in that chain.
      void ctx.resume();
    }

    const level = clampConfirmationVolume(volume) * PEAK_LEVEL;
    if (level < 0.0005) return;

    const now = ctx.currentTime;
    this.playChime(ctx, now, FIRST_FREQ_HZ, level);
    this.playChime(ctx, now + SECOND_DELAY_S, SECOND_FREQ_HZ, level);
  }

  /** Releases the AudioContext. Call from a teardown if no longer needed. */
  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    if (this.context) {
      void this.context.close().catch(() => {
        // Closing an already-closed context can throw; we don't care.
      });
      this.context = null;
    }
  }

  /** Single sine chime with an exponential decay envelope. */
  private playChime(
    ctx: AudioContext,
    time: number,
    frequency: number,
    level: number,
  ): void {
    const oscillator = ctx.createOscillator();
    const gain = ctx.createGain();
    oscillator.type = "sine";
    oscillator.frequency.value = frequency;
    oscillator.connect(gain);
    gain.connect(ctx.destination);

    gain.gain.setValueAtTime(0.0001, time);
    gain.gain.exponentialRampToValueAtTime(level, time + CHIME_ATTACK_S);
    gain.gain.exponentialRampToValueAtTime(
      0.0001,
      time + CHIME_ATTACK_S + CHIME_DECAY_S,
    );

    oscillator.start(time);
    oscillator.stop(time + CHIME_ATTACK_S + CHIME_DECAY_S + 0.02);
  }

  /** Lazily constructs the AudioContext on the first play. */
  private getContext(): AudioContext | null {
    if (this.context) return this.context;
    if (this.disposed) return null;
    if (typeof window === "undefined") return null;
    const Ctor =
      window.AudioContext ??
      (window as unknown as { webkitAudioContext?: typeof AudioContext })
        .webkitAudioContext;
    if (!Ctor) return null;
    try {
      this.context = new Ctor();
      return this.context;
    } catch {
      return null;
    }
  }
}

// Module-scoped singleton. Lazily constructed on first call; the underlying
// AudioContext is created even lazier — only when the first chime sounds.
let player: ConfirmationPlayer | null = null;

/**
 * Plays the confirmation chime. The first call may construct an
 * AudioContext, so it should run in response to (or soon after) a user
 * interaction — calling it from inside a tuner-driven setTimeout works.
 */
export function playConfirmationChime(volume?: number): void {
  if (typeof window === "undefined") return;
  if (!player) player = new ConfirmationPlayer();
  player.play(volume ?? DEFAULT_CONFIRMATION_VOLUME);
}

/**
 * Returns whether haptic vibration is available on this device. iOS Safari
 * exposes navigator but not vibrate; Android Chrome supports it.
 */
export function isHapticAvailable(): boolean {
  if (typeof navigator === "undefined") return false;
  return typeof navigator.vibrate === "function";
}

/**
 * Fires a short haptic pulse (60 ms). No-op when the device or browser
 * doesn't support vibration — safe to call unconditionally.
 */
export function pulseHaptic(durationMs = 60): void {
  if (!isHapticAvailable()) return;
  try {
    navigator.vibrate(durationMs);
  } catch {
    // Some browsers throw if vibrate is called too often or from a context
    // without user activation. We swallow it — the user can live without
    // the buzz, and a thrown exception shouldn't break tuning.
  }
}
