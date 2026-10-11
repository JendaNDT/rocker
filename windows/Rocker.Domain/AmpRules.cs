// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Domain;
public sealed record AmpSettings(double Compression, double Drive, string Distortion, double Bass, double Mid, double Treble, string Cabinet, double Delay, double Reverb, double Chorus, double Level)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? PresetId {get;init;}
}
public sealed record AmpPreset(string Id, string Name, AmpSettings Settings);
public static class AmpRules
{
    private static JsonElement Get(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var entry) ? entry : default;
    private static double Number(JsonElement value, string key, double fallback, double minimum = 0, double maximum = 1)
    {
        var entry = Get(value, key);
        return entry.ValueKind == JsonValueKind.Number && entry.TryGetDouble(out var number) && double.IsFinite(number) ? Math.Clamp(number, minimum, maximum) : fallback;
    }
    private static string Choice(JsonElement value, string key, string[] allowed, string fallback)
    {
        var entry = Get(value, key); return entry.ValueKind == JsonValueKind.String && allowed.Contains(entry.GetString()!) ? entry.GetString()! : fallback;
    }
    public static AmpSettings Sanitize(JsonElement value) => new(Number(value,"compression",0), Number(value,"drive",0.1), Choice(value,"distortion",["overdrive","distortion","fuzz"],"distortion"),
        Number(value,"bass",0.1,-1,1), Number(value,"mid",0,-1,1), Number(value,"treble",0.15,-1,1), Choice(value,"cabinet",["modern","vintage","open"],"modern"),
        Number(value,"delay",0.1), Number(value,"reverb",0), Number(value,"chorus",0), Number(value,"level",0.6));
    public static AmpPreset[] SanitizeUserPresets(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return [];
        var result = new List<AmpPreset>();
        foreach (var item in value.EnumerateArray())
        {
            var id = Get(item,"id"); var name = Get(item,"name");
            if (id.ValueKind != JsonValueKind.String || id.GetString()!.Length == 0 || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())) continue;
            result.Add(new(id.GetString()!, name.GetString()!, Sanitize(Get(item,"settings"))));
        }
        return result.ToArray();
    }
}
