// Riff Player — architecture layer 5 (audio).
//
// Plays a MetalRiff's notes as synthesised tones on a loop, at an adjustable
// tempo (the riff library's slow-down). Uses the same "two clocks" pattern as
// the metronome: a setInterval scheduler queues notes slightly ahead on the
// AudioContext clock so timing never drifts, and a requestAnimationFrame loop
// reports the playback position for the tab playhead.
//
// The tones are oscillator-synthesised — a reference preview of the riff's
// pitches and rhythm, not the sound of a real guitar.

import {
  type MetalRiff,
  type RiffNote,
  riffDurationBeats,
  riffNoteMidi,
} from "@/lib/metalRiffs";
import { midiToFrequency } from "@/lib/pitch";

export interface RiffPlayerState {
  isPlaying: boolean;
  /** Playback position within the riff, in beats. 0 when idle. */
  positionBeats: number;
}

const INITIAL_STATE: RiffPlayerState = { isPlaying: false, positionBeats: 0 };

// How often the scheduler wakes up (ms) and how far ahead it schedules (s).
const LOOKAHEAD_MS = 25;
const SCHEDULE_AHEAD_S = 0.12;

type Listener = (state: RiffPlayerState) => void;

class RiffPlayer {
  private state: RiffPlayerState = INITIAL_STATE;
  private listeners = new Set<Listener>();
  private context: AudioContext | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;
  private rafId = 0;
  private riff: MetalRiff | null = null;
  /** Riff notes sorted by start time. */
  private notes: RiffNote[] = [];
  /** Length of one riff loop, in beats. */
  private loopBeats = 1;
  private bpm = 100;
  private nextNoteTime = 0;
  private noteIndex = 0;
  /** AudioContext times at which loop iterations begin (oldest first). */
  private loopStarts: number[] = [];

  getState(): RiffPlayerState {
    return this.state;
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  private setState(patch: Partial<RiffPlayerState>): void {
    this.state = { ...this.state, ...patch };
    for (const listener of this.listeners) {
      listener(this.state);
    }
  }

  /** Sets the riff to play. Stop before changing the riff. */
  setRiff(riff: MetalRiff): void {
    this.riff = riff;
    this.notes = [...riff.notes].sort((a, b) => a.start - b.start);
    this.loopBeats = riffDurationBeats(riff);
    this.bpm = riff.bpm;
  }

  /** Sets the playback tempo; picked up from the next scheduled note. */
  setBpm(bpm: number): void {
    this.bpm = bpm;
  }

  start(context: AudioContext): void {
    if (this.state.isPlaying) this.stop();
    if (!this.riff || this.notes.length === 0) return;
    this.context = context;
    this.noteIndex = 0;
    this.nextNoteTime = context.currentTime + 0.1;
    this.loopStarts = [];
    this.setState({ isPlaying: true, positionBeats: 0 });
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
    this.loopStarts = [];
    this.setState({ isPlaying: false, positionBeats: 0 });
  }

  toggle(context: AudioContext): void {
    if (this.state.isPlaying) {
      this.stop();
    } else {
      this.start(context);
    }
  }

  /** Schedules every note whose time falls inside the look-ahead window. */
  private scheduler(): void {
    const context = this.context;
    const riff = this.riff;
    if (!context || !riff) return;
    while (this.nextNoteTime < context.currentTime + SCHEDULE_AHEAD_S) {
      const note = this.notes[this.noteIndex];
      if (this.noteIndex === 0) this.loopStarts.push(this.nextNoteTime);

      const secondsPerBeat = 60 / this.bpm;
      this.scheduleNote(riff, note, this.nextNoteTime, secondsPerBeat);

      // Advance to the next note, computing the gap incrementally so a tempo
      // change applies cleanly from the next note onward.
      const isLast = this.noteIndex === this.notes.length - 1;
      const nextStart = isLast
        ? this.loopBeats + this.notes[0].start
        : this.notes[this.noteIndex + 1].start;
      this.nextNoteTime += (nextStart - note.start) * secondsPerBeat;
      this.noteIndex = isLast ? 0 : this.noteIndex + 1;
    }
  }

  /** Synthesises one note with a triangle oscillator and a short envelope. */
  private scheduleNote(
    riff: MetalRiff,
    note: RiffNote,
    time: number,
    secondsPerBeat: number,
  ): void {
    const context = this.context;
    if (!context) return;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.connect(gain);
    gain.connect(context.destination);

    oscillator.type = "triangle";
    oscillator.frequency.value = midiToFrequency(riffNoteMidi(riff, note));

    // Palm-muted notes are quieter and decay fast — a thud, not a ring.
    const muted = note.technique === "palm-mute";
    const peak = muted ? 0.16 : 0.3;
    const lengthSec = note.duration * secondsPerBeat;
    const sustain = muted ? Math.min(lengthSec, 0.12) : lengthSec;

    gain.gain.setValueAtTime(0.0001, time);
    gain.gain.exponentialRampToValueAtTime(peak, time + 0.01);
    gain.gain.exponentialRampToValueAtTime(0.0001, time + sustain + 0.06);

    oscillator.start(time);
    oscillator.stop(time + sustain + 0.1);
  }

  /** Reports the playback position to the UI for the tab playhead. */
  private drawLoop(): void {
    const context = this.context;
    if (!context) return;
    const now = context.currentTime;
    while (this.loopStarts.length > 1 && this.loopStarts[1] <= now) {
      this.loopStarts.shift();
    }
    if (this.loopStarts.length > 0 && this.loopStarts[0] <= now) {
      const position = (now - this.loopStarts[0]) / (60 / this.bpm);
      this.setState({ positionBeats: Math.min(position, this.loopBeats) });
    }
    this.rafId = requestAnimationFrame(() => this.drawLoop());
  }
}

/** Shared singleton instance. */
export const riffPlayer = new RiffPlayer();
