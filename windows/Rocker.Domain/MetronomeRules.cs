// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Rocker.Domain;
public sealed record RampConfig(bool Enabled, int EveryBars, int StepBpm, int TargetBpm);
public sealed record GapConfig(bool Enabled, int AudibleBars, int SilentBars);
public sealed record PolyrhythmConfig(bool Enabled, int TicksPerBeat, string Sound, double Volume);
public sealed record MetronomeConfig(int Bpm, int BeatsPerBar, int Subdivision, IReadOnlyList<string> AccentPattern, string Sound, double Volume,
    double AccentVolume, RampConfig Ramp, GapConfig Gap, double Swing, int CountInBars, PolyrhythmConfig Polyrhythm)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VisualMode {get;init;}
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyList<string?>? SoundPattern { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SubdivisionSound { get; init; }
}
public static class MetronomeRules
{
    private static readonly HashSet<string> Sounds = ["classic", "wood", "beep", "click", "hihat"];
    private static JsonElement Get(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var entry) ? entry : default;
    private static double Number(JsonElement value, double fallback) => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) && double.IsFinite(number) ? number : fallback;
    private static bool Enabled(JsonElement value) => Get(value, "enabled").ValueKind == JsonValueKind.True;
    private static string Sound(JsonElement value, string fallback) => value.ValueKind == JsonValueKind.String && Sounds.Contains(value.GetString()!) ? value.GetString()! : fallback;
    private static int Int(JsonElement value, int minimum, int maximum, int fallback) => (int)Math.Clamp(Math.Floor(Number(value, fallback) + 0.5), minimum, maximum);
    public static int ClampBeatsPerBar(double value) => double.IsFinite(value) ? (int)Math.Clamp(Math.Floor(value + 0.5), 1, 16) : 4;
    public static int ClampSubdivision(int value) => value is >= 1 and <= 4 ? value : 1;
    public static string CycleAccent(string level) => level == "normal" ? "accent" : level == "accent" ? "muted" : "normal";
    public static double ClampSwing(double value) => double.IsFinite(value) ? Math.Clamp(value, 0.5, 0.75) : 0.5;
    public static RampConfig SanitizeRamp(JsonElement value) => new(Enabled(value), Int(Get(value,"everyBars"),1,16,4), Int(Get(value,"stepBpm"),1,20,5), Music.ClampBpm(Number(Get(value,"targetBpm"),160)));
    public static GapConfig SanitizeGap(JsonElement value) => new(Enabled(value), Int(Get(value,"audibleBars"),1,16,4), Int(Get(value,"silentBars"),1,16,1));
    public static PolyrhythmConfig SanitizePolyrhythm(JsonElement value) => new(Enabled(value), Int(Get(value,"ticksPerBeat"),2,7,3), Sound(Get(value,"sound"),"hihat"), Math.Clamp(Number(Get(value,"volume"),0.6),0,1));
    public static bool IsSilentBar(long bar, GapConfig gap) => gap.Enabled && bar >= 0 && gap.AudibleBars + gap.SilentBars > 0 && bar % (gap.AudibleBars + gap.SilentBars) >= gap.AudibleBars;
    public static int NextRampBpm(int current, RampConfig ramp) => current >= ramp.TargetBpm ? current : Math.Min(current + ramp.StepBpm, ramp.TargetBpm);
    public static MetronomeConfig Sanitize(JsonElement value)
    {
        int beats = ClampBeatsPerBar(Number(Get(value,"beatsPerBar"),4));
        var entries = Get(value,"accentPattern"); var accents = new string[beats];
        var sounds = Get(value,"soundPattern"); string?[]? pattern = sounds.ValueKind == JsonValueKind.Array ? new string?[beats] : null;
        for (int i = 0; i < beats; ++i)
        {
            var entry = entries.ValueKind == JsonValueKind.Array && i < entries.GetArrayLength() ? entries[i] : default;
            var accent = entry.ValueKind == JsonValueKind.String ? entry.GetString() : null;
            accents[i] = accent is "muted" or "normal" or "accent" ? accent : i == 0 ? "accent" : "normal";
            if (pattern is not null && i < sounds.GetArrayLength())
            {
                var sound = sounds[i]; pattern[i] = sound.ValueKind == JsonValueKind.String && Sounds.Contains(sound.GetString()!) ? sound.GetString() : null;
            }
        }
        var subdivision = Number(Get(value,"subdivision"),1);
        var subSound = Get(value,"subdivisionSound");
        return new(Music.ClampBpm(Number(Get(value,"bpm"),100)), beats, subdivision is 1 or 2 or 3 or 4 ? (int)subdivision : 1, Array.AsReadOnly(accents), Sound(Get(value,"sound"),"classic"),
            Math.Clamp(Number(Get(value,"volume"),0.8),0,1), Math.Clamp(Number(Get(value,"accentVolume"),1),0,1), SanitizeRamp(Get(value,"ramp")), SanitizeGap(Get(value,"gap")),
            ClampSwing(Number(Get(value,"swing"),0.5)), Int(Get(value,"countInBars"),0,2,0), SanitizePolyrhythm(Get(value,"polyrhythm")))
        {
            SoundPattern = pattern is not null && pattern.Any(sound => sound is not null) ? Array.AsReadOnly(pattern) : null,
            SubdivisionSound = subSound.ValueKind == JsonValueKind.String && Sounds.Contains(subSound.GetString()!) ? subSound.GetString() : null
        };
    }
}
