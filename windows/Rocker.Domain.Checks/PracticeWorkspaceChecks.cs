// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using System.Text.Json;
internal static class PracticeWorkspaceChecks
{
    public static void Run(Action<bool, string> check)
    {
        var amp = AmpRules.Sanitize(JsonSerializer.SerializeToElement(new { drive = .37, cabinet = "vintage" }));
        var metronome = MetronomeRules.Sanitize(JsonSerializer.SerializeToElement(new { bpm = 87, beatsPerBar = 3 }));
        var setup = new PracticeSetup(Guid.NewGuid().ToString("D"), "  Moje cvičení  ", new("06", "lesson-id"), amp, true, metronome, "standard", Guid.NewGuid().ToString("D"), .75, 12, 5, 15, true);
        var workspace = new PracticeWorkspace([setup], ["view:06:lesson-id", "view:06:lesson-id", "song:" + setup.SongId]);
        var restored = UserData.Read<PracticeWorkspace>(UserData.Document("practice-workspace", "default", workspace).Value);
        check(restored.Setups[0].Name == "Moje cvičení" && restored.Setups[0].Amp.Drive == .37 && restored.Setups[0].Metronome.Bpm == 87 && restored.Setups[0].LoopEnd == 15 && restored.Favorites.Count == 2, "complete practice setup retains sound, timing, exact task and deduplicated favorites");
        check(PracticeWorkspaceRules.Validate(setup with{RecordingId="w01-recording-2"}).RecordingId=="w01-recording-2","legacy recording identifiers remain usable in saved practice setups");
        check(PracticeWorkspaceRules.Validate(PracticeWorkspaceRules.Empty).Setups.Count == 0, "old profiles start with an empty practice workspace");
        foreach (var invalid in new[] { setup with { Speed = double.NaN }, setup with { LoopEnd = 4 }, setup with { SongId = null }, setup with { Target = new("unknown") }, setup with { Name = "\n" }, setup with { Position = -1 }, setup with { RecordingId = "" } })
        {
            bool rejected = false;
            try { PracticeWorkspaceRules.Validate(invalid); } catch (Exception e) when (e is InvalidDataException or ArgumentException) { rejected = true; }
            check(rejected, "invalid practice setup cannot reach persistence or native audio");
        }
        bool duplicate = false; try { PracticeWorkspaceRules.Validate(new PracticeWorkspace([setup, setup], [])); } catch (InvalidDataException) { duplicate = true; }
        check(duplicate, "duplicate stable setup IDs are rejected");
        var entries = new[] { new SearchEntry("one", "Přesný trénink", "Cvičení", new("09")), new SearchEntry("two", "Moje skladba", "Nahrávka", new("14")), new SearchEntry("three", "Trénink akordů", "Lekce", new("06")) };
        var favorites = new HashSet<string> { "three" };
        check(PracticeWorkspaceRules.Search(entries, "presny cviceni", favorites, false).Select(e => e.Key).SequenceEqual(["one"]), "search ignores Czech diacritics and matches all words across title and category");
        check(PracticeWorkspaceRules.Search(entries, "TRENINK", favorites, false)[0].Key == "three", "favorite results come first independent of case");
        check(PracticeWorkspaceRules.Search(entries, "", favorites, true).Select(e => e.Key).SequenceEqual(["three"]), "favorite filter does not return unrelated items");
        check(PracticeWorkspaceRules.Search(entries, "absent", favorites, false).Length == 0, "no search match does not become a default action");
        check(PracticeWorkspaceRules.Search(Enumerable.Range(0, 250).Select(i => new SearchEntry(i.ToString(), "Item " + i, "View", new("01"))), "", favorites, false).Length == 200, "search result allocation is bounded");
    }
}
