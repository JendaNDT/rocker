import { describe, expect, it } from "vitest";
import type { Exercise } from "./exercises";
import {
  currentTarget,
  drillNotes,
  feedNote,
  REQUIRED_CLEAN_REPS,
  startSurgical,
  type SurgicalState,
} from "./surgicalEngine";

const exercise: Exercise = {
  id: "test",
  name: "Test",
  description: "",
  notes: [40, 43, 45, 47, 50],
};

function playAll(state: SurgicalState, notes: number[]): SurgicalState {
  return notes.reduce((current, note) => feedNote(current, note), state);
}

describe("startSurgical", () => {
  it("starts in the run phase at position 0", () => {
    const state = startSurgical(exercise);
    expect(state.phase).toBe("run");
    expect(state.position).toBe(0);
    expect(currentTarget(state)).toBe(40);
  });
});

describe("run phase", () => {
  it("advances on a correct note", () => {
    const state = feedNote(startSurgical(exercise), 40);
    expect(state.position).toBe(1);
    expect(currentTarget(state)).toBe(43);
  });

  it("accepts an octave-off note (pitch-class match)", () => {
    const state = feedNote(startSurgical(exercise), 52); // 40 + 12
    expect(state.position).toBe(1);
  });

  it("counts a wrong note without advancing", () => {
    const state = feedNote(startSurgical(exercise), 41);
    expect(state.position).toBe(0);
    expect(state.errorCounts[0]).toBe(1);
  });

  it("completes with no mistakes when the whole run is clean", () => {
    const state = playAll(startSurgical(exercise), exercise.notes);
    expect(state.phase).toBe("complete");
  });

  it("enters the drill phase when the run had mistakes", () => {
    let state = startSurgical(exercise);
    state = feedNote(state, 40);
    state = feedNote(state, 99); // wrong at index 1
    state = feedNote(state, 99); // wrong again at index 1
    state = playAll(state, [43, 45, 47, 50]); // finish the run
    expect(state.phase).toBe("drill");
    expect(drillNotes(state)).toHaveLength(3);
    // Drill window is centred near the troublesome index 1.
    expect(state.drillStart).toBe(0);
  });
});

describe("drill phase", () => {
  function reachDrill(): SurgicalState {
    let state = startSurgical(exercise);
    state = feedNote(state, 40);
    state = feedNote(state, 99); // mistake at index 1
    state = playAll(state, [43, 45, 47, 50]);
    expect(state.phase).toBe("drill");
    return state;
  }

  it("completes after the required clean repetitions", () => {
    let state = reachDrill();
    const window = drillNotes(state);
    for (let rep = 0; rep < REQUIRED_CLEAN_REPS; rep += 1) {
      state = playAll(state, window);
    }
    expect(state.phase).toBe("complete");
  });

  it("resets the streak on a wrong note", () => {
    let state = reachDrill();
    const window = drillNotes(state);
    state = playAll(state, window); // one clean rep
    expect(state.cleanReps).toBe(1);
    state = feedNote(state, 99); // wrong note
    expect(state.cleanReps).toBe(0);
    expect(state.drillPosition).toBe(0);
  });
});
