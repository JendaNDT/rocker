// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json.Serialization;
namespace Rocker.Domain;
public enum SurgicalPhase { Run, Drill, Complete }
public sealed record SurgicalExercise(string Id, string Name, string Description, IReadOnlyList<int> Notes);
public sealed record SurgicalState(SurgicalExercise Exercise, [property: JsonIgnore] SurgicalPhase Phase,
    int Position, IReadOnlyList<int> ErrorCounts, int DrillStart, int DrillLength, int DrillPosition, int CleanReps)
{
    [JsonPropertyName("phase")] public string PhaseName => Phase.ToString().ToLowerInvariant();
}
public static class SurgicalEngine
{
    public const int DrillLength = 3, RequiredCleanReps = 3;
    public static SurgicalState Start(SurgicalExercise exercise)
    {
        if (exercise.Notes.Count is 0 or > 10000 || exercise.Notes.Any(note => note is < 0 or > 127))
            throw new ArgumentException("Cvičení musí obsahovat platné cílové tóny.", nameof(exercise));
        var owned = exercise with { Notes = Array.AsReadOnly(exercise.Notes.ToArray()) };
        return new SurgicalState(owned, SurgicalPhase.Run, 0, Array.AsReadOnly(new int[owned.Notes.Count]), 0, 0, 0, 0);
    }
    private static bool SamePitchClass(int a, int b) => ((a % 12) + 12) % 12 == ((b % 12) + 12) % 12;
    private static int WorstIndex(IReadOnlyList<int> errors)
    {
        int worst = -1, maximum = 0;
        for (int i = 0; i < errors.Count; ++i) if (errors[i] > maximum) { maximum = errors[i]; worst = i; }
        return worst;
    }
    public static SurgicalState FeedNote(SurgicalState state, int playedMidi)
    {
        if (state.Phase == SurgicalPhase.Run)
        {
            var expected = state.Exercise.Notes[state.Position];
            if (!SamePitchClass(playedMidi, expected))
            {
                var errors = state.ErrorCounts.ToArray(); ++errors[state.Position];
                return state with { ErrorCounts = Array.AsReadOnly(errors) };
            }
            int position = state.Position + 1;
            if (position < state.Exercise.Notes.Count) return state with { Position = position };
            var worst = WorstIndex(state.ErrorCounts);
            if (worst < 0) return state with { Position = position, Phase = SurgicalPhase.Complete };
            var length = Math.Min(DrillLength, state.Exercise.Notes.Count);
            var start = Math.Max(0, Math.Min(worst - 1, state.Exercise.Notes.Count - length));
            return state with { Position = position, Phase = SurgicalPhase.Drill, DrillStart = start, DrillLength = length, DrillPosition = 0, CleanReps = 0 };
        }
        if (state.Phase == SurgicalPhase.Drill)
        {
            var expected = state.Exercise.Notes[state.DrillStart + state.DrillPosition];
            if (!SamePitchClass(playedMidi, expected)) return state with { DrillPosition = 0, CleanReps = 0 };
            int position = state.DrillPosition + 1;
            if (position < state.DrillLength) return state with { DrillPosition = position };
            int reps = state.CleanReps + 1;
            return state with { DrillPosition = 0, CleanReps = reps, Phase = reps >= RequiredCleanReps ? SurgicalPhase.Complete : SurgicalPhase.Drill };
        }
        return state;
    }
    public static int? CurrentTarget(SurgicalState state) => state.Phase switch
    {
        SurgicalPhase.Run when state.Position < state.Exercise.Notes.Count => state.Exercise.Notes[state.Position],
        SurgicalPhase.Drill => state.Exercise.Notes[state.DrillStart + state.DrillPosition],
        _ => null
    };
    public static int[] DrillNotes(SurgicalState state) => state.Phase == SurgicalPhase.Drill ? state.Exercise.Notes.Skip(state.DrillStart).Take(state.DrillLength).ToArray() : [];
}
