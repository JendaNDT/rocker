import { describe, expect, it } from "vitest";
import { getGallopExercise } from "./metalExercises";
import {
  CELLS_PER_ROUND,
  SPEED_UP_RATIO,
  type GallopTrainerState,
  advanceCell,
  cellDurationBeats,
  cellDurationSeconds,
  cellOnsets,
  cellOnsetsSeconds,
  roundProgress,
  secondsPerBeat,
  startGallopTrainer,
} from "./gallopTrainer";

// basic-gallop: cell [0.5, 0.25, 0.25] on e-0, starts at 90 BPM.
const exercise = getGallopExercise("basic-gallop");
if (!exercise) throw new Error("missing fixture exercise");

function advance(
  state: GallopTrainerState,
  cells: number,
): GallopTrainerState {
  let next = state;
  for (let i = 0; i < cells; i += 1) next = advanceCell(next);
  return next;
}

describe("cellOnsets", () => {
  it("returns the running start of each note", () => {
    expect(cellOnsets([0.5, 0.25, 0.25])).toEqual([0, 0.5, 0.75]);
  });

  it("handles the reverse gallop", () => {
    expect(cellOnsets([0.25, 0.25, 0.5])).toEqual([0, 0.25, 0.5]);
  });
});

describe("cellDurationBeats", () => {
  it("sums note lengths to one beat", () => {
    expect(cellDurationBeats(exercise.cell)).toBe(1);
  });
});

describe("secondsPerBeat", () => {
  it("is half a second at 120 BPM", () => {
    expect(secondsPerBeat(120)).toBe(0.5);
  });
});

describe("cellOnsetsSeconds", () => {
  it("scales the onsets by the tempo", () => {
    expect(cellOnsetsSeconds([0.5, 0.25, 0.25], 120)).toEqual([0, 0.25, 0.375]);
  });
});

describe("cellDurationSeconds", () => {
  it("is one beat in seconds", () => {
    expect(cellDurationSeconds([0.5, 0.25, 0.25], 120)).toBe(0.5);
  });
});

describe("startGallopTrainer", () => {
  it("starts at the exercise tempo, zero cells, zero rounds", () => {
    const state = startGallopTrainer(exercise, false);
    expect(state.bpm).toBe(90);
    expect(state.cellsCompleted).toBe(0);
    expect(state.roundsCompleted).toBe(0);
  });
});

describe("advanceCell round completion", () => {
  it("counts a round after CELLS_PER_ROUND cells", () => {
    const state = advance(startGallopTrainer(exercise, false), CELLS_PER_ROUND);
    expect(state.roundsCompleted).toBe(1);
  });

  it("does not count a round mid-way", () => {
    const state = advance(
      startGallopTrainer(exercise, false),
      CELLS_PER_ROUND - 1,
    );
    expect(state.roundsCompleted).toBe(0);
  });
});

describe("roundProgress", () => {
  it("is zero at the start and a half mid-round", () => {
    let state = startGallopTrainer(exercise, false);
    expect(roundProgress(state)).toBe(0);
    state = advance(state, CELLS_PER_ROUND / 2);
    expect(roundProgress(state)).toBe(0.5);
  });
});

describe("speed-up", () => {
  it("raises tempo by ~5% after each round when enabled", () => {
    let state = startGallopTrainer(exercise, true);
    state = advance(state, CELLS_PER_ROUND);
    expect(state.bpm).toBe(95); // clampBpm(90 * 1.05) = clampBpm(94.5) = 95
    state = advance(state, CELLS_PER_ROUND);
    expect(state.bpm).toBe(100); // clampBpm(95 * 1.05) = clampBpm(99.75) = 100
  });

  it("keeps tempo constant when speed-up is disabled", () => {
    const state = advance(
      startGallopTrainer(exercise, false),
      CELLS_PER_ROUND * 2,
    );
    expect(state.bpm).toBe(90);
  });

  it("exposes the speed-up ratio", () => {
    expect(SPEED_UP_RATIO).toBe(0.05);
  });
});
