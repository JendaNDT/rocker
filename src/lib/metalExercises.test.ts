import { describe, expect, it } from "vitest";
import {
  GALLOP_EXERCISES,
  POWER_CHORD_EXERCISES,
  getGallopExercise,
  getPowerChordExercise,
} from "./metalExercises";
import { getPowerChord } from "./powerChords";

describe("power chord exercises", () => {
  it("every sequence id resolves to a real power chord", () => {
    for (const exercise of POWER_CHORD_EXERCISES) {
      for (const id of exercise.sequence) {
        expect(getPowerChord(id), `${exercise.id}: ${id}`).toBeDefined();
      }
    }
  });

  it("has unique ids", () => {
    const ids = POWER_CHORD_EXERCISES.map((exercise) => exercise.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("looks up an exercise by id", () => {
    expect(getPowerChordExercise("adjacent-frets")?.sequence).toEqual([
      "e-0",
      "e-1",
      "e-2",
      "e-3",
    ]);
  });

  it("returns undefined for an unknown id", () => {
    expect(getPowerChordExercise("nope")).toBeUndefined();
  });
});

describe("gallop exercises", () => {
  it("every chordId resolves to a real power chord", () => {
    for (const exercise of GALLOP_EXERCISES) {
      expect(getPowerChord(exercise.chordId), exercise.id).toBeDefined();
    }
  });

  it("the basic gallop cell is an eighth plus two sixteenths", () => {
    expect(getGallopExercise("basic-gallop")?.cell).toEqual([0.5, 0.25, 0.25]);
  });

  it("every cell sums to one beat", () => {
    for (const exercise of GALLOP_EXERCISES) {
      const sum = exercise.cell.reduce((a, b) => a + b, 0);
      expect(sum, exercise.id).toBeCloseTo(1);
    }
  });

  it("returns undefined for an unknown id", () => {
    expect(getGallopExercise("nope")).toBeUndefined();
  });
});
