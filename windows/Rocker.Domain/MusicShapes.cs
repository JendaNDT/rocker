// SPDX-License-Identifier: AGPL-3.0-only
using System.Globalization;
namespace Rocker.Domain;
public sealed record PowerChord(string Id, string Name, string RootName, int RootStringIndex, int RootFret, int RootMidi);
public static class MusicShapes
{
    private static readonly int[] Standard = [40,45,50,55,59,64];
    public static int NoteAtFret(int openMidi, int fret) => ((openMidi + fret) % 12 + 12) % 12;
    public static int? ScaleOffset(IReadOnlyList<int> intervals, int root, int pitch)
    {
        var relative = ((pitch - root) % 12 + 12) % 12;
        return intervals.Contains(relative) ? relative : null;
    }
    public static PowerChord PowerChordAt(int rootString, int fret, IReadOnlyList<int>? tuning = null)
    {
        tuning ??= Standard;
        if (rootString is not (0 or 1) || fret is < 0 or > 12 || tuning.Count != 6 || tuning.Any(note => note is < 0 or > 108)) throw new ArgumentException("Neplatná pozice nebo ladění power chordu.");
        int midi = tuning[rootString] + fret; string name = Music.PitchClassName(midi);
        return new((rootString == 0 ? "e-" : "a-") + fret.ToString(CultureInfo.InvariantCulture), name + "5", name, rootString, fret, midi);
    }
    public static PowerChord? GetPowerChord(string id, IReadOnlyList<int>? tuning = null)
    {
        if (id.Length is < 3 or > 4 || id[1] != '-' || id[0] is not ('e' or 'a') || !int.TryParse(id.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out int fret) || fret is < 0 or > 12 || id[2..] != fret.ToString(CultureInfo.InvariantCulture)) return null;
        return PowerChordAt(id[0] == 'e' ? 0 : 1, fret, tuning);
    }
    public static int[] PowerChordMidiNotes(PowerChord chord) => [chord.RootMidi, chord.RootMidi + 7, chord.RootMidi + 12];
    // Inject the random value to keep exercises reproducible in checks.
    public static int PickIndex(int count, int? previous, double random)
    {
        if (count < 1 || !double.IsFinite(random) || random is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(random));
        int pick = Math.Min(count - 1, (int)Math.Floor(random * count));
        return previous == pick && count > 1 ? (pick + 1) % count : pick;
    }
    public static int PickRoot(double random) => 48 + PickIndex(12, null, random);
}
