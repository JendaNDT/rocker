// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using System.Text.Json;
internal static class DomainReference
{
    internal static (int Cases, int States) Run(string path, ContentCatalog catalog)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        T Read<T>(JsonElement value) => JsonSerializer.Deserialize<T>(value, options)!;
        void Compare(object? actual, JsonElement expected, string label)
        {
            if (!Equivalent(JsonSerializer.SerializeToElement(actual, options), expected)) throw new Exception("PWA/native comparison failed: " + label);
        }
        int cases = 0, states = 0;
        foreach (var item in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var a = item.GetProperty("args"); var name = item.GetProperty("function").GetString();
            object? actual = name switch
            {
                "cellOnsetsSeconds" => Practice.CellOnsetsSeconds(Read<double[]>(a[0]), a[1].GetDouble()),
                "cellDurationSeconds" => RhythmTrainers.CellDurationSeconds(Read<double[]>(a[0]), a[1].GetDouble()),
                "calibrate" => PalmMuteRules.Calibrate(Read<double[]>(a[0]), Read<double[]>(a[1])),
                "classifyHit" => PalmMuteRules.ClassifyHit(a[0].GetDouble(), Read<PalmMuteCalibration>(a[1])),
                "getPowerChord" => MusicShapes.GetPowerChord(a[0].GetString()!, Read<int[]>(a[1])),
                "powerChordMidiNotes" => MusicShapes.PowerChordMidiNotes(Read<PowerChord>(a[0])),
                "noteAtFret" => MusicShapes.NoteAtFret(a[0].GetInt32(), a[1].GetInt32()),
                "scaleOffset" => MusicShapes.ScaleOffset(Read<int[]>(a[0].GetProperty("intervals")), a[1].GetInt32(), a[2].GetInt32()),
                "pickTarget" => MusicShapes.PickIndex(12, a[0].ValueKind == JsonValueKind.Null ? null : a[0].GetInt32(), a[1].GetDouble()),
                "pickIndex" => MusicShapes.PickIndex(a[0].GetInt32(), a[1].ValueKind == JsonValueKind.Null ? null : a[1].GetInt32(), a[2].GetDouble()),
                "pickRoot" => MusicShapes.PickRoot(a[0].GetDouble()),
                "sanitizeProgress" => ProgressRules.Sanitize(a[0]),
                "sanitizeCompletedLessonIds" => ProgressRules.StringArray(a[0], true),
                "overallCompletion" => LessonRules.Completion(catalog.Lessons, Read<string[]>(a[1])),
                "stageCompletion" => LessonRules.Completion(Read<Lesson[]>(a[0].GetProperty("lessons")), Read<string[]>(a[1])),
                "isStageComplete" => Read<Lesson[]>(a[0].GetProperty("lessons")).All(lesson => LessonRules.IsLessonComplete(lesson.Id, Read<string[]>(a[1]))),
                "nextIncompleteLesson" => NextLessonElement(a[0], Read<string[]>(a[1])),
                "longestStreak" => ProgressRules.LongestStreak(Read<Dictionary<string,double>>(a[0])),
                "totalPracticeDays" => ProgressRules.TotalPracticeDays(Read<Dictionary<string,double>>(a[0])),
                "sanitizeMetronomeConfig" => MetronomeRules.Sanitize(a[0]),
                "isSilentBar" => MetronomeRules.IsSilentBar(a[0].GetInt64(), Read<GapConfig>(a[1])),
                "nextRampBpm" => MetronomeRules.NextRampBpm(a[0].GetInt32(), Read<RampConfig>(a[1])),
                "sanitizeAmpSettings" => AmpRules.Sanitize(a[0]),
                "sanitizeUserPresets" => AmpRules.SanitizeUserPresets(a[0]),
                "cycleAccent" => MetronomeRules.CycleAccent(a[0].GetString()!),
                "evaluateAchievements" => ProgressRules.EvaluateAchievements(catalog, Read<ProgressData>(a[0]), Read<string[]>(a[1])),
                _ => throw new Exception("Unported W06 comparison: " + name)
            };
            Compare(actual, item.GetProperty("expected"), name!); ++cases;
        }
        foreach (var trace in doc.RootElement.GetProperty("rhythm").EnumerateArray())
        {
            bool speed = trace.GetProperty("speedUp").GetBoolean();
            if (trace.GetProperty("kind").GetString() == "power")
            {
                var state = RhythmTrainers.Start(Read<PowerChordExercise>(trace.GetProperty("exercise")), speed);
                foreach (var expected in trace.GetProperty("snapshots").EnumerateArray())
                {
                    Compare(new { state, active = RhythmTrainers.ActiveChordId(state), next = RhythmTrainers.NextChordId(state), progress = RhythmTrainers.RoundProgress(state) }, expected, "power-chord state"); ++states;
                    state = RhythmTrainers.AdvanceBeat(state);
                }
            }
            else
            {
                var state = RhythmTrainers.Start(Read<GallopExercise>(trace.GetProperty("exercise")), speed);
                foreach (var expected in trace.GetProperty("snapshots").EnumerateArray())
                {
                    Compare(new { state, progress = RhythmTrainers.RoundProgress(state) }, expected, "gallop state"); ++states;
                    state = RhythmTrainers.AdvanceCell(state);
                }
            }
        }
        foreach (var trace in doc.RootElement.GetProperty("palm").EnumerateArray())
        {
            var state = PalmMuteRules.Start(Read<PalmMuteDrill>(trace.GetProperty("drill")));
            var hits = Read<string[]>(trace.GetProperty("hits")); int index = 0;
            foreach (var expected in trace.GetProperty("snapshots").EnumerateArray())
            {
                Compare(new { state, complete = PalmMuteRules.IsComplete(state), expected = PalmMuteRules.ExpectedTechnique(state), score = PalmMuteRules.Score(state) }, expected, "palm-mute state"); ++states;
                if (index < hits.Length) state = PalmMuteRules.LogHit(state, hits[index++]);
            }
        }
        var breaks = doc.RootElement.GetProperty("breaks"); var ticks = breaks.GetProperty("ticks");
        var breakState = new BreakState(); int tickIndex = 0;
        foreach (var expected in breaks.GetProperty("states").EnumerateArray())
        {
            var tick = ticks[tickIndex++]; var advance = ProgressRules.AdvanceBreak(breakState, tick[0].GetDouble(), tick[1].GetBoolean());
            Compare(advance, expected, "break reminder transition"); breakState = advance.State; ++states;
        }
        return (cases, states);
    }
    private static JsonElement? NextLessonElement(JsonElement stages, string[] completed)
    {
        // Compare the original content payload as well as the next-lesson rule.
        foreach (var stage in stages.EnumerateArray()) foreach (var lesson in stage.GetProperty("lessons").EnumerateArray())
            if (LessonRules.NextIncomplete([new Lesson(lesson.GetProperty("id").GetString()!, "", 1, [])], completed) is not null) return lesson;
        return null;
    }
    private static bool Equivalent(JsonElement actual, JsonElement expected)
    {
        if (actual.ValueKind != expected.ValueKind) return false;
        if (actual.ValueKind == JsonValueKind.Number)
        {
            double a = actual.GetDouble(), b = expected.GetDouble();
            return Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Abs(b));
        }
        if (actual.ValueKind == JsonValueKind.Array) return actual.GetArrayLength() == expected.GetArrayLength() && actual.EnumerateArray().Zip(expected.EnumerateArray()).All(pair => Equivalent(pair.First, pair.Second));
        if (actual.ValueKind == JsonValueKind.Object) return actual.EnumerateObject().Count() == expected.EnumerateObject().Count() && actual.EnumerateObject().All(item => expected.TryGetProperty(item.Name, out var other) && Equivalent(item.Value, other));
        return JsonElement.DeepEquals(actual, expected);
    }
}
