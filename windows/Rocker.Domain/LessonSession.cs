// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Domain;
public sealed record LessonSession(Lesson Lesson, int StepIndex, bool NotePassed = false, bool Completed = false)
{
    public JsonElement CurrentStep => Lesson.Steps[StepIndex];
    public double ProgressPercent => (StepIndex + 1) * 100.0 / Lesson.Steps.Count;
    public bool IsFirst => StepIndex == 0;
    public bool IsLast => StepIndex == Lesson.Steps.Count - 1;
    public static LessonSession Start(Lesson lesson, int restoredStep = 0) => lesson.Steps.Count > 0 ? new(lesson, Math.Clamp(restoredStep, 0, lesson.Steps.Count - 1)) : throw new ArgumentException("Lekce nemá kroky.");
    // The original Next button is always available. Pitch confirmation is
    // feedback, not an invented requirement to finish the lesson.
    public LessonSession Next() => IsLast || Completed ? this : this with { StepIndex = StepIndex + 1, NotePassed = false };
    public LessonSession Previous() => IsFirst || Completed ? this : this with { StepIndex = StepIndex - 1, NotePassed = false };
    public LessonSession Finish() => IsLast ? this with { Completed = true } : this;
    public LessonSession ObserveNote(int? midi)
    {
        if (midi is null || CurrentStep.GetProperty("kind").GetString() != "note") return this;
        int target = CurrentStep.GetProperty("targetMidi").GetInt32();
        return MusicShapes.NoteAtFret(midi.Value, 0) == MusicShapes.NoteAtFret(target, 0) ? this with { NotePassed = true } : this;
    }
}
