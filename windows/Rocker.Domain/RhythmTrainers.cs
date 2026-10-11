// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;
public sealed record PowerChordExercise(string Id, string Name, string Description, IReadOnlyList<string> Sequence, int BeatsPerChord, double StartBpm);
public sealed record PowerChordState(PowerChordExercise Exercise, int Bpm, long ElapsedBeats, long RoundsCompleted, bool SpeedUpEnabled);
public sealed record GallopExercise(string Id, string Name, string Description, IReadOnlyList<double> Cell, string ChordId, double StartBpm);
public sealed record GallopState(GallopExercise Exercise, int Bpm, long CellsCompleted, long RoundsCompleted, bool SpeedUpEnabled);
public static class RhythmTrainers
{
    public const int CellsPerRound = 8;
    public static PowerChordState Start(PowerChordExercise exercise, bool speedUp)
    {
        if (exercise.Sequence.Count is 0 or > 256 || exercise.Sequence.Any(id => MusicShapes.GetPowerChord(id) is null) || exercise.BeatsPerChord is < 1 or > 64)
            throw new ArgumentException("Neplatná sekvence power chordů.", nameof(exercise));
        return new(exercise with { Sequence = Array.AsReadOnly(exercise.Sequence.ToArray()) }, Music.ClampBpm(exercise.StartBpm), 0, 0, speedUp);
    }
    public static int RoundBeats(PowerChordExercise exercise) => checked(exercise.Sequence.Count * exercise.BeatsPerChord);
    public static int ActiveChordIndex(PowerChordState state) => (int)(state.ElapsedBeats % RoundBeats(state.Exercise)) / state.Exercise.BeatsPerChord;
    public static string ActiveChordId(PowerChordState state) => state.Exercise.Sequence[ActiveChordIndex(state)];
    public static string NextChordId(PowerChordState state) => state.Exercise.Sequence[(ActiveChordIndex(state) + 1) % state.Exercise.Sequence.Count];
    public static double RoundProgress(PowerChordState state) => (double)(state.ElapsedBeats % RoundBeats(state.Exercise)) / RoundBeats(state.Exercise);
    public static PowerChordState AdvanceBeat(PowerChordState state)
    {
        long beats = checked(state.ElapsedBeats + 1);
        bool round = beats % RoundBeats(state.Exercise) == 0;
        return state with { ElapsedBeats = beats, RoundsCompleted = state.RoundsCompleted + (round ? 1 : 0), Bpm = round && state.SpeedUpEnabled ? Music.ClampBpm(state.Bpm * 1.05) : state.Bpm };
    }
    public static GallopState Start(GallopExercise exercise, bool speedUp)
    {
        if (exercise.Cell.Count is 0 or > 64 || exercise.Cell.Any(length => !double.IsFinite(length) || length <= 0) || MusicShapes.GetPowerChord(exercise.ChordId) is null)
            throw new ArgumentException("Neplatná rytmická buňka.", nameof(exercise));
        return new(exercise with { Cell = Array.AsReadOnly(exercise.Cell.ToArray()) }, Music.ClampBpm(exercise.StartBpm), 0, 0, speedUp);
    }
    public static double RoundProgress(GallopState state) => (double)(state.CellsCompleted % CellsPerRound) / CellsPerRound;
    public static GallopState AdvanceCell(GallopState state)
    {
        long cells = checked(state.CellsCompleted + 1); bool round = cells % CellsPerRound == 0;
        return state with { CellsCompleted = cells, RoundsCompleted = state.RoundsCompleted + (round ? 1 : 0), Bpm = round && state.SpeedUpEnabled ? Music.ClampBpm(state.Bpm * 1.05) : state.Bpm };
    }
    public static double CellDurationSeconds(IReadOnlyList<double> cell, double bpm)
    {
        _ = Practice.CellOnsetsSeconds(cell, bpm); // Share validation and tempo semantics.
        return cell.Sum() * 60 / bpm;
    }
}
