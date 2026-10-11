// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Globalization;
namespace Rocker.Domain;
public sealed record ProgressData(IReadOnlyDictionary<string,double> DailyMinutes, IReadOnlyDictionary<string,double> BpmRecords, IReadOnlyList<string> UnlockedAchievements);
public sealed record BreakState(double PracticedMs = 0, double IdleMs = 0, bool Reminded = false);
public sealed record BreakAdvance(BreakState State, bool Remind);
public static class ProgressRules
{
    public static ProgressData Empty => new(new Dictionary<string,double>(), new Dictionary<string,double>(), Array.Empty<string>());
    private static Dictionary<string,double> NumberRecord(JsonElement value) => value.ValueKind != JsonValueKind.Object ? [] : value.EnumerateObject()
        .Where(item => item.Value.ValueKind == JsonValueKind.Number && item.Value.TryGetDouble(out var number) && double.IsFinite(number) && number > 0)
        .ToDictionary(item => item.Name, item => item.Value.GetDouble(), StringComparer.Ordinal);
    public static string[] StringArray(JsonElement value, bool nonEmpty = false) => value.ValueKind != JsonValueKind.Array ? [] : value.EnumerateArray()
        .Where(item => item.ValueKind == JsonValueKind.String && (!nonEmpty || item.GetString()!.Length > 0)).Select(item => item.GetString()!).Distinct(StringComparer.Ordinal).ToArray();
    public static ProgressData Sanitize(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return Empty;
        return new(NumberRecord(value.TryGetProperty("dailyMinutes", out var daily) ? daily : default), NumberRecord(value.TryGetProperty("bpmRecords", out var bpm) ? bpm : default), StringArray(value.TryGetProperty("unlockedAchievements", out var ids) ? ids : default));
    }
    public static int TotalPracticeDays(IReadOnlyDictionary<string,double> minutes) => minutes.Values.Count(value => double.IsFinite(value) && value > 0);
    public static int LongestStreak(IReadOnlyDictionary<string,double> minutes)
    {
        var days = minutes.Where(item => double.IsFinite(item.Value) && item.Value > 0).Select(item => DateOnly.TryParseExact(item.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? (DateOnly?)day : null)
            .Where(day => day.HasValue).Select(day => day!.Value.DayNumber).Distinct().Order().ToArray();
        int longest = 0, run = 0, previous = -2;
        foreach (var day in days) { run = day == previous + 1 ? run + 1 : 1; longest = Math.Max(longest, run); previous = day; }
        return longest;
    }
    public static string[] EvaluateAchievements(ContentCatalog catalog, ProgressData progress, IReadOnlyCollection<string> completed)
    {
        int streak = LongestStreak(progress.DailyMinutes), days = TotalPracticeDays(progress.DailyMinutes); double minutes = Practice.TotalMinutes(progress.DailyMinutes);
        var stageOne = catalog.Find("stages", "stage-1")!.Value.GetProperty("lessons").EnumerateArray().Select(item => item.GetProperty("id").GetString()!).ToArray();
        double best = progress.BpmRecords.Values.Where(double.IsFinite).DefaultIfEmpty(0).Max();
        bool Unlocked(string id) => id switch {
            "first-practice" => days >= 1, "streak-3" => streak >= 3, "streak-7" => streak >= 7, "streak-30" => streak >= 30,
            "minutes-60" => minutes >= 60, "minutes-600" => minutes >= 600, "first-lesson" => completed.Count >= 1,
            "first-power-chord" => completed.Contains("power-chord"), "stage-1-done" => stageOne.Length > 0 && stageOne.All(completed.Contains),
            "all-lessons" => completed.Count >= catalog.Lessons.Count, "tempo-120" => best >= 120,
            _ => throw new InvalidDataException("Neznámé pravidlo odměny.") };
        return catalog.Items("achievements").Select(item => item.GetProperty("id").GetString()!).Where(Unlocked).ToArray();
    }
    public static BreakAdvance AdvanceBreak(BreakState state, double deltaMs, bool practicing)
    {
        if (!double.IsFinite(deltaMs) || deltaMs <= 0) return new(state, false);
        if (practicing) { var time = state.PracticedMs + deltaMs; bool remind = !state.Reminded && time >= 45 * 60 * 1000; return new(new(time, 0, state.Reminded || remind), remind); }
        double idle = state.IdleMs + deltaMs;
        return new(idle >= 5 * 60 * 1000 ? new() : state with { IdleMs = idle }, false);
    }
}
