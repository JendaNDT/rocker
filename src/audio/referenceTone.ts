// Reference tone — short, guitar-coloured pitch played through the speakers
// so the user can tune by ear or pre-check a string before plugging in.
//
// The tuner module hosts this so it lives next to the other audio code, but
// it owns its own AudioContext: the reference tone must work even when the
// main audioEngine never started (no microphone permission needed).
//
// Sound design: a sine fundamental plus weak 2nd and 3rd harmonics. Pure
// sine is too flat to compare against a real string; one extra octave and
// the 12th give the tone enough body to recognise without making it muddy.

const ATTACK_S = 0.03;
const RELEASE_S = 0.05;
/** Hard cap on tone length. Stops the tone if pointer-up never arrives. */
const SAFETY_CAP_MS = 5000;
const MIN_FREQ_HZ = 20;
const MAX_FREQ_HZ = 5000;

const HARMONIC_GAIN_2 = 0.3;
const HARMONIC_GAIN_3 = 0.15;

export const DEFAULT_REFERENCE_TONE_VOLUME = 0.3;

/**
 * Clamps a stored volume to the [0, 1] range; falls back to the default on
 * non-numbers, NaN or infinity. Used by the settings store rehydration and
 * by the volume slider so a corrupt value never blows up the audio graph.
 */
export function clampReferenceVolume(value: unknown): number {
  if (typeof value !== "number") return DEFAULT_REFERENCE_TONE_VOLUME;
  if (!Number.isFinite(value)) return DEFAULT_REFERENCE_TONE_VOLUME;
  if (value < 0) return 0;
  if (value > 1) return 1;
  return value;
}

/**
 * Frequency guard — only the audible-guitar range plays. Anything outside
 * (silly values, NaN, infinity) is a no-op. Lets the caller pass raw
 * `midiToFrequency` output without pre-checking.
 */
export function isPlayableFrequency(freq: number): boolean {
  return (
    Number.isFinite(freq) && freq >= MIN_FREQ_HZ && freq <= MAX_FREQ_HZ
  );
}

interface OscillatorRef {
  oscillator: OscillatorNode;
  gain: GainNode;
}

/**
 * Hold-to-play reference tone. Callers create one instance per tuner screen
 * (via `useRef`), call `play()` on pointer-down and `stop()` on pointer-up,
 * then `dispose()` on unmount. Calling `play()` while a tone is already
 * sounding cleanly retunes — useful when dragging across the string row.
 */
export class ReferenceTone {
  private context: AudioContext | null = null;
  private master: GainNode | null = null;
  private oscillators: OscillatorRef[] = [];
  private safetyTimer: ReturnType<typeof setTimeout> | null = null;
  private cleanupTimer: ReturnType<typeof setTimeout> | null = null;
  private disposed = false;

  /**
   * Starts (or retunes) the reference tone. A no-op for invalid frequencies
   * or after dispose. The 5-second safety cap also fires `stop()` in case
   * the pointer-up event never arrives (e.g. a touch ending off-screen).
   */
  play(frequency: number, volume: number): void {
    if (this.disposed) return;
    if (!isPlayableFrequency(frequency)) return;

    const ctx = this.getContext();
    if (!ctx) return;

    // Browsers keep an AudioContext suspended until the first user gesture.
    // The play call itself is one (pointer-down on a button), so this is
    // safe and usually silent.
    if (ctx.state === "suspended") {
      void ctx.resume();
    }

    // Cancel any in-flight teardown so a quick re-press doesn't disconnect
    // the new oscillators a few ms after they start.
    if (this.cleanupTimer !== null) {
      clearTimeout(this.cleanupTimer);
      this.cleanupTimer = null;
    }
    this.tearDownOscillators();

    const target = Math.max(0.0001, clampReferenceVolume(volume));
    const master = ctx.createGain();
    const now = ctx.currentTime;
    master.gain.setValueAtTime(0.0001, now);
    master.gain.exponentialRampToValueAtTime(target, now + ATTACK_S);
    master.connect(ctx.destination);
    this.master = master;

    const harmonics: { mul: number; gain: number }[] = [
      { mul: 1, gain: 1 },
      { mul: 2, gain: HARMONIC_GAIN_2 },
      { mul: 3, gain: HARMONIC_GAIN_3 },
    ];
    for (const { mul, gain } of harmonics) {
      const oscillator = ctx.createOscillator();
      oscillator.type = "sine";
      oscillator.frequency.value = frequency * mul;
      const g = ctx.createGain();
      g.gain.value = gain;
      oscillator.connect(g);
      g.connect(master);
      oscillator.start(now);
      this.oscillators.push({ oscillator, gain: g });
    }

    this.safetyTimer = setTimeout(() => this.stop(), SAFETY_CAP_MS);
  }

  /**
   * Releases the tone with a short fade. Safe to call when nothing is
   * playing — does nothing in that case.
   */
  stop(): void {
    const ctx = this.context;
    if (!ctx) return;
    this.scheduleRelease(ctx);
  }

  /**
   * Releases the AudioContext and any pending timers. Call once on unmount.
   * After dispose, further `play()` calls are no-ops.
   */
  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    if (this.safetyTimer !== null) {
      clearTimeout(this.safetyTimer);
      this.safetyTimer = null;
    }
    if (this.cleanupTimer !== null) {
      clearTimeout(this.cleanupTimer);
      this.cleanupTimer = null;
    }
    this.tearDownOscillators();
    if (this.context) {
      void this.context.close().catch(() => {
        // Closing an already-closed context throws on some browsers; the
        // dispose flow doesn't care either way.
      });
      this.context = null;
    }
  }

  /** Schedules the release envelope and a delayed disconnect. */
  private scheduleRelease(ctx: AudioContext): void {
    if (this.safetyTimer !== null) {
      clearTimeout(this.safetyTimer);
      this.safetyTimer = null;
    }
    const master = this.master;
    if (!master) return;
    const now = ctx.currentTime;
    master.gain.cancelScheduledValues(now);
    master.gain.setValueAtTime(master.gain.value, now);
    master.gain.exponentialRampToValueAtTime(0.0001, now + RELEASE_S);

    // Disconnect after the release so we don't truncate the fade. Capturing
    // the nodes locally lets a quick re-press start a new tone immediately
    // without the cleanup nuking it.
    const oscs = this.oscillators;
    const releasedMaster = master;
    this.master = null;
    this.oscillators = [];
    this.cleanupTimer = setTimeout(() => {
      this.cleanupTimer = null;
      for (const ref of oscs) {
        try {
          ref.oscillator.stop();
        } catch {
          // Already stopped — ignore.
        }
        ref.oscillator.disconnect();
        ref.gain.disconnect();
      }
      releasedMaster.disconnect();
    }, RELEASE_S * 1000 + 30);
  }

  /** Immediate, fade-less teardown — used when retuning or on dispose. */
  private tearDownOscillators(): void {
    for (const ref of this.oscillators) {
      try {
        ref.oscillator.stop();
      } catch {
        // Already stopped — ignore.
      }
      ref.oscillator.disconnect();
      ref.gain.disconnect();
    }
    this.oscillators = [];
    if (this.master) {
      this.master.disconnect();
      this.master = null;
    }
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
