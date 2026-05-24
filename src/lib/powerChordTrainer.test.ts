import { describe, expect, it } from "vitest";
import { getPowerChordExercise } from "./metalExercises";
import {
  type PowerChordTrainerState,
  SPEED_UP_RATIO,
  activeChordId,
  activeChordIndex,
  advanceBeat,
  nextChordIndex,
  roundBeats,
  roundProgress,
  startPowerChordTrainer,
} from "./powerChordTrainer";

// adjacent-frets: 4 chords, 2 beats each, starts at 70 BPM -> round = 8 beats.
const exercise = getPowerChordExercise("adjacent-frets");
if (!exercise) throw new Error("missing fixture exercise");

function advance(
  state: PowerChordTrainerState,
  beats: number,
): PowerChordTrainerState {
  let next = state;
  for (let i = 0; i < beats; i += 1) next = advanceBeat(next);
  return next;
}

describe("roundBeats", () => {
  it("multiplies sequence length by beats per chord", () => {
    expect(roundBeats(exercise)).toBe(8);
  });
});

describe("startPowerChordTrainer", () => {
  it("starts at the exercise tempo, beat zero, no rounds done", () => {
    const state = startPowerChordTrainer(exercise, false);
    expect(state.bpm).toBe(70);
    expect(state.elapsedBeats).toBe(0);
    expect(state.roundsCompleted).toBe(0);
  });
});

describe("activeChordIndex", () => {
  it("holds each chord for beatsPerChord beats", () => {
    let state = startPowerChordTrainer(exercise, false);
    expect(activeChordIndex(state)).toBe(0);
    state = advance(state, 1);
    expect(activeChordIndex(state)).toBe(0); // still chord 0 after 1 of 2 beats
    state = advance(state, 1);
    expect(activeChordIndex(state)).toBe(1); // chord 1 after 2 beats
  });

  it("wraps back to the first chord after a full round", () => {
    const state = advance(startPowerChordTrainer(exercise, false), 8);
    expect(activeChordIndex(state)).toBe(0);
  });
});

describe("nextChordIndex", () => {
  it("points at the upcoming chord", () => {
    expect(nextChordIndex(startPowerChordTrainer(exercise, false))).toBe(1);
  });

  it("wraps from the last chord to the first", () => {
    const state = advance(startPowerChordTrainer(exercise, false), 6);
    expect(activeChordIndex(state)).toBe(3);
    expect(nextChordIndex(state)).toBe(0);
  });
});

describe("activeChordId", () => {
  it("returns the power chord id from the sequence", () => {
    expect(activeChordId(startPowerChordTrainer(exercise, false))).toBe("e-0");
  });
});

describe("roundProgress", () => {
  it("is zero at the start and a half mid-round", () => {
    let state = startPowerChordTrainer(exercise, false);
    expect(roundProgress(state)).toBe(0);
    state = advance(state, 4);
    expect(roundProgress(state)).toBe(0.5);
  });
});

describe("advanceBeat round completion", () => {
  it("counts a completed round after roundBeats beats", () => {
    const state = advance(startPowerChordTrainer(exercise, false), 8);
    expect(state.roundsCompleted).toBe(1);
  });

  it("does not count a round mid-way", () => {
    const state = advance(startPowerChordTrainer(exercise, false), 7);
    expect(state.roundsCompleted).toBe(0);
  });
});

describe("speed-up", () => {
  it("raises tempo by ~5% after each round when enabled", () => {
    let state = startPowerChordTrainer(exercise, true);
    state = advance(state, 8);
    expect(state.bpm).toBe(74); // clampBpm(70 * 1.05) = clampBpm(73.5) = 74
    state = advance(state, 8);
    expect(state.bpm).toBe(78); // clampBpm(74 * 1.05) = clampBpm(77.7) = 78
  });

  it("keeps tempo constant when speed-up is disabled", () => {
    const state = advance(startPowerChordTrainer(exercise, false), 16);
    expect(state.bpm).toBe(70);
  });

  it("exposes the speed-up ratio", () => {
    expect(SPEED_UP_RATIO).toBe(0.05);
  });
});
