// SPDX-License-Identifier: AGPL-3.0-only
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rocker.Domain;
namespace Rocker.Infrastructure;

public sealed class AiRequestException(string message):Exception(message);
public sealed record AiModel(string Id,string Name);
public sealed record AiModelList(IReadOnlyList<AiModel> Models,bool More);
public sealed partial class AiHttpClient(HttpClient http,string provider="openai")
{
    public static readonly Uri GeminiOrigin=new("https://generativelanguage.googleapis.com/v1beta/");
    public static readonly Uri OpenAiOrigin=new("https://api.openai.com/v1/");
    private Uri Origin=>provider switch{"gemini"=>GeminiOrigin,"openai"=>OpenAiOrigin,_=>throw new AiRequestException("Poskytovatel AI není podporovaný.")};
    [GeneratedRegex("^[A-Za-z0-9_.-]{1,128}$",RegexOptions.CultureInvariant)]private static partial Regex ModelPattern();
    private HttpRequestMessage Request(HttpMethod method,string path,string key)
    {
        if(string.IsNullOrWhiteSpace(key) || key.Length>4096 || key.Any(char.IsControl))throw new AiRequestException("API klíč není uložený nebo není platný. Zadej jej v Nastavení.");
        var request=new HttpRequestMessage(method,new Uri(Origin,path));if(provider=="gemini")request.Headers.Add("x-goog-api-key",key.Trim());else request.Headers.Authorization=new("Bearer",key.Trim());return request;
    }
    private static void EnsureStatus(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode)return;
        throw new AiRequestException(response.StatusCode switch{
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden=>"Klíč nebo jeho oprávnění nejsou platné. Zkontroluj Nastavení AI.",
            HttpStatusCode.NotFound=>"Model není dostupný. Obnov seznam modelů a vyber jiný.",
            HttpStatusCode.TooManyRequests=>"Poskytovatel omezil požadavky nebo vyčerpal kvótu. Zkus to později; limity závisí na tvém účtu.",
            HttpStatusCode.BadRequest=>"Poskytovatel nepřijal dotaz nebo nastavení modelu. Uprav zadání či model.",
            _=>"Server AI neodpověděl úspěšně. Zkus požadavek později."});
    }
    private static async Task<byte[]> ReadBoundedAsync(Stream stream,int maximum,CancellationToken token)
    {
        using var output=new MemoryStream();var bytes=new byte[8192];int read;
        while((read=await stream.ReadAsync(bytes,token))>0){if(output.Length+read>maximum)throw new AiRequestException("Odpověď AI překročila povolenou velikost.");await output.WriteAsync(bytes.AsMemory(0,read),token);}return output.ToArray();
    }
    private static JsonElement OpenAiSchema(object schema)
    {
        var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(schema))!;
        void Convert(System.Text.Json.Nodes.JsonNode? current)
        {
            if(current is System.Text.Json.Nodes.JsonObject obj)
            {
                if(obj["type"] is { } type){string value=type.GetValue<string>().ToLowerInvariant();bool nullable=obj["nullable"]?.GetValue<bool>()==true;obj.Remove("nullable");obj["type"]=nullable?new System.Text.Json.Nodes.JsonArray(value,"null"):System.Text.Json.Nodes.JsonValue.Create(value);if(value=="object")obj["additionalProperties"]=false;}
                foreach(var child in obj.ToArray())Convert(child.Value);
            }else if(current is System.Text.Json.Nodes.JsonArray array)foreach(var child in array)Convert(child);
        }
        Convert(node);return JsonSerializer.SerializeToElement(node);
    }
    public async Task<AiModelList> ModelsAsync(string key,CancellationToken cancellation=default)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(30));var models=new List<AiModel>();string? next=null;
        try{for(int page=0;page<5;++page){using var request=Request(HttpMethod.Get,provider=="openai"?"models":"models?pageSize=100"+(next is null?"":"&pageToken="+Uri.EscapeDataString(next)),key);using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);EnsureStatus(response);await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var json=JsonDocument.Parse(await ReadBoundedAsync(stream,1024*1024,timeout.Token),new JsonDocumentOptions{MaxDepth=16});
            if(json.RootElement.TryGetProperty(provider=="openai"?"data":"models",out var entries) && entries.ValueKind==JsonValueKind.Array)foreach(var entry in entries.EnumerateArray())
            {
                if(!entry.TryGetProperty(provider=="openai"?"id":"name",out var name) || name.ValueKind!=JsonValueKind.String)continue;
                if(provider=="gemini" && (!entry.TryGetProperty("supportedGenerationMethods",out var methods) || methods.ValueKind!=JsonValueKind.Array || !methods.EnumerateArray().Any(m=>m.ValueKind==JsonValueKind.String && m.GetString()=="generateContent")))continue;
                string id=name.GetString()!;if(id.StartsWith("models/",StringComparison.Ordinal))id=id[7..];if(!ModelPattern().IsMatch(id))continue;if(provider=="openai" && (!id.StartsWith("gpt-",StringComparison.Ordinal) || new[]{"audio","transcribe","realtime","image","search","tts"}.Any(id.Contains)))continue;string label=entry.TryGetProperty("displayName",out var display) && display.ValueKind==JsonValueKind.String?display.GetString()!:id;models.Add(new(id,label.Length>200?label[..200]:label));if(models.Count>500)throw new AiRequestException("Seznam modelů je příliš velký.");
            }
            next=json.RootElement.TryGetProperty("nextPageToken",out var nextField) && nextField.ValueKind==JsonValueKind.String?nextField.GetString():null;if(string.IsNullOrEmpty(next))break;if(next.Length>512)throw new AiRequestException("Neplatná odpověď seznamu modelů.");
        }return new(models.DistinctBy(m=>m.Id).OrderBy(m=>m.Id,StringComparer.Ordinal).ToArray(),!string.IsNullOrEmpty(next));}
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new AiRequestException("Načtení modelů vypršelo. Zkus to později.");}
        catch(HttpRequestException){throw new AiRequestException("Se serverem AI se nelze spojit. Zkontroluj připojení.");}
        catch(Exception error) when(error is JsonException or InvalidOperationException){throw new AiRequestException("Seznam modelů má neplatný formát.");}
    }
    public async Task<string> GenerateAsync(string key,string model,IReadOnlyList<ChatMessage> history,string instruction,Action<string>? partial=null,object? responseSchema=null,CancellationToken cancellation=default,TimeSpan? timeLimit=null)
    {
        if(!ModelPattern().IsMatch(model))throw new AiRequestException("Vyber platný model AI.");
        if(history.Count is <1 or >40 || history.Any(m=>m.Role is not ("user" or "model") || string.IsNullOrWhiteSpace(m.Text)) || history.Sum(m=>m.Text.Length)>64000 || instruction.Length>32000)throw new AiRequestException("Dotaz nebo kontext je příliš dlouhý či prázdný.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(timeLimit??TimeSpan.FromSeconds(90));
        try
        {
            using var request=Request(HttpMethod.Post,provider=="openai"?"chat/completions":"models/"+Uri.EscapeDataString(model)+":streamGenerateContent?alt=sse",key);
            var config=new Dictionary<string,object>{{"temperature",.7},{"maxOutputTokens",4096}};if(responseSchema is not null){config["responseMimeType"]="application/json";config["responseSchema"]=responseSchema;}
            if(provider=="openai")
            {
                var messages=new List<object>{new{role="system",content=instruction}};messages.AddRange(history.Select(m=>(object)new{role=m.Role=="model"?"assistant":"user",content=m.Text}));var body=new Dictionary<string,object>{{"model",model},{"messages",messages},{"stream",true},{"max_completion_tokens",4096}};if(responseSchema is not null)body["response_format"]=new{type="json_schema",json_schema=new{name="rocker_practice_plan",strict=true,schema=OpenAiSchema(responseSchema)}};request.Content=JsonContent.Create(body);
            }
            else request.Content=JsonContent.Create(new{systemInstruction=new{parts=new[]{new{text=instruction}}},contents=history.Select(m=>new{role=m.Role,parts=new[]{new{text=m.Text}}}),generationConfig=config});
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);EnsureStatus(response);
            await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var reader=new StreamReader(stream,new UTF8Encoding(false,true),false,4096);var buffer=new char[4096];var line=new StringBuilder();var data=new StringBuilder();var text=new StringBuilder();int total=0;bool completed=false;
            void ProcessData()
            {
                string payload=data.ToString().Trim();data.Clear();if(payload.Length==0 || payload=="[DONE]")return;
                using var json=JsonDocument.Parse(payload,new JsonDocumentOptions{MaxDepth=16});var root=json.RootElement;
                if(root.ValueKind!=JsonValueKind.Object)throw new AiRequestException("AI poslala neplatný obsah.");
                if(provider=="openai")
                {
                    if(!root.TryGetProperty("choices",out var choices) || choices.ValueKind!=JsonValueKind.Array || choices.GetArrayLength()==0)return;var choice=choices[0];
                    if(choice.TryGetProperty("delta",out var delta) && delta.TryGetProperty("content",out var piece) && piece.ValueKind==JsonValueKind.String){text.Append(piece.GetString());if(text.Length>64000)throw new AiRequestException("Odpověď AI je příliš dlouhá.");partial?.Invoke(text.ToString());}
                    if(choice.TryGetProperty("finish_reason",out var finish) && finish.ValueKind==JsonValueKind.String){if(finish.GetString()=="stop")completed=true;else throw new AiRequestException("Poskytovatel odpověď nedokončil; návrh nebyl přijat. Zkrať nebo uprav zadání.");}return;
                }
                if(root.TryGetProperty("promptFeedback",out var feedback) && feedback.TryGetProperty("blockReason",out _))throw new AiRequestException("Poskytovatel zablokoval odpověď. Přeformuluj zadání.");
                if(!root.TryGetProperty("candidates",out var candidates) || candidates.ValueKind!=JsonValueKind.Array || candidates.GetArrayLength()==0)return;var candidate=candidates[0];
                if(candidate.TryGetProperty("content",out var content) && content.TryGetProperty("parts",out var parts) && parts.ValueKind==JsonValueKind.Array)foreach(var part in parts.EnumerateArray())
                {
                    if(part.TryGetProperty("thought",out var thought) && thought.ValueKind==JsonValueKind.True)continue;
                    if(part.TryGetProperty("text",out var value) && value.ValueKind==JsonValueKind.String){text.Append(value.GetString());if(text.Length>64000)throw new AiRequestException("Odpověď AI je příliš dlouhá.");}
                }
                partial?.Invoke(text.ToString());
                if(candidate.TryGetProperty("finishReason",out var completedReason) && completedReason.ValueKind==JsonValueKind.String && completedReason.GetString()=="STOP")completed=true;
                if(candidate.TryGetProperty("finishReason",out var reason) && reason.GetString() is { } why && why is not ("STOP" or "FINISH_REASON_UNSPECIFIED"))throw new AiRequestException(why=="MAX_TOKENS"?"Odpověď je neúplná kvůli limitu délky. Zkrať zadání.":"Poskytovatel odpověď nedokončil. Uprav zadání.");
            }
            void ProcessLine(){string value=line.ToString().TrimEnd('\r');line.Clear();if(value.Length==0){ProcessData();return;}if(value.StartsWith("data:",StringComparison.Ordinal)){data.Append(value.AsSpan(5).TrimStart());data.Append('\n');if(data.Length>1024*1024)throw new AiRequestException("Část odpovědi AI je příliš velká.");}}
            int count;while((count=await reader.ReadAsync(buffer.AsMemory(),timeout.Token))>0){total+=count;if(total>4*1024*1024)throw new AiRequestException("Odpověď AI překročila povolenou velikost.");for(int i=0;i<count;++i){if(buffer[i]=='\n')ProcessLine();else{line.Append(buffer[i]);if(line.Length>1024*1024)throw new AiRequestException("Řádek odpovědi AI je příliš dlouhý.");}}}
            if(line.Length>0)ProcessLine();ProcessData();if(!completed)throw new AiRequestException("Spojení skončilo před dokončením odpovědi. Neúplný text nebyl přijat.");if(string.IsNullOrWhiteSpace(text.ToString()))throw new AiRequestException("AI nevrátila žádnou odpověď. Uprav zadání.");return text.ToString().Trim();
        }
        catch(OperationCanceledException) when(!cancellation.IsCancellationRequested){throw new AiRequestException("Čekání na AI vypršelo. Dotaz se automaticky neopakuje.");}
        catch(HttpRequestException){throw new AiRequestException("Se serverem AI se nelze spojit. Zkontroluj připojení.");}
        catch(Exception error) when(error is JsonException or DecoderFallbackException or InvalidOperationException){throw new AiRequestException("AI poslala neplatný nebo neúplný obsah.");}
    }
}
