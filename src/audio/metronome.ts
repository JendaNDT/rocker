// Metronome — architecture layer 5 (audio).
//
// Sample-accurate click scheduling using the "two clocks" pattern: a coarse
// JS timer (setInterval) wakes up regularly and schedules clicks slightly
// ahead of time directly on the AudioContext clock, so the clicks themselves
// never drift. A requestAnimationFrame loop reports which beat is sounding
// for the visual indicator.

import type {
  AccentLevel,
  ClickSoundId,
  GapConfig,
  PerBeatSound,
  PolyrhythmConfig,
  RampConfig,
} from "@/lib/metronomeConfig";
import {
  clamp01,
  clampCountInBars,
  clampSwing,
  cycleAccent,
  DEFAULT_GAP,
  DEFAULT_POLYRHYTHM,
  DEFAULT_RAMP,
  DEFAULT_SWING,
  defaultAccentPattern,
  isClickSoundId,
  isSilentBar,
  nextRampBpm,
  resizeAccentPattern,
  resizeSoundPattern,
  sanitizeGap,
  sanitizePolyrhythm,
  sanitizeRamp,
  swingedTickDuration,
} from "@/lib/metronomeConfig";
import { clampBpm } from "@/lib/tempo";

export interface MetronomeState {
  isPlaying: boolean;
  bpm: number;
  /** Beats per bar (e.g. 4 for 4/4, 3 for 3/4). */
  beatsPerBar: number;
  /** Ticks per beat: 1 = quarters, 2 = eighths, 3 = triplets, 4 = sixteenths. */
  subdivision: number;
  /** Accent level for each beat in the bar; length matches beatsPerBar. */
  accentPattern: AccentLevel[];
  /** Synthesised click sound. */
  sound: ClickSoundId;
  /** Master metronome volume (0..1). */
  volume: number;
  /** Loudness of accented beats relative to their full level (0..1). */
  accentVolume: number;
  /** Progressive speed-up trainer. */
  ramp: RampConfig;
  /** Gap trainer (alternating audible and silent bars). */
  gap: GapConfig;
  /** Swing / shuffle feel for paired subdivisions (0.5 = straight). */
  swing: number;
  /** Count-in bars (0, 1 or 2) — played before the main tempo starts. */
  countInBars: number;
  /**
   * True while the engine is in the count-in phase. Drives the small
   * countdown widget on the screen. Set only by the scheduler; ignored
   * by setters.
   */
  isCountingIn: boolean;
  /** Secondary polyrhythmic click layer. */
  polyrhythm: PolyrhythmConfig;
  /**
   * Optional per-beat sound override (drum mode). Same length as
   * accentPattern; non-null entries take precedence over `sound`.
   * undefined = every beat uses `sound`.
   */
  soundPattern?: PerBeatSound;
  /**
   * Optional sound used for subdivision ticks. When set, every subdivision
   * tick plays this sound (e.g. hi-hat over kick/snare). undefined =
   * subdivisions play the beat's own sound.
   */
  subdivisionSound?: ClickSoundId;
  /** Beat currently sounding (0-based), or -1 when stopped or in a gap. */
  currentBeat: number;
  /** Bar currently sounding (0-based), or -1 when stopped. */
  currentBar: number;
}

const INITIAL_STATE: MetronomeState = {
  isPlaying: false,
  bpm: 100,
  beatsPerBar: 4,
  subdivision: 1,
  accentPattern: defaultAccentPattern(4),
  sound: "classic",
  volume: 0.8,
  accentVolume: 1,
  ramp: { ...DEFAULT_RAMP },
  gap: { ...DEFAULT_GAP },
  swing: DEFAULT_SWING,
  countInBars: 0,
  isCountingIn: false,
  polyrhythm: { ...DEFAULT_POLYRHYTHM },
  currentBeat: -1,
  currentBar: -1,
};

// How often the scheduler wakes up (ms) and how far ahead it schedules (s).
const LOOKAHEAD_MS = 25;
const SCHEDULE_AHEAD_S = 0.12;

type ClickKind = "accent" | "beat" | "sub" | "muted";
type Listener = (state: MetronomeState) => void;

interface ScheduledNote {
  tick: number;
  time: number;
  bar: number;
  /** True when the bar is in the gap trainer's silent phase. */
  silent: boolean;
}

class Metronome {
  private state: MetronomeState = INITIAL_STATE;
  private listeners = new Set<Listener>();
  private context: AudioContext | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;
  private rafId = 0;
  private nextNoteTime = 0;
  private currentTick = 0;
  private scheduledBar = 0;
  private rampBarCounter = 0;
  private queue: ScheduledNote[] = [];
  private noiseBuffer: AudioBuffer | null = null;
  private noiseContext: AudioContext | null = null;
  /**
   * Secondary polyrhythm scheduler state. Independent tick stream, but
   * phase-aligned to the main downbeat at start(). Snaps back to the bar
   * boundary every time a new bar begins.
   */
  private secondaryNextNoteTime = 0;
  private secondaryTickInBar = 0;
  /** Bar index of the next secondary tick — kept in sync with scheduledBar. */
  private secondaryBar = 0;

  getState(): MetronomeState {
    return this.state;
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  private setState(patch: Partial<MetronomeState>): void {
    this.state = { ...this.state, ...patch };
    for (const listener of this.listeners) {
      listener(this.state);
    }
  }

  setBpm(bpm: number): void {
    this.setState({ bpm: clampBpm(bpm) });
  }

  setBeatsPerBar(beatsPerBar: number): void {
    const pattern = resizeAccentPattern(this.state.accentPattern, beatsPerBar);
    const sounds = this.state.soundPattern
      ? resizeSoundPattern(this.state.soundPattern, beatsPerBar)
      : undefined;
    const patch: Partial<MetronomeState> = {
      beatsPerBar,
      accentPattern: pattern,
    };
    // Keep the field absent when there is no override — every beat uses sound.
    if (sounds && sounds.some((entry) => entry !== null)) {
      patch.soundPattern = sounds;
    } else if (this.state.soundPattern) {
      patch.soundPattern = undefined;
    }
    this.setState(patch);
    // Re-align to a downbeat: a mid-play change shrinks ticksPerBar() and the
    // running currentTick could otherwise land on a stray, mis-accented tick.
    if (this.state.isPlaying) this.currentTick = 0;
  }

  setSubdivision(subdivision: number): void {
    this.setState({ subdivision });
    if (this.state.isPlaying) this.currentTick = 0;
  }

  /** Replaces the whole accent pattern; resized to the current bar length. */
  setAccentPattern(pattern: AccentLevel[]): void {
    const resized = resizeAccentPattern(pattern, this.state.beatsPerBar);
    this.setState({ accentPattern: resized });
  }

  /** Cycles a single beat's accent level (normal -> accent -> muted). */
  cycleAccent(beat: number): void {
    if (beat < 0 || beat >= this.state.beatsPerBar) return;
    const pattern = this.state.accentPattern.slice();
    pattern[beat] = cycleAccent(pattern[beat] ?? "normal");
    this.setState({ accentPattern: pattern });
  }

  setSound(sound: ClickSoundId): void {
    this.setState({ sound });
  }

  /**
   * Replaces the whole per-beat sound pattern. Pass undefined to clear it
   * (every beat then uses the global `sound`).
   */
  setSoundPattern(pattern: PerBeatSound | undefined): void {
    if (!pattern) {
      this.setState({ soundPattern: undefined });
      return;
    }
    const resized = resizeSoundPattern(pattern, this.state.beatsPerBar);
    // An all-null pattern is the "no override" state — drop it so the rest
    // of the engine can keep checking soundPattern as a single truthy flag.
    if (resized.every((entry) => entry === null)) {
      this.setState({ soundPattern: undefined });
    } else {
      this.setState({ soundPattern: resized });
    }
  }

  /**
   * Sets the sound for a single beat. Pass null to clear the override on
   * that beat (it falls back to the global `sound`).
   */
  setBeatSound(beat: number, sound: ClickSoundId | null): void {
    if (beat < 0 || beat >= this.state.beatsPerBar) return;
    const current =
      this.state.soundPattern ??
      Array.from({ length: this.state.beatsPerBar }, () => null as ClickSoundId | null);
    const next: PerBeatSound = current.slice();
    next[beat] = sound;
    this.setSoundPattern(next);
  }

  /**
   * Sets the sound used for every subdivision tick (e.g. hi-hat on 8ths).
   * Pass undefined to clear it — subdivisions then play the beat's own sound.
   */
  setSubdivisionSound(sound: ClickSoundId | undefined): void {
    if (sound === undefined) {
      this.setState({ subdivisionSound: undefined });
    } else if (isClickSoundId(sound)) {
      this.setState({ subdivisionSound: sound });
    }
  }

  /** Sets the swing / shuffle ratio (0.5 = straight, up to 0.75). */
  setSwing(swing: number): void {
    this.setState({ swing: clampSwing(swing) });
  }

  /** Sets how many count-in bars (0, 1 or 2) play before the main tempo. */
  setCountInBars(bars: number): void {
    this.setState({ countInBars: clampCountInBars(bars) });
  }

  /** Patches the secondary polyrhythm layer (merge style like ramp/gap). */
  setPolyrhythm(patch: Partial<PolyrhythmConfig>): void {
    this.setState({
      polyrhythm: sanitizePolyrhythm({ ...this.state.polyrhythm, ...patch }),
    });
  }

  setVolume(volume: number): void {
    this.setState({ volume: clamp01(volume) });
  }

  setAccentVolume(accentVolume: number): void {
    this.setState({ accentVolume: clamp01(accentVolume) });
  }

  /** Configures the progressive speed-up trainer. */
  setRamp(ramp: Partial<RampConfig>): void {
    this.setState({ ramp: sanitizeRamp({ ...this.state.ramp, ...ramp }) });
    this.rampBarCounter = 0;
  }

  /** Configures the gap trainer (alternating audible and silent bars). */
  setGap(gap: Partial<GapConfig>): void {
    this.setState({ gap: sanitizeGap({ ...this.state.gap, ...gap }) });
  }

  /** Starts the metronome on the given (output) AudioContext. */
  start(context: AudioContext): void {
    if (this.state.isPlaying) this.stop();
    this.context = context;
    this.currentTick = 0;
    // Count-in plays its bars at indices -countInBars..-1; the user's first
    // "real" downbeat sits at bar 0. The scheduledBar counter advances at
    // every bar boundary just as before.
    const countIn = this.state.countInBars;
    this.scheduledBar = -countIn;
    this.rampBarCounter = 0;
    this.nextNoteTime = context.currentTime + 0.1;
    // Secondary polyrhythm starts on the same downbeat — the layers meet
    // again at every bar boundary.
    this.secondaryNextNoteTime = this.nextNoteTime;
    this.secondaryTickInBar = 0;
    this.secondaryBar = -countIn;
    this.queue = [];
    this.setState({
      isPlaying: true,
      isCountingIn: countIn > 0,
      currentBeat: -1,
      // currentBar starts at the first count-in bar so the UI countdown
      // ("2/2 taktů") can show as soon as the first tick sounds.
      currentBar: countIn > 0 ? -countIn : -1,
    });
    this.timerId = setInterval(() => this.scheduler(), LOOKAHEAD_MS);
    this.rafId = requestAnimationFrame(() => this.drawLoop());
  }

  stop(): void {
    if (this.timerId !== null) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
    if (this.rafId !== 0) {
      cancelAnimationFrame(this.rafId);
      this.rafId = 0;
    }
    this.queue = [];
    this.setState({
      isPlaying: false,
      isCountingIn: false,
      currentBeat: -1,
      currentBar: -1,
    });
  }

  toggle(context: AudioContext): void {
    if (this.state.isPlaying) {
      this.stop();
    } else {
      this.start(context);
    }
  }

  private ticksPerBar(): number {
    return this.state.beatsPerBar * this.state.subdivision;
  }

  /** Schedules every click whose time falls inside the look-ahead window. */
  private scheduler(): void {
    const context = this.context;
    if (!context) return;
    while (this.nextNoteTime < context.currentTime + SCHEDULE_AHEAD_S) {
      this.scheduleTick(this.currentTick, this.nextNoteTime, this.scheduledBar);
      const { bpm, subdivision, swing } = this.state;
      const baseDuration = 60 / bpm / subdivision;
      // Swing slides the inner ticks of every beat — the beat itself stays
      // exactly on time. tickInBeat is the position within the current beat
      // (0..subdivision-1).
      const tickInBeat = this.currentTick % subdivision;
      this.nextNoteTime += swingedTickDuration(
        tickInBeat,
        baseDuration,
        subdivision,
        swing,
      );
      const nextTick = (this.currentTick + 1) % this.ticksPerBar();
      if (nextTick === 0) {
        this.scheduledBar += 1;
        this.applyRamp(this.scheduledBar);
      }
      this.currentTick = nextTick;
    }

    // Independent secondary polyrhythm scheduler. Walks its own tick stream
    // at `60 / bpm / ticksPerBeat` seconds per tick. Phase-aligned to the
    // main downbeat at start() and snapped back at every bar boundary so
    // mid-bar BPM changes don't drift permanently.
    this.scheduleSecondary(context);
  }

  /** Schedules secondary polyrhythmic ticks inside the look-ahead window. */
  private scheduleSecondary(context: AudioContext): void {
    const { polyrhythm, bpm, beatsPerBar } = this.state;
    if (!polyrhythm.enabled) return;
    const ticksPerBar = beatsPerBar * polyrhythm.ticksPerBeat;
    const secondaryTickSeconds = 60 / bpm / polyrhythm.ticksPerBeat;
    while (
      this.secondaryNextNoteTime < context.currentTime + SCHEDULE_AHEAD_S
    ) {
      // Skip count-in bars (negative) and silent gap bars — the secondary
      // layer is an extra colour, not a counting aid.
      const audible =
        this.secondaryBar >= 0 && !isSilentBar(this.secondaryBar, this.state.gap);
      if (audible) {
        const level =
          polyrhythm.volume * this.state.volume * 0.5; // headroom for the layer
        if (level >= 0.0005) {
          this.playSecondaryTick(
            context,
            this.secondaryNextNoteTime,
            level,
            polyrhythm.sound,
          );
        }
      }
      this.secondaryNextNoteTime += secondaryTickSeconds;
      this.secondaryTickInBar += 1;
      if (this.secondaryTickInBar >= ticksPerBar) {
        this.secondaryTickInBar = 0;
        this.secondaryBar += 1;
      }
    }
  }

  /** Plays a single secondary tick using the same synth as the main layer. */
  private playSecondaryTick(
    context: AudioContext,
    time: number,
    level: number,
    sound: ClickSoundId,
  ): void {
    switch (sound) {
      case "classic":
        this.playTone(context, time, level, "sine", 1100, 0.04);
        break;
      case "wood":
        this.playTone(context, time, level, "triangle", 950, 0.03);
        break;
      case "beep":
        this.playTone(context, time, level, "sine", 900, 0.08, 0.004);
        break;
      case "click":
        this.playNoise(context, time, level, 2000, 0.012);
        break;
      case "hihat":
        this.playNoise(context, time, level, 7500, 0.035);
        break;
    }
  }

  /**
   * At each bar boundary, raises the tempo if the ramp trainer is due.
   * Count-in bars (bar < 0) are excluded — the trainer kicks in only once
   * the real performance begins.
   */
  private applyRamp(bar: number): void {
    if (bar < 0) return;
    const { ramp } = this.state;
    if (!ramp.enabled) return;
    this.rampBarCounter += 1;
    if (this.rampBarCounter < ramp.everyBars) return;
    this.rampBarCounter = 0;
    const next = nextRampBpm(this.state.bpm, ramp);
    if (next !== this.state.bpm) this.setBpm(next);
  }

  private scheduleTick(tick: number, time: number, bar: number): void {
    const silent = isSilentBar(bar, this.state.gap);
    this.queue.push({ tick, time, bar, silent });
    if (!silent) {
      const kind = this.clickKind(tick);
      this.playClick(time, kind, this.soundForTick(tick, kind));
    }
  }

  /** Decides what a tick should sound like from the accent pattern. */
  private clickKind(tick: number): ClickKind {
    const { subdivision, accentPattern } = this.state;
    const beatIndex = Math.floor(tick / subdivision);
    const level = accentPattern[beatIndex] ?? "normal";
    if (level === "muted") return "muted";
    if (tick % subdivision !== 0) return "sub";
    return level === "accent" ? "accent" : "beat";
  }

  /**
   * Picks the click sound for a tick. Precedence:
   *  - subdivision tick  → subdivisionSound (if set) else beat's own sound
   *  - beat tick         → soundPattern[beat] (if set) else global `sound`
   */
  private soundForTick(tick: number, kind: ClickKind): ClickSoundId {
    const { subdivision, sound, soundPattern, subdivisionSound } = this.state;
    if (kind === "sub" && subdivisionSound) return subdivisionSound;
    const beatIndex = Math.floor(tick / subdivision);
    const override = soundPattern?.[beatIndex];
    if (override) return override;
    return sound;
  }

  /** Plays the selected synthesised click — no audio files needed. */
  private playClick(time: number, kind: ClickKind, sound: ClickSoundId): void {
    const context = this.context;
    if (!context || kind === "muted") return;

    const accent = kind === "accent";
    const sub = kind === "sub";
    let level = accent
      ? 0.9 * this.state.accentVolume
      : sub
        ? 0.32
        : 0.6;
    level *= this.state.volume;
    if (level < 0.0005) return;

    switch (sound) {
      case "classic":
        this.playTone(
          context,
          time,
          level,
          "sine",
          accent ? 1600 : sub ? 760 : 1000,
          0.05,
        );
        break;
      case "wood":
        this.playTone(
          context,
          time,
          level,
          "triangle",
          accent ? 1150 : sub ? 720 : 920,
          0.035,
        );
        break;
      case "beep":
        this.playTone(
          context,
          time,
          level,
          "sine",
          accent ? 1320 : sub ? 660 : 880,
          0.09,
          0.004,
        );
        break;
      case "click":
        this.playNoise(
          context,
          time,
          level,
          accent ? 2600 : sub ? 1500 : 2000,
          accent ? 0.02 : 0.014,
        );
        break;
      case "hihat":
        this.playNoise(
          context,
          time,
          level,
          accent ? 9000 : sub ? 7000 : 8000,
          accent ? 0.06 : sub ? 0.03 : 0.045,
        );
        break;
    }
  }

  /** Synthesises a pitched click from a single oscillator. */
  private playTone(
    context: AudioContext,
    time: number,
    level: number,
    type: OscillatorType,
    freq: number,
    decay: number,
    attack = 0.001,
  ): void {
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.type = type;
    oscillator.frequency.value = freq;
    oscillator.connect(gain);
    gain.connect(context.destination);

    gain.gain.setValueAtTime(0.0001, time);
    gain.gain.exponentialRampToValueAtTime(level, time + attack);
    gain.gain.exponentialRampToValueAtTime(0.0001, time + attack + decay);

    oscillator.start(time);
    oscillator.stop(time + attack + decay + 0.02);
  }

  /** Synthesises a noise-based click (sharp click, hi-hat) via a filter. */
  private playNoise(
    context: AudioContext,
    time: number,
    level: number,
    highpassFreq: number,
    decay: number,
  ): void {
    const source = context.createBufferSource();
    source.buffer = this.getNoiseBuffer(context);
    const filter = context.createBiquadFilter();
    filter.type = "highpass";
    filter.frequency.value = highpassFreq;
    const gain = context.createGain();
    source.connect(filter);
    filter.connect(gain);
    gain.connect(context.destination);

    gain.gain.setValueAtTime(level, time);
    gain.gain.exponentialRampToValueAtTime(0.0001, time + decay);

    source.start(time);
    source.stop(time + decay + 0.02);
  }

  /** Lazily builds (and caches) a short white-noise buffer for the context. */
  private getNoiseBuffer(context: AudioContext): AudioBuffer {
    if (this.noiseBuffer && this.noiseContext === context) {
      return this.noiseBuffer;
    }
    const buffer = context.createBuffer(
      1,
      Math.floor(context.sampleRate * 0.3),
      context.sampleRate,
    );
    const data = buffer.getChannelData(0);
    for (let i = 0; i < data.length; i += 1) {
      data[i] = Math.random() * 2 - 1;
    }
    this.noiseBuffer = buffer;
    this.noiseContext = context;
    return buffer;
  }

  /** Reports the sounding beat to the UI as scheduled clicks come due. */
  private drawLoop(): void {
    const context = this.context;
    if (!context) return;
    while (this.queue.length > 0 && this.queue[0].time <= context.currentTime) {
      const note = this.queue.shift();
      if (note && note.tick % this.state.subdivision === 0) {
        // A silent gap bar lights no dot; the bar counter still advances.
        const beat = note.silent ? -1 : note.tick / this.state.subdivision;
        const isCountingIn = note.bar < 0;
        if (
          beat !== this.state.currentBeat ||
          note.bar !== this.state.currentBar ||
          isCountingIn !== this.state.isCountingIn
        ) {
          this.setState({
            currentBeat: beat,
            currentBar: note.bar,
            isCountingIn,
          });
        }
      }
    }
    this.rafId = requestAnimationFrame(() => this.drawLoop());
  }
}

/** Shared singleton instance. */
export const metronome = new Metronome();
