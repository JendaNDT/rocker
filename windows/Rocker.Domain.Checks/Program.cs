// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using System.Text.Json;
if (args.Length is < 2 or > 3) throw new ArgumentException("Supply the frozen W01 domain-cases.json and native catalog paths.");
var content = ContentCatalog.Load(File.ReadAllBytes(args[1]));
var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
using var document = JsonDocument.Parse(File.ReadAllText(args[0]));
int compared = 0, checks = 0;
void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
foreach (var item in document.RootElement.EnumerateArray())
{
    var arguments = item.GetProperty("args");
    var name = item.GetProperty("function").GetString();
    object? result;
    switch (name)
    {
        case "midiToFrequency": result = Music.MidiToFrequency(arguments[0].GetDouble()); break;
        case "frequencyToNote": result = Music.FrequencyToNote(arguments[0].GetDouble()); break;
        case "midiToLabelIn": result = Music.MidiToLabel(arguments[0].GetInt32(), arguments[1].GetString() == "german" ? NoteNotation.German : NoteNotation.English); break;
        case "clampBpm": result = Music.ClampBpm(arguments[0].GetDouble()); break;
        case "bpmFromTaps": result = Music.BpmFromTaps(arguments[0].EnumerateArray().Select(value => value.GetDouble()).ToArray()); break;
        case "swingedTickDuration": result = Practice.SwingedTickDuration(arguments[0].GetInt32(), arguments[1].GetDouble(), arguments[2].GetInt32(), arguments[3].GetDouble()); break;
        case "isPracticeRoute": result = Practice.IsPracticeRoute(arguments[0].GetString()!); break;
        case "chordMidiNotes": result = Practice.ChordMidiNotes(arguments[0]); break;
        case "currentStreak": result = Practice.CurrentStreak(JsonSerializer.Deserialize<Dictionary<string, double>>(arguments[0])!, DateOnly.ParseExact(arguments[1].GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)); break;
        case "totalMinutes": result = Practice.TotalMinutes(JsonSerializer.Deserialize<Dictionary<string, double>>(arguments[0])!); break;
        case "generateDailyPlan": result = Practice.GenerateDailyPlan(content, JsonSerializer.Deserialize<string[]>(arguments[0].GetProperty("completedLessonIds"))!, DateOnly.ParseExact(arguments[0].GetProperty("dayKey").GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)); break;
        case "recordingFileName": result = Practice.RecordingFileName(arguments[0].GetString()!, arguments[1].GetString()!); break;
        case "sortMarkers": result = Practice.SortMarkers(JsonSerializer.Deserialize<Marker[]>(arguments[0], options)!); break;
        case "sanitizeChatHistory": result = Practice.SanitizeChatHistory(arguments[0]); break;
        case "cellOnsetsSeconds": result = Practice.CellOnsetsSeconds(arguments[0].EnumerateArray().Select(value => value.GetDouble()).ToArray(), arguments[1].GetDouble()); break;
        default: throw new Exception("Unported frozen reference function: " + name);
    }
    var expected = item.GetProperty("expected");
    var actual = JsonSerializer.SerializeToElement(result, options);
    if (expected.ValueKind == JsonValueKind.Number && actual.ValueKind == JsonValueKind.Number)
        Check(Math.Abs(expected.GetDouble() - actual.GetDouble()) < 1e-9, item.GetProperty("id").GetString()!);
    else Check(JsonElement.DeepEquals(expected, actual), item.GetProperty("id").GetString()!);
    ++compared;
}
Check(compared == 69, "all frozen reference cases were executed");
Check(Music.FrequencyToNote(double.NaN) is null && Music.FrequencyToNote(double.PositiveInfinity) is null && Music.FrequencyToNote(-1) is null, "invalid frequency rejected");
Check(Music.PitchClassName(-1, NoteNotation.German) == "H", "negative pitch class normalized");
Check(Music.LocalizeChordName("Bb7", NoteNotation.German) == "B7" && Music.LocalizeChordName("Bm", NoteNotation.German) == "Hm", "Czech chord naming");
Check(Music.ClampBpm(double.NaN) == 40 && Music.BpmFromTaps([1, 1]) is null && Music.BpmFromTaps([0, double.NaN]) is null, "invalid tempo input rejected");
Check(Music.CentsBetween(double.PositiveInfinity, 440) == 0, "invalid cents input sanitized");
Check(content.Lessons.Count > 0 && content.Items("chords").Count() == 26, "content groups and lesson links");
Check(Practice.GenerateDailyPlan(content, [], new DateOnly(2026,10,10)).Items[2].LessonId == content.Lessons[0].Id, "native plan preserves direct lesson target");
var corrupted = File.ReadAllText(args[1]).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
try { ContentCatalog.Load(System.Text.Encoding.UTF8.GetBytes(corrupted)); throw new Exception("Future catalog accepted"); }
catch (InvalidDataException) { Check(true, "future content schema rejected"); }
var duplicate = File.ReadAllText(args[1]).Replace("\"id\": \"stage-2\"", "\"id\": \"stage-1\"");
try { ContentCatalog.Load(System.Text.Encoding.UTF8.GetBytes(duplicate)); throw new Exception("Duplicate content ID accepted"); }
catch (InvalidDataException) { Check(true, "duplicate ID rejected"); }
Console.WriteLine($"PASS: {compared} frozen PWA cases and {checks - compared} boundary assertions; lesson/trainer state machines remain W06 work.");

if (args.Length == 3) File.WriteAllText(args[2], JsonSerializer.Serialize(new { result = "passed", referenceCases = compared, boundaryChecks = checks - compared, contentGroups = 15, stateMachinesPorted = false }));
