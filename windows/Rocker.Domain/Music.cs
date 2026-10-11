// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;

public enum NoteNotation { English, German }
public sealed record NoteInfo(int Midi, string Name, int Octave, string Label, int Cents);

public static class Music
{
    private static readonly string[] English = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly string[] German = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "B", "H"];
    // Match JavaScript rounding at negative half values; .NET's default rounds
    // ties to even and would change pitch/tempo behaviour during migration.
    private static int Round(double value) => checked((int)Math.Floor(value + 0.5));
    public static double MidiToFrequency(double midi) => 440 * Math.Pow(2, (midi - 69) / 12);
    public static string PitchClassName(int pitchClass, NoteNotation notation = NoteNotation.English) =>
        (notation == NoteNotation.German ? German : English)[((pitchClass % 12) + 12) % 12];
    public static string MidiToLabel(int midi, NoteNotation notation = NoteNotation.English) =>
        $"{PitchClassName(midi, notation)}{(int)Math.Floor(midi / 12.0) - 1}";
    public static NoteInfo? FrequencyToNote(double frequency)
    {
        if (!double.IsFinite(frequency) || frequency <= 0) return null;
        var exact = 69 + 12 * Math.Log2(frequency / 440);
        if (!double.IsFinite(exact)) return null;
        var midi = Round(exact);
        var name = PitchClassName(midi);
        var octave = (int)Math.Floor(midi / 12.0) - 1;
        return new NoteInfo(midi, name, octave, $"{name}{octave}", Round((exact - midi) * 100));
    }
    public static int CentsBetween(double frequency, double target)
    {
        if (!double.IsFinite(frequency) || !double.IsFinite(target) || frequency <= 0 || target <= 0) return 0;
        var value = 1200 * (Math.Log2(frequency) - Math.Log2(target));
        return double.IsFinite(value) ? Round(value) : 0;
    }
    public static string LocalizeChordName(string name, NoteNotation notation) => notation != NoteNotation.German ? name :
        name.StartsWith("Bb", StringComparison.Ordinal) ? "B" + name[2..] :
        name.StartsWith('B') ? "H" + name[1..] : name;
    public static int ClampBpm(double value) => !double.IsFinite(value) ? 40 : (int)Math.Clamp(Math.Floor(value + 0.5), 40, 240);
    public static int? BpmFromTaps(IReadOnlyList<double> timestamps)
    {
        if (timestamps.Count < 2 || timestamps.Any(value => !double.IsFinite(value))) return null;
        double total = 0;
        for (int i = 1; i < timestamps.Count; ++i) total += timestamps[i] - timestamps[i - 1];
        var average = total / (timestamps.Count - 1);
        var bpm = 60000 / average;
        return average > 0 && double.IsFinite(bpm) && bpm <= int.MaxValue ? Round(bpm) : null;
    }
}
