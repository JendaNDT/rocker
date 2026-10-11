// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Rocker.Domain;
public sealed record Marker(string Id, double TimeSec, string Label);
public sealed record ChatMessage(string Role, string Text);
public sealed record PlanItem(string Id, string Title, string Detail, int Minutes, string Href)
{
    [JsonIgnore] public string? LessonId { get; init; }
    [JsonIgnore] public string? TrainerId { get; init; }
}
public sealed record DailyPlan(IReadOnlyList<PlanItem> Items, int TotalMinutes);
public static partial class Practice
{
    private static readonly HashSet<string> Routes = ["/tuner", "/metronome", "/practice", "/metal", "/youtube", "/looper", "/studio", "/games", "/amp"];
    private static readonly int[] Standard = [40, 45, 50, 55, 59, 64];
    public static bool IsPracticeRoute(string route) => Routes.Contains(route.Length > 1 && route.EndsWith('/') ? route[..^1] : route);
    public static double SwingedTickDuration(int tick, double duration, int subdivision, double swing)
    {
        if (subdivision is not (2 or 4)) return duration;
        var ratio = double.IsFinite(swing) ? Math.Clamp(swing, 0.5, 0.75) : 0.5;
        return duration * (tick % 2 == 1 ? 2 * (1 - ratio) : 2 * ratio);
    }
    public static int[] ChordMidiNotes(JsonElement chord)
    {
        var frets = chord.GetProperty("frets").EnumerateArray().ToArray();
        if (frets.Length != 6) throw new InvalidDataException("Akord musí mít šest strun.");
        var notes = new List<int>();
        for (int i = 0; i < frets.Length; ++i)
        {
            if (frets[i].ValueKind == JsonValueKind.String && frets[i].GetString() == "x") continue;
            var fret = frets[i].GetInt32();
            if (fret is < 0 or > 36) throw new InvalidDataException("Pražec akordu není platný.");
            notes.Add(Standard[i] + fret);
        }
        return notes.ToArray();
    }
    public static int CurrentStreak(IReadOnlyDictionary<string, double> minutes, DateOnly today)
    {
        bool Practiced(DateOnly day) => minutes.TryGetValue(day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), out var value) && double.IsFinite(value) && value > 0;
        var cursor = today;
        if (!Practiced(cursor)) { if (cursor == DateOnly.MinValue) return 0; cursor = cursor.AddDays(-1); }
        int count = 0;
        while (Practiced(cursor)) { ++count; if (cursor == DateOnly.MinValue) break; cursor = cursor.AddDays(-1); }
        return count;
    }
    public static double TotalMinutes(IReadOnlyDictionary<string, double> minutes) => minutes.Values.Where(double.IsFinite).Sum();
    public static double[] CellOnsetsSeconds(IReadOnlyList<double> cell, double bpm)
    {
        if (!double.IsFinite(bpm) || bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
        var secondsPerBeat = 60.0 / bpm;
        var onsets = new double[cell.Count]; double cursor = 0;
        for (int i = 0; i < cell.Count; ++i)
        {
            if (!double.IsFinite(cell[i]) || cell[i] <= 0) throw new ArgumentException("Rytmická buňka obsahuje neplatnou délku.", nameof(cell));
            onsets[i] = cursor * secondsPerBeat; cursor += cell[i];
        }
        return onsets;
    }
    [GeneratedRegex("[\\\\/:*?\"<>|]", RegexOptions.CultureInvariant)] private static partial Regex IllegalFileChars();
    public static string RecordingFileName(string name, string mime)
    {
        var value = IllegalFileChars().Replace(name, "_").Trim();
        if (value.Length == 0) value = "nahravka";
        var type = mime.ToLowerInvariant();
        return value + (type.Contains("ogg") ? ".ogg" : type.Contains("mp4") || type.Contains("m4a") ? ".m4a" : type.Contains("mpeg") ? ".mp3" : type.Contains("wav") ? ".wav" : ".webm");
    }
    public static Marker[] SortMarkers(IEnumerable<Marker> markers) => markers.OrderBy(marker => marker.TimeSec).ToArray();
    public static ChatMessage[] SanitizeChatHistory(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return [];
        var result = new List<ChatMessage>();
        foreach (var item in value.EnumerateArray())
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String && role.GetString() is "user" or "model" &&
                item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String) result.Add(new ChatMessage(role.GetString()!, text.GetString()!));
        return result.ToArray();
    }
    public static DailyPlan GenerateDailyPlan(ContentCatalog content, IReadOnlyCollection<string> completed, DateOnly today)
    {
        var lesson = LessonRules.NextIncomplete(content.Lessons, completed);
        var focus = (int)today.DayOfWeek % 3;
        PlanItem[] items = [
            new("tune", "Naladění", "Nalaď kytaru, ať cvičíš na čistém zvuku.", 2, "/tuner"),
            new("warmup", "Rozehřátí", "Chromatika přes všechny prsty v pomalém tempu s metronomem.", 3, "/metronome"),
            new("lesson", "Lekce", lesson is null ? "Všechny lekce máš hotové — zopakuj si svou oblíbenou." : $"Pokračuj lekcí „{lesson.Title}\".", 7, "/practice") { LessonId = lesson?.Id },
            focus == 1 ? new("focus", "YouTube cvičení", "Pusť si tutoriál nebo skladbu a procvič jednu pasáž ve smyčce.", 5, "/youtube") :
            focus == 2 ? new("focus", "Přesný trénink", "Najdi chybové místo a vyčisti ho pár soustředěnými repeticemi.", 5, "/practice") { TrainerId = "surgical" } :
            new("focus", "Riff trainer", "Vyber si riff a projeď ho zpomalený, pak přidej tempo.", 5, "/practice") { TrainerId = "riff" }
        ];
        return new DailyPlan(Array.AsReadOnly(items), items.Sum(item => item.Minutes));
    }
}
