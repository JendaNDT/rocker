// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Domain;
public sealed record AiPlanItem(string Title,string Detail,int Minutes,string ViewId,string? ItemId=null);
public sealed record AiPracticePlan(IReadOnlyList<AiPlanItem> Items,int TotalMinutes,DateTimeOffset? AcceptedAt=null);
public static class AiPlanRules
{
    public static readonly IReadOnlyDictionary<string,string?> Targets=new Dictionary<string,string?>{
        ["03"]="tunings",["04"]="metronomePresets",["06"]="lessons",["09"]="surgicalExercises",["10"]="powerChordExercises",["11"]="gallopExercises",["12"]="palmMuteDrills",["17"]="chords",["18"]="scales",["19"]="riffs",["07"]=null,["08"]=null,["13"]=null,["14"]=null,["15"]=null,["16"]=null,["21"]=null,["22"]=null};
    public static AiPracticePlan Parse(string text,ContentCatalog catalog,int minutes)
    {
        if(text.Length>32000)throw new InvalidDataException("Návrh plánu je příliš dlouhý.");using var json=JsonDocument.Parse(text,new JsonDocumentOptions{MaxDepth=8});if(json.RootElement.ValueKind!=JsonValueKind.Object || !json.RootElement.TryGetProperty("items",out var items) || items.ValueKind!=JsonValueKind.Array || items.GetArrayLength() is <3 or >6)throw new InvalidDataException("AI nevrátila platný seznam kroků.");
        var result=new List<AiPlanItem>();foreach(var item in items.EnumerateArray()){if(item.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Neplatný blok plánu.");result.Add(new(item.GetProperty("title").GetString()!,item.GetProperty("detail").GetString()!,item.GetProperty("minutes").GetInt32(),item.GetProperty("viewId").GetString()!,item.TryGetProperty("itemId",out var id) && id.ValueKind!=JsonValueKind.Null?id.GetString():null));}
        return Validate(new(result,result.Sum(i=>i.Minutes)),catalog,minutes);
    }
    public static AiPracticePlan Validate(AiPracticePlan plan,ContentCatalog? catalog=null,int? minutes=null)
    {
        if(plan.Items is null || plan.Items.Count is <3 or >6 || plan.Items.Any(i=>i is null || string.IsNullOrWhiteSpace(i.Title) || i.Title.Length>128 || string.IsNullOrWhiteSpace(i.Detail) || i.Detail.Length>1024 || i.Minutes is <1 or >60 || i.ViewId is null || !Targets.ContainsKey(i.ViewId) || i.ItemId is not null && !UserData.ValidId(i.ItemId)) || plan.TotalMinutes!=plan.Items.Sum(i=>i.Minutes) || plan.TotalMinutes>180 || minutes is { } target && Math.Abs(plan.TotalMinutes-target)>2)throw new InvalidDataException("Plán musí mít 3–6 platných bloků a odpovídající součet minut.");
        if(catalog is not null)foreach(var item in plan.Items)ValidateTarget(item,catalog);
        return plan with{Items=Array.AsReadOnly(plan.Items.ToArray())};
    }
    public static void ValidateTarget(AiPlanItem item,ContentCatalog catalog)
    {
        if(!Targets.TryGetValue(item.ViewId,out var group))throw new InvalidDataException("Nepodporovaný pohled plánu.");
        if(item.ItemId is null)return;bool valid=group=="lessons"?catalog.Lessons.Any(l=>l.Id==item.ItemId):group is not null && catalog.Find(group,item.ItemId) is not null;
        if(!valid)throw new InvalidDataException("Plán odkazuje na neexistující úlohu aplikace.");
    }
    public static object ResponseSchema=>new{type="OBJECT",properties=new{items=new{type="ARRAY",minItems=3,maxItems=6,items=new{type="OBJECT",properties=new{title=new{type="STRING"},detail=new{type="STRING"},minutes=new{type="INTEGER",minimum=1,maximum=60},viewId=new{type="STRING",@enum=Targets.Keys.ToArray()},itemId=new{type="STRING",nullable=true}},required=new[]{"title","detail","minutes","viewId","itemId"}}}},required=new[]{"items"}};
}
