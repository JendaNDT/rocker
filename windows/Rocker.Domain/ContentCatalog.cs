// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Collections.ObjectModel;

namespace Rocker.Domain;
public sealed record Lesson(string Id, string Title, int EstimatedMinutes, IReadOnlyList<JsonElement> Steps);
public sealed class ContentCatalog
{
    private readonly IReadOnlyDictionary<string, JsonElement> groups;
    public IReadOnlyList<Lesson> Lessons { get; }
    private ContentCatalog(IReadOnlyDictionary<string, JsonElement> groups, IReadOnlyList<Lesson> lessons) { this.groups = groups; Lessons = lessons; }
    public IEnumerable<JsonElement> Items(string group) => groups.TryGetValue(group, out var items) ? items.EnumerateArray() : [];
    public JsonElement? Find(string group, string id) => Items(group).Cast<JsonElement?>().FirstOrDefault(item => item!.Value.GetProperty(group == "screens" ? "href" : "id").GetString() == id);
    public static ContentCatalog Load(byte[] bytes)
    {
        if (bytes.Length is 0 or > 4 * 1024 * 1024) throw new InvalidDataException("Katalog obsahu přesahuje povolenou velikost.");
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Verze katalogu obsahu není podporovaná.");
            var dictionary = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var group in root.GetProperty("groups").EnumerateObject())
            {
                var items = group.Value.GetProperty("items");
                if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 10000) throw new InvalidDataException("Neplatná skupina obsahu.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.GetProperty(group.Name == "screens" ? "href" : "id").GetString();
                    if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !ids.Add(id)) throw new InvalidDataException("Chybějící nebo duplicitní identifikátor obsahu.");
                }
                if (!dictionary.TryAdd(group.Name, items.Clone())) throw new InvalidDataException("Duplicitní skupina obsahu.");
            }
            string[] required = ["stages", "chords", "scales", "riffs", "achievements", "tunings", "ampPresets", "metronomePresets", "timeSignatures", "clickSounds", "surgicalExercises", "powerChordExercises", "gallopExercises", "palmMuteDrills", "screens"];
            if (required.Any(group => !dictionary.ContainsKey(group))) throw new InvalidDataException("Katalog neobsahuje všechny požadované skupiny.");
            var chords = dictionary["chords"].EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
            var lessons = new List<Lesson>();
            var lessonIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stage in dictionary["stages"].EnumerateArray()) foreach (var item in stage.GetProperty("lessons").EnumerateArray())
            {
                var id = item.GetProperty("id").GetString(); var title = item.GetProperty("title").GetString();
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || !lessonIds.Add(id)) throw new InvalidDataException("Neplatný nebo duplicitní identifikátor lekce.");
                var minutes = item.GetProperty("estimatedMinutes").GetInt32();
                if (minutes <= 0 || minutes > 240) throw new InvalidDataException("Neplatná délka lekce.");
                var steps = item.GetProperty("steps").EnumerateArray().Select(step => step.Clone()).ToArray();
                if (steps.Length is 0 or > 256) throw new InvalidDataException("Neplatný počet kroků lekce.");
                foreach (var step in steps)
                {
                    var kind = step.GetProperty("kind").GetString();
                    if (kind is not ("text" or "note" or "chord" or "timed")) throw new InvalidDataException("Neznámý typ kroku lekce.");
                    if (kind == "chord" && !chords.Contains(step.GetProperty("chordId").GetString())) throw new InvalidDataException("Lekce odkazuje na neexistující akord.");
                    if (kind == "note" && step.GetProperty("targetMidi").GetInt32() is < 0 or > 127) throw new InvalidDataException("Neplatný cílový tón lekce.");
                }
                lessons.Add(new Lesson(id, title, minutes, Array.AsReadOnly(steps)));
            }
            return new ContentCatalog(new ReadOnlyDictionary<string, JsonElement>(dictionary), lessons.AsReadOnly());
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new InvalidDataException("Katalog obsahu je poškozený.", error); }
    }
}
