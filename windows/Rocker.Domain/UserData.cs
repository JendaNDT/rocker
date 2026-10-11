// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Rocker.Domain;
public sealed record AppSettings(string Theme = "system", string TuningId = "standard", string Handedness = "right", bool ZenMode = false,
    string Notation = "english", double NoiseGate = 0.06, double ReferenceToneVolume = 0.3, bool TunerConfirmSound = false, bool Onboarded = false,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] DevicePreference? AudioDevice = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] bool AmpEnabled = false,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? ReferenceA = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] PalmProfile? PalmMute = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] NavigationTarget? ResumeTask = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] PlanProgress? PracticePlan = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] bool KeepAwake = false,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] PlanProgress? AiPlanProgress = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] int SetupStep = 0);
public sealed record PlanProgress(string Key, IReadOnlyList<string> Completed);
public sealed record DevicePreference(string Backend, string Input, string Output, double SampleRate=0, uint BufferFrames=0, int InputChannel=-1, uint OutputFirst=0);
public sealed record Pickup(string Position, string Type);
public sealed record InstrumentProfile(string Name, string BodyType, IReadOnlyList<Pickup> Pickups, string ScaleLength, string StringGauge, string Notes)
{
    public static InstrumentProfile Default => new("Solton Double Cut","Double cut, masivní tělo",[new("neck","humbucker"),new("bridge","humbucker")],"25,5\"",".010-.046","");
}
public sealed record LessonProgress(IReadOnlyList<string> CompletedIds, IReadOnlyDictionary<string,int> Positions);
public sealed record TuningString(string Note, int Midi);
public sealed record CustomTuning(string Id, string Label, IReadOnlyList<TuningString> Strings);
public sealed record SavedVideoLoop(string Id, string VideoId, string Label, double Start, double End, double Rate);
public sealed record AiSettings(string Model = "gemini-2.5-flash", bool IncludeProgress = true, bool IncludeInstrument = false,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] AiPracticePlan? Plan = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? Provider = null);
public sealed record SavedChatMessage(string Role, string Text, DateTimeOffset? CreatedAt = null);
public sealed record RecordingData(string Id, string Name, string MimeType, double DurationSec, DateTimeOffset CreatedAt,
    string RelativePath, long Bytes, string Sha256, IReadOnlyList<Marker> Markers,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] CaptureMetadata? Capture = null)
{
    [JsonIgnore] public string LibraryLabel => Name + (Capture?.Partial==true ? " · neúplná / obnovená" : "");
}
public sealed record CaptureMetadata(int SampleRate, int Channels, int Bus, long Frames, long? FirstFrame, uint? Epoch,
    uint DspLatencyFrames, bool DspChanged, bool Partial, string? Failure = null);
public sealed record ProjectTrack(string Id, string Name, string MediaPath, string Sha256, double DurationSec,
    double Gain = 1, bool Muted = false, double Pan = 0, bool Solo = false, long OffsetFrames = 0,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] long TrimStartFrames = 0,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] int? SourceSampleRate = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] long? SourceFrames = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] TrackAlignment? Alignment = null);
public sealed record ProjectManifest(int SchemaVersion, string Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int SampleRate, IReadOnlyList<ProjectTrack> Tracks,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] LoopSessionMetadata? Looper = null);
public sealed record LoopSessionMetadata(int Bus, long Frames, uint DelayFrames, bool DelayMeasured, bool Partial);
public sealed record ProjectIndex(string Id, string Name, string RelativeManifest);
public sealed record DataDocument(string Kind, string Id, int SchemaVersion, JsonElement Value);
public static class UserData
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 32 };
    public static IReadOnlySet<string> Kinds { get; } = new HashSet<string>(StringComparer.Ordinal) { "settings","instrument","progress","lessons","amp","amp-presets","metronome","metronome-presets","custom-tunings","yt-loops","ai-settings","chat","recording","project","song" };
    public static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128 && !id.Any(char.IsControl);
    public static bool ValidHash(string? value) => value is not null && value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool ValidRelativePath(string? path) => path is not null && path.Length is > 0 and <= 512 && !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':') && !path.Any(char.IsControl) && path.Split('/').All(segment => segment.Length > 0 && segment is not "." and not ".." && !segment.EndsWith('.') && segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'));
    public static DataDocument Document<T>(string kind, string id, T value) => Normalize(new(kind,id,1,JsonSerializer.SerializeToElement(value,Json)));
    public static T Read<T>(JsonElement value) => JsonSerializer.Deserialize<T>(value,Json) ?? throw new InvalidDataException("Uložená hodnota chybí.");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static bool Unit(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
    public static DataDocument Normalize(DataDocument document)
    {
        Require(Kinds.Contains(document.Kind) && ValidId(document.Id) && document.SchemaVersion == 1, "Typ, ID nebo verze dat není podporovaná.");
        Require(document.Value.GetRawText().Length <= 1024 * 1024, "Záznam je příliš velký.");
        RejectSecretFields(document.Value);
        object value;
        switch (document.Kind)
        {
            case "song":
                var song=SongRules.Validate(Read<SongSession>(document.Value));Require(song.Id==document.Id,"ID skladby neodpovídá záznamu.");value=song;break;
            case "settings":
                var settings = Read<AppSettings>(document.Value);
                Require(settings.Theme is "system" or "dark" or "light" && settings.Handedness is "right" or "left" && settings.Notation is "english" or "german" && ValidId(settings.TuningId) && Unit(settings.NoiseGate) && Unit(settings.ReferenceToneVolume), "Neplatné nastavení.");
                Require(settings.SetupStep is >=0 and <=5,"Neplatný krok průvodce.");
                if(settings.ResumeTask is { } resume)WorkspaceViews.Validate(resume);
                foreach(var plan in new[]{settings.PracticePlan,settings.AiPlanProgress}.OfType<PlanProgress>())Require(ValidId(plan.Key) && plan.Completed is not null && plan.Completed.Count<=32 && plan.Completed.All(ValidId),"Neplatný stav plánu.");
                if(settings.ReferenceA is double referenceA)Require(double.IsFinite(referenceA) && referenceA is >=430 and <=450,"Neplatná referenční frekvence A.");
                if(settings.AudioDevice is { } device) Require(ValidId(device.Backend) && device.Input is not null && device.Input.Length<=512 && !device.Input.Any(char.IsControl) && ValidId(device.Output) && double.IsFinite(device.SampleRate) && (device.SampleRate==0 || device.SampleRate is >=8000 and <=192000) && (device.BufferFrames==0 || device.BufferFrames is >=16 and <=8192) && device.InputChannel is >=-1 and <=63 && (device.InputChannel==-1 || device.Input.Length>0) && device.OutputFirst<=62,"Neplatné zvukové zařízení.");
                if(settings.PalmMute is { } palm)Require(palm.Calibration is not null && ValidHash(palm.Device) && ValidId(palm.Tuning) && ValidHash(palm.Instrument) && !string.IsNullOrWhiteSpace(palm.Pickup) && palm.Pickup.Length<=64 && double.IsFinite(palm.Sensitivity) && palm.Sensitivity is >=.5 and <=1.5 && double.IsFinite(palm.Calibration.OpenMean) && double.IsFinite(palm.Calibration.MutedMean) && palm.Calibration.MutedMean>=0 && palm.Calibration.OpenMean>palm.Calibration.MutedMean && palm.Calibration.OpenMean<=100000 && Math.Abs(palm.Calibration.Threshold-(palm.Calibration.OpenMean+palm.Calibration.MutedMean)/2)<1e-6,"Neplatný profil palm mute.");
                value = settings; break;
            case "instrument":
                var profile = Read<InstrumentProfile>(document.Value);
                Require(profile.Name is not null && profile.BodyType is not null && profile.ScaleLength is not null && profile.StringGauge is not null && profile.Notes is not null && profile.Pickups is not null && profile.Pickups.Count <= 6 && profile.Pickups.All(p => p.Position is "neck" or "middle" or "bridge" && p.Type is "humbucker" or "single" or "p90"), "Neplatný profil kytary."); value = profile; break;
            case "progress": value = ProgressRules.Sanitize(document.Value); break;
            case "lessons":
                var lessons = Read<LessonProgress>(document.Value);
                Require(lessons.CompletedIds is not null && lessons.Positions is not null && lessons.Positions.Count <= 10000 && lessons.Positions.All(item => ValidId(item.Key) && item.Value is >= 0 and <= 255), "Neplatná pozice lekce.");
                value = lessons with { CompletedIds = lessons.CompletedIds!.Where(ValidId).Distinct(StringComparer.Ordinal).ToArray() }; break;
            case "amp": value = AmpRules.Sanitize(document.Value) with { PresetId=document.Value.ValueKind==JsonValueKind.Object && document.Value.TryGetProperty("presetId",out var preset) && preset.ValueKind==JsonValueKind.String && ValidId(preset.GetString()) ? preset.GetString():null }; break;
            case "amp-presets": value = AmpRules.SanitizeUserPresets(document.Value); break;
            case "metronome": value = MetronomeRules.Sanitize(document.Value) with { VisualMode=document.Value.ValueKind==JsonValueKind.Object && document.Value.TryGetProperty("visualMode",out var mode) && mode.ValueKind==JsonValueKind.String && mode.GetString() is "dots" or "pendulum" ? mode.GetString():null }; break;
            case "metronome-presets":
                Require(document.Value.ValueKind == JsonValueKind.Array && document.Value.GetArrayLength() <= 1000,"Neplatné metronomové presety.");
                value = document.Value.EnumerateArray().Select(item => new { id = item.GetProperty("id").GetString(), name = item.GetProperty("name").GetString(), config = MetronomeRules.Sanitize(item.GetProperty("config")) }).Where(item => ValidId(item.id) && !string.IsNullOrWhiteSpace(item.name)).ToArray(); break;
            case "custom-tunings":
                var tunings = Read<CustomTuning[]>(document.Value);
                Require(tunings.Length <= 20 && tunings.All(t => t.Id is not null && t.Id.StartsWith("custom-",StringComparison.Ordinal) && ValidId(t.Id) && !string.IsNullOrWhiteSpace(t.Label) && t.Strings is not null && t.Strings.Count == 6 && t.Strings.All(s => s.Midi is >= 24 and <= 84 && !string.IsNullOrEmpty(s.Note))) && tunings.Select(t => t.Id).Distinct().Count() == tunings.Length, "Neplatná vlastní ladění."); value = tunings; break;
            case "yt-loops":
                var loops = Read<SavedVideoLoop[]>(document.Value);
                Require(loops.Length <= 10000 && loops.All(l => ValidId(l.Id) && l.VideoId is not null && l.VideoId.Length == 11 && l.VideoId.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') && l.Label is not null && double.IsFinite(l.Start) && double.IsFinite(l.End) && l.Start >= 0 && l.End > l.Start && double.IsFinite(l.Rate) && l.Rate is > 0 and <= 16), "Neplatná video smyčka."); value = loops; break;
            case "ai-settings":
                var ai = Read<AiSettings>(document.Value); Require(ValidId(ai.Model) && ai.Provider is null or "openai" or "gemini","Neplatný model nebo poskytovatel AI.");if(ai.Plan is not null)ai=ai with{Plan=AiPlanRules.Validate(ai.Plan)}; value = ai; break;
            case "chat":
                var chat = Read<SavedChatMessage[]>(document.Value); Require(chat.Length <= 10000 && chat.All(m => m.Role is "user" or "model" && m.Text is not null && m.Text.Length <= 100000),"Neplatná historie AI."); value = chat; break;
            case "recording":
                var recording = Read<RecordingData>(document.Value);
                if(recording.Capture is { } capture) Require(capture.SampleRate is >=8000 and <=192000 && capture.Channels is 1 or 2 && capture.Bus is >=0 and <=2 && capture.Frames>0 && (capture.FirstFrame is null or >=0) && Math.Abs(recording.DurationSec-capture.Frames/(double)capture.SampleRate)<0.00001 && (capture.Failure is null || capture.Failure.Length<=512),"Neplatné údaje pořízení nahrávky.");
                Require(recording.Id == document.Id && recording.Name is not null && recording.MimeType is not null && double.IsFinite(recording.DurationSec) && recording.DurationSec >= 0 && recording.Bytes is > 0 and <= 1024L*1024*1024 && ValidHash(recording.Sha256) && ValidRelativePath(recording.RelativePath) && recording.RelativePath.StartsWith("recordings/",StringComparison.Ordinal) && recording.Markers is not null && recording.Markers.All(m => ValidId(m.Id) && m.Label is not null && double.IsFinite(m.TimeSec) && m.TimeSec >= 0 && m.TimeSec <= recording.DurationSec),"Neplatná metadata nahrávky."); value = recording; break;
            case "project":
                var project = Read<ProjectIndex>(document.Value); Require(project.Id == document.Id && project.Name is not null && ValidRelativePath(project.RelativeManifest) && Guid.TryParse(project.Id,out var projectGuid) && project.RelativeManifest=="projects/"+projectGuid.ToString("N")+"/project.rockerproj","Neplatný index projektu."); value = project; break;
            default: throw new InvalidDataException("Neznámý typ dat.");
        }
        return document with { Value = JsonSerializer.SerializeToElement(value,Json) };
    }
    public static void ValidateProject(ProjectManifest project)
    {
        Require(project.SchemaVersion == 1 && Guid.TryParse(project.Id,out _) && !string.IsNullOrWhiteSpace(project.Name) && project.SampleRate is >= 8000 and <= 192000 && project.Tracks is not null && project.Tracks.Count <= 256, "Neplatný projekt.");
        if(project.Looper is { } loop)Require(loop.Bus is 0 or 1 && loop.Frames>0 && loop.Frames<=2097152 && loop.DelayFrames<=192000 && project.Tracks!.Count is >=1 and <=8 && project.Tracks.All(t=>t.OffsetFrames==0 && Math.Abs(t.DurationSec-loop.Frames/(double)project.SampleRate)<0.00001),"Neplatná Looper session.");
        Require(project.Tracks!.All(t => ValidId(t.Id) && t.Name is not null && ValidRelativePath(t.MediaPath) && t.MediaPath.StartsWith("media/",StringComparison.Ordinal) && ValidHash(t.Sha256) && double.IsFinite(t.DurationSec) && t.DurationSec >= 0 && Unit(t.Gain) && double.IsFinite(t.Pan) && t.Pan is >= -1 and <= 1 && t.OffsetFrames is >=0 and <=uint.MaxValue && (t.Alignment is null || StudioRules.Valid(t.Alignment)) && t.TrimStartFrames>=0 && ((t.SourceSampleRate is null && t.SourceFrames is null) || (t.SourceSampleRate is >=8000 and <=192000 && t.SourceFrames>t.TrimStartFrames))) && project.Tracks!.Select(t => t.Id).Distinct().Count() == project.Tracks!.Count,"Neplatné stopy projektu.");
    }
    private static void RejectSecretFields(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var entry in value.EnumerateObject())
            {
                var key = entry.Name.Replace("_","").Replace("-","").ToLowerInvariant();
                Require(key is not ("apikey" or "secret" or "password" or "authorization" or "accesstoken"), "Tajné údaje nepatří do běžného úložiště.");
                RejectSecretFields(entry.Value);
            }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var entry in value.EnumerateArray()) RejectSecretFields(entry);
    }
}
