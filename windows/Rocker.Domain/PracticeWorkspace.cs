// SPDX-License-Identifier: AGPL-3.0-only
using System.Globalization;
using System.Text;
using System.Text.Json;
namespace Rocker.Domain;

public sealed record PracticeSetup(string Id, string Name, NavigationTarget Target, AmpSettings Amp,
    bool AmpEnabled, MetronomeConfig Metronome, string TuningId, string? SongId = null,
    double Speed = 1, double Position = 0, double LoopStart = 0, double LoopEnd = 0, bool LoopEnabled = false, string? RecordingId = null);
public sealed record PracticeWorkspace(IReadOnlyList<PracticeSetup> Setups, IReadOnlyList<string> Favorites);
public sealed record SearchEntry(string Key, string Title, string Category, NavigationTarget Target, string? ResourceId = null)
{
    public string Label => Title + " · " + Category;
}
public static class PracticeWorkspaceRules
{
    public static PracticeWorkspace Empty => new([], []);
    public static PracticeSetup Validate(PracticeSetup value)
    {
        static bool Range(double n, double min, double max) => double.IsFinite(n) && n >= min && n <= max;
        if (value is null || !Guid.TryParse(value.Id, out _) || string.IsNullOrWhiteSpace(value.Name) || value.Name.Length > 120 || value.Name.Any(char.IsControl)
            || value.Target is null || value.Amp is null || value.Metronome is null || !UserData.ValidId(value.TuningId)
            || value.SongId is not null && !Guid.TryParse(value.SongId, out _) || value.RecordingId is not null && (!UserData.ValidId(value.RecordingId) || value.SongId is null) || !Range(value.Speed, .25, 2)
            || !Range(value.Position, 0, 86400) || !Range(value.LoopStart, 0, 86400) || !Range(value.LoopEnd, 0, 86400)
            || value.LoopEnabled && (value.SongId is null || value.LoopEnd <= value.LoopStart))
            throw new InvalidDataException("Sestava cvičení není platná.");
        WorkspaceViews.Validate(value.Target);
        // Canonical sanitizers keep saved sound parameters inside the native ranges.
        var amp = AmpRules.Sanitize(JsonSerializer.SerializeToElement(value.Amp, UserData.Json)) with { PresetId = UserData.ValidId(value.Amp.PresetId) ? value.Amp.PresetId : null };
        var metronome = MetronomeRules.Sanitize(JsonSerializer.SerializeToElement(value.Metronome, UserData.Json)) with { VisualMode = value.Metronome.VisualMode is "dots" or "pendulum" ? value.Metronome.VisualMode : null };
        return value with { Name = value.Name.Trim(), Amp = amp, Metronome = metronome };
    }
    public static PracticeWorkspace Validate(PracticeWorkspace value)
    {
        if (value.Setups is null || value.Favorites is null || value.Setups.Count > 100 || value.Favorites.Count > 500
            || value.Favorites.Any(k => string.IsNullOrWhiteSpace(k) || k.Length > 256 || k.Any(char.IsControl)))
            throw new InvalidDataException("Knihovna cvičení není platná.");
        var setups = value.Setups.Select(Validate).ToArray();
        if (setups.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != setups.Length)
            throw new InvalidDataException("Sestavy cvičení mají opakované ID.");
        return new(setups, value.Favorites.Distinct(StringComparer.Ordinal).ToArray());
    }
    public static string SearchText(string value)
    {
        var text = new StringBuilder();
        foreach (char c in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) text.Append(char.ToLowerInvariant(c));
        return text.ToString().Normalize(NormalizationForm.FormC);
    }
    public static SearchEntry[] Search(IEnumerable<SearchEntry> entries, string query, IReadOnlySet<string> favorites, bool onlyFavorites)
    {
        var terms = SearchText(query[..Math.Min(query.Length, 256)]).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return entries.Where(e => (!onlyFavorites || favorites.Contains(e.Key)) && terms.All(t => SearchText(e.Label).Contains(t, StringComparison.Ordinal)))
            .OrderByDescending(e => favorites.Contains(e.Key)).ThenBy(e => e.Title, StringComparer.Create(CultureInfo.GetCultureInfo("cs-CZ"), true))
            .ThenBy(e => e.Key, StringComparer.Ordinal).Take(200).ToArray();
    }
}
