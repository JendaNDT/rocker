import { describe, expect, it } from "vitest";
import {
  isStageComplete,
  nextIncompleteLesson,
  overallCompletion,
  stageCompletion,
} from "./lessonEngine";
import { STAGES } from "./lessons";

const stage1 = STAGES[0];

describe("stageCompletion", () => {
  it("counts no lessons done with empty progress", () => {
    expect(stageCompletion(stage1, [])).toEqual({
      done: 0,
      total: stage1.lessons.length,
    });
  });

  it("counts completed lessons of the stage", () => {
    const ids = [stage1.lessons[0].id, stage1.lessons[1].id];
    expect(stageCompletion(stage1, ids).done).toBe(2);
  });
});

describe("isStageComplete", () => {
  it("is false until every lesson is done", () => {
    expect(isStageComplete(stage1, [stage1.lessons[0].id])).toBe(false);
  });

  it("is true when all lessons are done", () => {
    const ids = stage1.lessons.map((lesson) => lesson.id);
    expect(isStageComplete(stage1, ids)).toBe(true);
  });
});

describe("overallCompletion", () => {
  it("totals every lesson across all stages", () => {
    const total = STAGES.flatMap((stage) => stage.lessons).length;
    expect(overallCompletion(STAGES, [])).toEqual({ done: 0, total });
  });
});

describe("nextIncompleteLesson", () => {
  it("returns the very first lesson with empty progress", () => {
    expect(nextIncompleteLesson(STAGES, [])?.id).toBe(stage1.lessons[0].id);
  });

  it("skips completed lessons", () => {
    const next = nextIncompleteLesson(STAGES, [stage1.lessons[0].id]);
    expect(next?.id).toBe(stage1.lessons[1].id);
  });

  it("returns null when everything is done", () => {
    const allIds = STAGES.flatMap((stage) =>
      stage.lessons.map((lesson) => lesson.id),
    );
    expect(nextIncompleteLesson(STAGES, allIds)).toBeNull();
  });
});
