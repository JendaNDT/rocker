// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Domain;
internal static class ContentValidation
{
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static void Check(IReadOnlyDictionary<string, JsonElement> groups)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        T Read<T>(JsonElement value) => JsonSerializer.Deserialize<T>(value, options)!;
        foreach (var pair in new[] { ("surgicalExercises", "notes"), ("powerChordExercises", "sequence"), ("gallopExercises", "cell"), ("palmMuteDrills", "pattern") })
            foreach (var item in groups[pair.Item1].EnumerateArray()) Require(item.GetProperty(pair.Item2).ValueKind == JsonValueKind.Array, "Cvičení neobsahuje platnou sekvenci.");
        foreach (var item in groups["chords"].EnumerateArray())
        {
            _ = Practice.ChordMidiNotes(item);
            Require(item.GetProperty("fingers").GetArrayLength() == 6 && item.GetProperty("fingers").EnumerateArray().All(f => f.ValueKind == JsonValueKind.Null || f.GetInt32() is >= 1 and <= 4), "Neplatné prstoklady akordu.");
            Require(item.GetProperty("baseFret").GetInt32() is >= 1 and <= 24, "Neplatná výchozí pozice akordu.");
        }
        foreach (var item in groups["scales"].EnumerateArray())
        {
            var intervals = Read<int[]>(item.GetProperty("intervals"));
            Require(intervals.Length is >= 1 and <= 12 && intervals.Contains(0) && intervals.Distinct().Count() == intervals.Length && intervals.All(n => n is >= 0 and <= 11), "Neplatné intervaly stupnice.");
        }
        var tunings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in groups["tunings"].EnumerateArray())
        {
            tunings.Add(item.GetProperty("id").GetString()!);
            Require(!string.IsNullOrWhiteSpace(item.GetProperty("label").GetString()) && item.GetProperty("strings").GetArrayLength() == 6 && item.GetProperty("strings").EnumerateArray().All(s => s.GetProperty("midi").GetInt32() is >= 0 and <= 127 && !string.IsNullOrEmpty(s.GetProperty("note").GetString())), "Neplatné ladění.");
        }
        foreach (var item in groups["surgicalExercises"].EnumerateArray()) _ = SurgicalEngine.Start(Read<SurgicalExercise>(item));
        foreach (var item in groups["powerChordExercises"].EnumerateArray()) _ = RhythmTrainers.Start(Read<PowerChordExercise>(item), false);
        foreach (var item in groups["gallopExercises"].EnumerateArray()) _ = RhythmTrainers.Start(Read<GallopExercise>(item), false);
        foreach (var item in groups["palmMuteDrills"].EnumerateArray()) _ = PalmMuteRules.Start(Read<PalmMuteDrill>(item));
        foreach (var item in groups["riffs"].EnumerateArray())
        {
            Require(tunings.Contains(item.GetProperty("tuning").GetString()!) && item.GetProperty("bpm").GetInt32() > 0 && item.GetProperty("notes").GetArrayLength() is >= 1 and <= 10000, "Neplatný riff nebo jeho ladění.");
            foreach (var note in item.GetProperty("notes").EnumerateArray())
                Require(note.GetProperty("stringIndex").GetInt32() is >= 0 and <= 5 && note.GetProperty("fret").GetInt32() is >= 0 and <= 24 && note.GetProperty("start").GetDouble() >= 0 && note.GetProperty("duration").GetDouble() > 0 && note.GetProperty("technique").GetString() is "normal" or "palm-mute" or "slide" or "bend" or "hammer-on" or "pull-off", "Neplatná nota riffu.");
        }
        foreach (var item in groups["ampPresets"].EnumerateArray())
            Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(AmpRules.Sanitize(item.GetProperty("settings")), options), item.GetProperty("settings")), "Tovární aparát obsahuje neplatné parametry.");
        foreach (var item in groups["metronomePresets"].EnumerateArray())
        {
            var normalized = JsonSerializer.SerializeToElement(MetronomeRules.Sanitize(item), options);
            foreach (var property in normalized.EnumerateObject()) Require(item.TryGetProperty(property.Name, out var value) && JsonElement.DeepEquals(value, property.Value), "Tovární metronom obsahuje neplatné parametry.");
        }
        foreach (var item in groups["timeSignatures"].EnumerateArray())
            Require(item.GetProperty("beatsPerBar").GetInt32() is >= 1 and <= 16 && item.GetProperty("defaultAccents").GetArrayLength() == item.GetProperty("beatsPerBar").GetInt32(), "Neplatný takt.");
    }
}
