// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Rocker.Domain;
public sealed class LegacyMigrationPlan(IReadOnlyList<DataDocument> documents,IReadOnlyList<string> warnings,string? secret)
{
    public IReadOnlyList<DataDocument> Documents {get;}=documents;
    public IReadOnlyList<string> Warnings {get;}=warnings;
    [System.Text.Json.Serialization.JsonIgnore] public string? Secret {get;}=secret;
    public bool HasSecret=>!string.IsNullOrEmpty(Secret);
    public override string ToString()=>$"{Documents.Count} documents, {Warnings.Count} warnings, secret present: {HasSecret}";
}
public static class LegacyMigration
{
    public static IReadOnlySet<string> Keys {get;}=new HashSet<string>(StringComparer.Ordinal){"rocker-settings","rocker-progress","rocker-instrument","rocker-lesson-progress","rocker-yt-loops","rocker-ai","rocker-chat","rocker-amp","rocker-amp-presets","rocker-metronome","rocker-custom-tunings"};
    private static JsonElement Get(JsonElement value,string key)=>value.ValueKind==JsonValueKind.Object && value.TryGetProperty(key,out var item)?item:default;
    private static string Text(JsonElement value,string key,string fallback)=>Get(value,key).ValueKind==JsonValueKind.String ? Get(value,key).GetString()!:fallback;
    private static bool Bool(JsonElement value,string key,bool fallback=false)=>Get(value,key).ValueKind==JsonValueKind.True ? true:Get(value,key).ValueKind==JsonValueKind.False ? false:fallback;
    private static double Number(JsonElement value,string key,double fallback)=>Get(value,key).ValueKind==JsonValueKind.Number && Get(value,key).TryGetDouble(out var n) && double.IsFinite(n)?Math.Clamp(n,0,1):fallback;
    public static LegacyMigrationPlan Parse(ReadOnlyMemory<byte> bytes,ContentCatalog? catalog=null)
    {
        if(bytes.Length>4*1024*1024)throw new InvalidDataException("Stará záloha je příliš velká.");
        using var json=JsonDocument.Parse(bytes,new JsonDocumentOptions{MaxDepth=32});var root=json.RootElement;
        if(Text(root,"app","")!="rocker" || Text(root,"kind","")!="settings-backup" || Get(root,"version").ValueKind!=JsonValueKind.Number || !Get(root,"version").TryGetInt32(out int version) || version is <1 or >2 || Get(root,"data").ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Soubor není podporovaná stará záloha Rockeru.");
        var states=new Dictionary<string,JsonElement>();var warnings=new List<string>();var documents=new List<DataDocument>();
        foreach(var entry in Get(root,"data").EnumerateObject())
        {
            if(!Keys.Contains(entry.Name)){warnings.Add("Neznámá skupina nastavení byla vynechána.");continue;}
            try
            {
                if(entry.Value.ValueKind!=JsonValueKind.String || entry.Value.GetString()!.Length>1024*1024)throw new InvalidDataException();
                using var state=JsonDocument.Parse(entry.Value.GetString()!,new JsonDocumentOptions{MaxDepth=32});var body=Get(state.RootElement,"state");
                if(body.ValueKind!=JsonValueKind.Object || (Get(state.RootElement,"version").ValueKind==JsonValueKind.Number && Get(state.RootElement,"version").GetInt32()!=0))throw new InvalidDataException();
                if(!states.TryAdd(entry.Name,body.Clone()))throw new InvalidDataException();
            }
            catch(Exception error) when(error is JsonException or InvalidDataException or InvalidOperationException or FormatException){warnings.Add(entry.Name+": neplatná skupina byla vynechána.");}
        }
        string secret=states.TryGetValue("rocker-ai",out var aiState)?Text(aiState,"apiKey",""):"";
        if(secret.Length>4096){warnings.Add("Starý AI klíč je neplatný; zadej jej znovu.");secret="";}
        foreach(var entry in states)
        {
            try
            {
                var state=entry.Value;string key=entry.Key;
                switch(key)
                {
                    case "rocker-settings":
                        documents.Add(UserData.Document("settings","default",new AppSettings(Theme:Text(state,"colorMode","auto") switch{"dark"=>"dark","light"=>"light",_=>"system"},TuningId:Text(state,"tuningId","standard"),Handedness:Text(state,"handedness","right")=="left"?"left":"right",ZenMode:Bool(state,"zenMode"),Notation:Text(state,"notation","english")=="german"?"german":"english",NoiseGate:Number(state,"noiseGate",0.06),ReferenceToneVolume:Number(state,"referenceToneVolume",0.3),TunerConfirmSound:Bool(state,"tunerConfirmSound"),Onboarded:Bool(state,"onboarded"))));
                        warnings.Add("Webové rozložení a materiál tématu nahrazuje nativní Vintage; vibrace se nepřenášejí.");break;
                    case "rocker-progress":
                        var progress=ProgressRules.Sanitize(state);var bpm=new Dictionary<string,double>();
                        foreach(var item in progress.BpmRecords){string id=catalog is not null && catalog.LegacyRiffAliases.TryGetValue(item.Key,out var replacement) ? replacement:item.Key;bpm[id]=Math.Max(bpm.GetValueOrDefault(id),item.Value);}
                        if(!progress.BpmRecords.Keys.SequenceEqual(bpm.Keys))warnings.Add("Staré riffové ID bylo převedeno na vlastní náhradní cvičení.");
                        documents.Add(UserData.Document("progress","default",progress with{BpmRecords=bpm}));break;
                    case "rocker-instrument":
                        var p=Get(state,"profile");var defaults=InstrumentProfile.Default;var pickups=Get(p,"pickups");
                        var validPickups=pickups.ValueKind==JsonValueKind.Array ? pickups.EnumerateArray().Where(item=>Text(item,"position","") is "neck" or "middle" or "bridge" && Text(item,"type","") is "humbucker" or "single" or "p90").Select(item=>new Pickup(Text(item,"position",""),Text(item,"type",""))).Take(6).ToArray():defaults.Pickups;
                        documents.Add(UserData.Document("instrument","default",new InstrumentProfile(Text(p,"name",defaults.Name),Text(p,"bodyType",defaults.BodyType),validPickups,Text(p,"scaleLength",defaults.ScaleLength),Text(p,"stringGauge",defaults.StringGauge),Text(p,"notes",defaults.Notes))));break;
                    case "rocker-lesson-progress":
                        var ids=Get(state,"completedLessonIds");documents.Add(UserData.Document("lessons","default",new LessonProgress(ids.ValueKind==JsonValueKind.Array?ids.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.String).Select(v=>v.GetString()!).ToArray():[],new Dictionary<string,int>())));break;
                    case "rocker-yt-loops":AddArray("yt-loops",Get(state,"loops"),documents,warnings);break;
                    case "rocker-ai":documents.Add(UserData.Document("ai-settings","default",new AiSettings(Text(state,"model","gemini-2.5-flash"))));break;
                    case "rocker-chat":AddArray("chat",Get(state,"messages"),documents,warnings);warnings.Add("Původní AI zprávy nemají čas odeslání; nový čas se nevymýšlí.");break;
                    case "rocker-amp":documents.Add(UserData.Document("amp","default",AmpRules.Sanitize(Get(state,"settings")) with{PresetId=Text(state,"presetId","") is {Length:>0} preset?preset:null}));break;
                    case "rocker-amp-presets":documents.Add(UserData.Document("amp-presets","default",AmpRules.SanitizeUserPresets(Get(state,"presets"))));break;
                    case "rocker-metronome":
                        documents.Add(UserData.Document("metronome","default",MetronomeRules.Sanitize(Get(state,"config")) with{VisualMode=Text(state,"visualMode","dots")=="pendulum"?"pendulum":"dots"}));
                        var presets=Get(state,"presets");
                        var list=presets.ValueKind==JsonValueKind.Array?presets.EnumerateArray().Select(p=>new{id=Text(p,"id",""),name=Text(p,"name",""),config=MetronomeRules.Sanitize(p)}).Where(p=>UserData.ValidId(p.id) && !string.IsNullOrWhiteSpace(p.name)).ToArray():[];
                        documents.Add(UserData.Document("metronome-presets","default",list));break;
                    case "rocker-custom-tunings":AddArray("custom-tunings",Get(state,"tunings"),documents,warnings);break;
                }
            }
            catch(Exception error) when(error is InvalidDataException or JsonException or InvalidOperationException or ArgumentException or NullReferenceException){warnings.Add(entry.Key+": nepodporovaná data byla vynechána.");}
        }
        if(secret.Length>0)
            for(int i=0;i<documents.Count;i++)
            {
                var original=documents[i];var node=JsonNode.Parse(original.Value.GetRawText());Redact(node,secret);
                using var redacted=JsonDocument.Parse(node!.ToJsonString());documents[i]=UserData.Normalize(original with{Value=redacted.RootElement.Clone()});
            }
        return new(documents,warnings,secret.Length>0?secret:null);
    }
    private static void AddArray(string kind,JsonElement value,List<DataDocument> documents,List<string> warnings)
    {
        var items=new List<JsonElement>();
        if(value.ValueKind==JsonValueKind.Array)
            foreach(var item in value.EnumerateArray())
            {
                try{var array=JsonSerializer.SerializeToElement(new[]{item},UserData.Json);var doc=UserData.Normalize(new(kind,"default",1,array));items.Add(doc.Value[0].Clone());}
                catch(Exception error) when(error is InvalidDataException or JsonException or NullReferenceException or InvalidOperationException){warnings.Add(kind+": neplatná položka byla vynechána.");}
            }
        if(kind=="custom-tunings")items=items.GroupBy(i=>i.GetProperty("id").GetString()).Select(g=>g.First()).Take(20).ToList();
        documents.Add(UserData.Document(kind,"default",items));
    }
    private static void Redact(JsonNode? node,string key)
    {
        if(node is JsonObject obj)foreach(var entry in obj.ToArray())
        {if(entry.Value is JsonValue value && value.TryGetValue<string>(out var text))obj[entry.Key]=text.Replace(key,"[klíč odstraněn]",StringComparison.Ordinal);else Redact(entry.Value,key);}
        else if(node is JsonArray array)for(int i=0;i<array.Count;i++)
        {if(array[i] is JsonValue value && value.TryGetValue<string>(out var text))array[i]=text.Replace(key,"[klíč odstraněn]",StringComparison.Ordinal);else Redact(array[i],key);}
    }
}
