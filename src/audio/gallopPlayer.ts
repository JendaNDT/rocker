// Gallop Player — architecture layer 5 (audio).
//
// Plays a gallop rhythm cell on a loop using the same "two clocks" pattern as
// the metronome, owned by the shared LoopRunner helper (src/audio/loopRunner.ts)
// so the stop() contract is uniform across every audio player. The scheduler
// queues clicks slightly ahead on the AudioContext clock so they never drift,
// and a requestAnimationFrame loop reports which note is sounding for the UI.
//
// The cell and tempo are plain configuration set by the component (the
// GallopTrainer engine in src/lib/gallopTrainer.ts owns that state). The
// player only reports playback position back through GallopPlayerState.

import { LoopRunner, SCHEDULE_AHEAD_S } from "@/audio/loopRunner";

export interface GallopPlayerState {
  isPlaying: boolean;
  /** Index of the gallop note currently sounding (0-based), or -1 when idle. */
  currentOnset: number;
  /** Number of full cells completed since start. */
  cellsPlayed: number;
}

const INITIAL_STATE: GallopPlayerState = {
  isPlaying: false,
  currentOnset: -1,
  cellsPlayed: 0,
};

type ClickKind = "accent" | "soft";
type Listener = (state: GallopPlayerState) => void;
type QueuedNote = { onset: number; cellEnd: boolean; time: number };

class GallopPlayer {
  private state: GallopPlayerState = INITIAL_STATE;
  private listeners = new Set<Listener>();
  private readonly runner = new LoopRunner({
    schedule: (context) => this.scheduler(context),
    draw: (context) => this.drawLoop(context),
  });
  private bpm = 90;
  private cell: number[] = [0.5, 0.25, 0.25];
  private nextNoteTime = 0;
  private onsetIndex = 0;
  private completedCells = 0;
  private queue: QueuedNote[] = [];

  getState(): GallopPlayerState {
    return this.state;
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  private setState(patch: Partial<GallopPlayerState>): void {
    this.state = { ...this.state, ...patch };
    for (const listener of this.listeners) {
      listener(this.state);
    }
  }

  /** Sets the tempo; picked up from the next scheduled note onward. */
  setBpm(bpm: number): void {
    this.bpm = bpm;
  }

  /** Sets the rhythm cell (note lengths in beats). */
  setCell(cell: number[]): void {
    // Reject an empty cell or non-positive note lengths: a zero-length note
    // never advances nextNoteTime and would freeze the scheduler in a loop.
    if (
      cell.length === 0 ||
      cell.some((length) => !Number.isFinite(length) || length <= 0)
    ) {
      return;
    }
    this.cell = cell;
  }

  /** Starts the player on the given (output) AudioContext. */
  start(context: AudioContext): void {
    if (this.state.isPlaying) this.stop();
    this.onsetIndex = 0;
    this.completedCells = 0;
    this.nextNoteTime = context.currentTime + 0.1;
    this.queue = [];
    this.setState({ isPlaying: true, currentOnset: -1, cellsPlayed: 0 });
    this.runner.start(context);
  }

  stop(): void {
    this.runner.stop();
    this.queue = [];
    this.setState({ isPlaying: false, currentOnset: -1 });
  }

  toggle(context: AudioContext): void {
    if (this.state.isPlaying) {
      this.stop();
    } else {
      this.start(context);
    }
  }

  /** Schedules every onset whose time falls inside the look-ahead window. */
  private scheduler(context: AudioContext): void {
    while (this.nextNoteTime < context.currentTime + SCHEDULE_AHEAD_S) {
      const onset = this.onsetIndex;
      const isLast = onset === this.cell.length - 1;
      this.queue.push({ onset, cellEnd: isLast, time: this.nextNoteTime });
      this.playClick(context, this.nextNoteTime, onset === 0 ? "accent" : "soft");

      const secondsPerBeat = 60 / this.bpm;
      this.nextNoteTime += this.cell[onset] * secondsPerBeat;
      this.onsetIndex = isLast ? 0 : onset + 1;
    }
  }

  /** Synthesises a short click with an oscillator — no audio files needed. */
  private playClick(context: AudioContext, time: number, kind: ClickKind): void {
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    oscillator.connect(gain);
    gain.connect(context.destination);

    oscillator.frequency.value = kind === "accent" ? 1400 : 900;
    const volume = kind === "accent" ? 0.55 : 0.3;
    gain.gain.setValueAtTime(volume, time);
    gain.gain.exponentialRampToValueAtTime(0.0001, time + 0.05);

    oscillator.start(time);
    oscillator.stop(time + 0.06);
  }

  /** Reports the sounding note to the UI as scheduled onsets come due. */
  private drawLoop(context: AudioContext): void {
    while (
      this.queue.length > 0 &&
      this.queue[0].time <= context.currentTime
    ) {
      const note = this.queue.shift();
      if (!note) break;
      const patch: Partial<GallopPlayerState> = { currentOnset: note.onset };
      if (note.cellEnd) {
        this.completedCells += 1;
        patch.cellsPlayed = this.completedCells;
      }
      this.setState(patch);
    }
    // No rAF re-arm here — LoopRunner does it (only while running).
  }
}

/** Shared singleton instance. */
export const gallopPlayer = new GallopPlayer();
