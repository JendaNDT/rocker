// Pure helpers over the lesson content and the learner's progress.
// No DOM, no React — unit-tested in lessonEngine.test.ts.

import type { Lesson, Stage } from "@/lib/lessons";

export interface Completion {
  done: number;
  total: number;
}

export function isLessonComplete(
  lessonId: string,
  completedIds: readonly string[],
): boolean {
  return completedIds.includes(lessonId);
}

export function stageCompletion(
  stage: Stage,
  completedIds: readonly string[],
): Completion {
  const done = stage.lessons.filter((lesson) =>
    completedIds.includes(lesson.id),
  ).length;
  return { done, total: stage.lessons.length };
}

export function isStageComplete(
  stage: Stage,
  completedIds: readonly string[],
): boolean {
  return stage.lessons.every((lesson) => completedIds.includes(lesson.id));
}

export function overallCompletion(
  stages: readonly Stage[],
  completedIds: readonly string[],
): Completion {
  const lessons = stages.flatMap((stage) => stage.lessons);
  const done = lessons.filter((lesson) =>
    completedIds.includes(lesson.id),
  ).length;
  return { done, total: lessons.length };
}

/** First lesson the learner has not finished yet, or null when all are done. */
export function nextIncompleteLesson(
  stages: readonly Stage[],
  completedIds: readonly string[],
): Lesson | null {
  for (const stage of stages) {
    for (const lesson of stage.lessons) {
      if (!completedIds.includes(lesson.id)) return lesson;
    }
  }
  return null;
}
