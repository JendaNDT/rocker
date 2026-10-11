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
var tracesPath = Path.Combine(Path.GetDirectoryName(args[0])!, "surgical-traces.json");
using var traces = JsonDocument.Parse(File.ReadAllText(tracesPath));
int surgicalStates = 0;
foreach (var trace in traces.RootElement.EnumerateArray())
{
    var expectedStates = trace.GetProperty("states").EnumerateArray().ToArray();
    var exercise = JsonSerializer.Deserialize<SurgicalExercise>(expectedStates[0].GetProperty("exercise"), options)!;
    var state = SurgicalEngine.Start(exercise);
    Check(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state, options), expectedStates[0]), "initial surgical reference state"); ++surgicalStates;
    int index = 1;
    foreach (var note in trace.GetProperty("notes").EnumerateArray())
    {
        state = SurgicalEngine.FeedNote(state, note.GetInt32());
        Check(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state, options), expectedStates[index++]), "surgical transition matches frozen PWA trace"); ++surgicalStates;
    }
    Check(index == expectedStates.Length, "all surgical states compared");
}
var firstExercise = JsonSerializer.Deserialize<SurgicalExercise>(content.Items("surgicalExercises").First(), options)!;
var firstState = SurgicalEngine.Start(firstExercise);
Check(SurgicalEngine.CurrentTarget(firstState) == firstExercise.Notes[0], "surgical current target");
Check(SurgicalEngine.FeedNote(firstState, firstExercise.Notes[0] + 12).Position == 1, "original octave-equivalent pitch-class matching preserved");
Check(firstState.Position == 0 && firstState.ErrorCounts.All(count => count == 0), "original state not mutated");
try { SurgicalEngine.Start(new SurgicalExercise("empty", "Empty", "", [])); throw new Exception("Empty exercise accepted"); }
catch (ArgumentException) { Check(true, "invalid exercise rejected before state creation"); }
Check(LessonRules.Completion(content.Lessons, [content.Lessons[0].Id, content.Lessons[0].Id]).Done == 1, "duplicate completions counted once");
Check(LessonRules.NextIncomplete(content.Lessons, content.Lessons.Select(item => item.Id).ToArray()) is null, "all lessons completed");
var noteLesson = content.Lessons.First(lesson => lesson.Steps.Any(step => step.GetProperty("kind").GetString() == "note"));
var session = LessonSession.Start(noteLesson);
while (session.CurrentStep.GetProperty("kind").GetString() != "note") session = session.Next();
var target = session.CurrentStep.GetProperty("targetMidi").GetInt32();
Check(session.ObserveNote(target + 12).NotePassed && !session.ObserveNote(target + 1).NotePassed, "lesson octave confirmation matches original NoteCheck");
Check(session.Next().StepIndex == Math.Min(session.StepIndex + 1, session.Lesson.Steps.Count - 1), "next lesson step does not require pitch confirmation");
Check(LessonSession.Start(noteLesson, 999).IsLast && LessonSession.Start(noteLesson, -1).IsFirst, "restored lesson position is bounded");
Check(LessonSession.Start(noteLesson).Finish().Completed == (noteLesson.Steps.Count == 1), "only final step can finish lesson");
var brokenLink = File.ReadAllText(args[1]).Replace("\"chordId\": \"Em\"", "\"chordId\": \"missing-chord\"");
try { ContentCatalog.Load(System.Text.Encoding.UTF8.GetBytes(brokenLink)); throw new Exception("Missing chord accepted"); }
catch (InvalidDataException) { Check(true, "lesson chord reference validated"); }
var brokenRhythm = File.ReadAllText(args[1]).Replace("\"e-0\"", "\"e-99\"");
try { ContentCatalog.Load(System.Text.Encoding.UTF8.GetBytes(brokenRhythm)); throw new Exception("Invalid chord shape accepted"); }
catch (InvalidDataException) { Check(true, "rhythm chord reference validated"); }
var extended = DomainReference.Run(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(args[0])!, "../../w06/domain-reference.json")), content);
Check(PalmMuteRules.Calibrate([double.NaN,2000,2400], [400,600,800]) is null, "nonfinite calibration rejected");
Check(ProgressRules.LongestStreak(new Dictionary<string,double> { ["invalid-date"] = 1 }) == 0, "invalid stored date cannot break history");
Check(MusicShapes.GetPowerChord("e-01") is null && MusicShapes.GetPowerChord("e-13") is null, "invalid chord shape ID rejected");
try { RhythmTrainers.Start(new PowerChordExercise("empty", "", "", [], 1, 70), true); throw new Exception("Empty sequence accepted"); }
catch (ArgumentException) { Check(true, "invalid rhythm exercise rejected"); }
Check(StudioRules.Place(new(4800,48,96,15,1,true),48000,48000,10000)==new TrackPlacement(4593,0),"studio accounts once for input, output, DSP and manual delay");
Check(StudioRules.Place(new(0,48,96,0,0,true),48000,96000,10000)==new TrackPlacement(0,144),"negative compensated offset trims the source at its own sample rate");
Check(StudioRules.Place(new(100,null,96,15,0,true),48000,48000,10000)==new TrackPlacement(100,0),"unknown driver delay is never fabricated as a measured offset");
var emPreview=TeachingPreview.Chord(content.Find("chords","Em")!.Value);Check(emPreview.Select(n=>n.Midi).SequenceEqual(new[]{40,47,52,55,59,64}),"lesson chord preview uses the actual played frets on six standard strings");
Check(Math.Abs(TeachingPreview.Chord(content.Find("chords","Em")!.Value,arpeggio:true).Last().Start-.9)<1e-9,"arpeggio previews schedule individual strings without UI timers");
foreach(var lesson in content.Lessons){var traversal=LessonSession.Start(lesson);for(int step=0;step<lesson.Steps.Count-1;++step)traversal=traversal.Next();Check(traversal.Finish().Completed,"every lesson can complete its original manual progression");}
var navigation = new NavigationSession();
navigation.Navigate(new("06", content.Lessons[0].Id)); navigation.Navigate(new("09", "spider")); navigation.Back();
Check(navigation.Current == new NavigationTarget("06", content.Lessons[0].Id), "back restores specific lesson task");
navigation.Forward(); navigation.Back(); navigation.Navigate(new("20"));
Check(!navigation.CanGoForward && navigation.Current.ViewId == "20", "new navigation clears forward history");
Check(WorkspaceViews.All.Select(view => view.Id).SequenceEqual(Enumerable.Range(1,32).Select(value => value.ToString("D2"))), "all 32 designed workspaces have stable IDs");
try { navigation.Navigate(new("unknown")); throw new Exception("Unknown view accepted"); }
catch (ArgumentException) { Check(navigation.Current.ViewId == "20", "invalid navigation preserves current context"); }
Check(new WorkState(true,false).RequiresConfirmation && new WorkState(false,true).RequiresConfirmation && !new WorkState(false,false).RequiresConfirmation, "close must confirm active recording or unsaved work");
var runtimeContent = ContentCatalog.Load(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(args[1])!, "runtime-catalog.json")));
Check(runtimeContent.Items("riffs").Count() == 14 && runtimeContent.Items("riffs").All(riff => !riff.TryGetProperty("artist", out _)), "runtime includes original drills and two new practice patterns");
Check(runtimeContent.ResolveRiff("smoke-on-the-water")?.GetProperty("id").GetString() == "chromatic-pulse" && runtimeContent.ResolveRiff("seven-nation-army")?.GetProperty("id").GetString() == "root-and-third", "legacy riff selections resolve to replacement IDs");
Console.WriteLine($"PASS: {extended.Cases} additional PWA cases and {extended.States} rhythm/palm-mute/break reference states.");
Console.WriteLine($"PASS: {compared} frozen PWA cases and {checks - compared} additional assertions, including {surgicalStates} frozen surgical states; native input capture and trainer UI remain later roadmap work.");

if (args.Length == 3) File.WriteAllText(args[2], JsonSerializer.Serialize(new { result = "passed", referenceCases = compared, boundaryChecks = checks - compared, contentGroups = 15, surgicalStatesCompared = surgicalStates, extendedReferenceCases = extended.Cases, extendedStatesCompared = extended.States }));
