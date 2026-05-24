// Metronome — architecture layer 5 (audio).
//
// Sample-accurate click scheduling using the "two clocks" pattern: a coarse
// JS timer (setInterval) wakes up regularly and schedules clicks slightly
// ahead of time directly on the AudioContext clock, so the clicks themselves
// never drift. A requestAnimationFrame loop reports which beat is sounding
// for the visual indicator.

import { clampBpm } from "@/lib/tempo";

export interface MetronomeState {
  isPlaying: boolean;
  bpm: number;
  /** Beats per bar (e.g. 4 for 4/4, 3 for 3/4). */
  beatsPerBar: number;
  /** Ticks per beat: 1 = quarters, 2 = eighths, 3 = triplets, 4 = sixteenths. */
  subdivision: number;
  accentFirst: boolean;
  /** Beat currently sounding (0-based), or -1 when stopped. */
  currentBeat: number;
}

const INITIAL_STATE: MetronomeState = {
  isPlaying: false,
  bpm: 100,
  beatsPerBar: 4,
  subdivision: 1,
  accentFirst: true,
  currentBeat: -1,
};

// How often the scheduler wakes up (ms) and how far ahead it schedules (s).
const LOOKAHEAD_MS = 25;
const SCHEDULE_AHEAD_S = 0.12;

type ClickKind = "accent" | "beat" | "sub";
type Listener = (state: MetronomeState) => void;

class Metronome {
  private state: MetronomeState = INITIAL_STATE;
  private listeners = new Set<Listener>();
  private context: AudioContext | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;
  private rafId = 0;
  private nextNoteTime = 0;
  private currentTick = 0;
  private queue: { tick: number; time: number }[] = [];

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
    this.setState({ beatsPerBar });
  }

  setSubdivision(subdivision: number): void {
    this.setState({ subdivision });
  }

  setAccentFirst(accentFirst: boolean): void {
    this.setState({ accentFirst });
  }

  /** Starts the metronome on the given (output) AudioContext. */
  start(context: AudioContext): void {
    if (this.state.isPlaying) this.stop();
    this.context = context;
    this.currentTick = 0;
    this.nextNoteTime = context.currentTime + 0.1;
    this.queue = [];
    this.setState({ isPlaying: true, currentBeat: -1 });
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
    this.setState({ isPlaying: false, currentBeat: -1 });
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
      this.scheduleTick(this.currentTick, this.nextNoteTime);
      const secondsPerTick = 60 / this.state.bpm / this.state.subdivision;
      this.nextNoteTime += secondsPerTick;
      this.currentTick = (this.currentTick + 1) % this.ticksPerBar();
    }
  }

  private scheduleTick(tick: number, time: number): void {
    this.queue.push({ tick, time });
    const isMainBeat = tick % this.state.subdivision === 0;
    const isAccent = tick === 0 && this.state.accentFirst;
    const kind: ClickKind = isAccent ? "accent" : isMainBeat ? "beat" : "sub";
    this.playClick(time, kind);
  }

  /** Synthesises a short click with an oscillator — no audio files needed. */
  private playClick(time: number, kind: ClickKind): void {
    const context = this.context;
    if (!context) return;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.connect(gain);
    gain.connect(context.destination);

    oscillator.frequency.value =
      kind === "accent" ? 1600 : kind === "beat" ? 1000 : 760;
    const volume = kind === "sub" ? 0.22 : kind === "accent" ? 0.6 : 0.42;
    gain.gain.setValueAtTime(volume, time);
    gain.gain.exponentialRampToValueAtTime(0.0001, time + 0.05);

    oscillator.start(time);
    oscillator.stop(time + 0.06);
  }

  /** Reports the sounding beat to the UI as scheduled clicks come due. */
  private drawLoop(): void {
    const context = this.context;
    if (!context) return;
    while (this.queue.length > 0 && this.queue[0].time <= context.currentTime) {
      const note = this.queue.shift();
      if (note && note.tick % this.state.subdivision === 0) {
        const beat = note.tick / this.state.subdivision;
        if (beat !== this.state.currentBeat) {
          this.setState({ currentBeat: beat });
        }
      }
    }
    this.rafId = requestAnimationFrame(() => this.drawLoop());
  }
}

/** Shared singleton instance. */
export const metronome = new Metronome();
