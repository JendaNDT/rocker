// SPDX-License-Identifier: AGPL-3.0-only
using System.Net;
using System.Text;
using System.Text.Json;
using Rocker.Domain;
using Rocker.Infrastructure;
internal static class AiChecks
{
    public static async Task<int> RunAsync()
    {
        int checks=0;void Check(bool valid,string message){++checks;if(!valid)throw new Exception(message);}
        string Event(string text,string? finish=null)=>"data: "+JsonSerializer.Serialize(new{candidates=new[]{new{content=new{parts=new[]{new{text}}},finishReason=finish??"FINISH_REASON_UNSPECIFIED"}}})+"\n\n";
        var handler=new MockHandler();using var http=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};var client=new AiHttpClient(http,"gemini");ChatMessage[] question=[new("user","Jak začít?")];const string key="synthetic-key-for-tests";
        handler.Next=(request,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StreamContent(new FragmentStream(Encoding.UTF8.GetBytes(Event("Cvič ")+Event("pomalu.","STOP"))))});
        var pieces=new List<string>();var reply=await client.GenerateAsync(key,"test-model",question,"Český tutor",pieces.Add);
        Check(reply=="Cvič pomalu." && pieces.Count==2,"split UTF-8 SSE preserves progressive complete text");Check(handler.Requests==1 && handler.LastPath=="/v1beta/models/test-model:streamGenerateContent","one POST uses the pinned provider origin and safe model path");Check(handler.LastKey==key,"credential is in the header, never a URL parameter");
        using(var cancelStream=new CancellationTokenSource())
        {
            bool stopped=false;try{await client.GenerateAsync(key,"test-model",question,"Tutor",_=>cancelStream.Cancel(),cancellation:cancelStream.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"cancellation during a fragmented SSE response cannot accept its partial text");
        }
        foreach(var status in new[]{HttpStatusCode.BadRequest,HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden,HttpStatusCode.NotFound,HttpStatusCode.TooManyRequests,HttpStatusCode.InternalServerError})
        {
            handler.Next=(_,_)=>Task.FromResult(new HttpResponseMessage(status){Content=new StringContent("sensitive provider body")});int before=handler.Requests;bool rejected=false;try{await client.GenerateAsync(key,"test-model",question,"Tutor");}catch(AiRequestException error){rejected=!error.Message.Contains("sensitive",StringComparison.Ordinal);}Check(rejected && handler.Requests==before+1,"HTTP error is actionable/redacted and never automatically retried");
        }
        foreach(string data in new[]{"data: invalid JSON\n\n",Event("partial"),Event("truncated","MAX_TOKENS"),"data: {\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}\n\n"})
        {
            handler.Next=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(data)});bool rejected=false;try{await client.GenerateAsync(key,"test-model",question,"Tutor");}catch(AiRequestException){rejected=true;}Check(rejected,"malformed, disconnected, truncated or blocked response cannot be accepted");
        }
        handler.Next=async(_,token)=>{await Task.Delay(Timeout.Infinite,token);return new(HttpStatusCode.OK);};
        using(var cancel=new CancellationTokenSource()){var work=client.GenerateAsync(key,"test-model",question,"Tutor",cancellation:cancel.Token);cancel.Cancel();bool cancelled=false;try{await work;}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"caller cancellation stops the pending HTTP request");}
        bool timedOut=false;try{await client.GenerateAsync(key,"test-model",question,"Tutor",timeLimit:TimeSpan.FromMilliseconds(10));}catch(AiRequestException error){timedOut=error.Message.Contains("vypršelo",StringComparison.Ordinal);}Check(timedOut,"timeout is distinct from caller cancellation");
        int prior=handler.Requests;bool invalidModel=false;try{await client.GenerateAsync(key,"../../escape",question,"Tutor");}catch(AiRequestException){invalidModel=true;}Check(invalidModel && handler.Requests==prior,"invalid model never creates a request");
        handler.Next=(request,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(request.RequestUri!.Query.Contains("pageToken",StringComparison.Ordinal)?"{\"models\":[{\"name\":\"models/test-b\",\"supportedGenerationMethods\":[\"generateContent\"]}]}":"{\"models\":[{\"name\":\"models/test-a\",\"displayName\":\"Model A\",\"supportedGenerationMethods\":[\"generateContent\"]},{\"name\":\"models/embed-only\",\"supportedGenerationMethods\":[\"embedContent\"]}],\"nextPageToken\":\"page 2\"}")});
        var models=await client.ModelsAsync(key);Check(models.Models.Select(m=>m.Id).SequenceEqual(new[]{"test-a","test-b"}) && !models.More,"available generating models are discovered across pages, not guessed");
        var catalog=ContentCatalog.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"content/catalog.json")));
        string planJson="{\"items\":[{\"title\":\"Ladění\",\"detail\":\"Nalaď kytaru\",\"minutes\":2,\"viewId\":\"03\",\"itemId\":null},{\"title\":\"Rytmus\",\"detail\":\"Cvič pomalu\",\"minutes\":3,\"viewId\":\"04\",\"itemId\":null},{\"title\":\"Riff\",\"detail\":\"Vyber oblíbený riff\",\"minutes\":5,\"viewId\":\"19\",\"itemId\":\"chromatic-pulse\"}]}";
        var plan=AiPlanRules.Parse(planJson,catalog,10);Check(plan.TotalMinutes==10 && plan.Items[2].ItemId=="chromatic-pulse","schema-validated plan has exact native task links");
        foreach(string bad in new[]{planJson.Replace("chromatic-pulse","invented"),planJson.Replace("\"03\"","\"https://evil.example\""),planJson.Replace("\"minutes\":2","\"minutes\":60")}){bool rejected=false;try{AiPlanRules.Parse(bad,catalog,10);}catch(InvalidDataException){rejected=true;}Check(rejected,"unknown task, external destination or incorrect time cannot enter the plan");}
        handler.Next=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Event(planJson,"STOP"))});string generated=await client.GenerateAsync(key,"test-model",question,"Plan",responseSchema:AiPlanRules.ResponseSchema);Check(AiPlanRules.Parse(generated,catalog,10).TotalMinutes==10,"plan traverses the real streaming HTTP client before domain validation");
        Check(UserData.Read<AiSettings>(UserData.Document("ai-settings","default",new AiSettings(Plan:plan with{AcceptedAt=DateTimeOffset.UtcNow})).Value).Plan!.TotalMinutes==10,"accepted plan survives the actual normalized settings schema");
        var openHandler=new MockHandler{AllowedHost="api.openai.com"};using var openHttp=new HttpClient(openHandler){Timeout=Timeout.InfiniteTimeSpan};var openClient=new AiHttpClient(openHttp);
        openHandler.Next=async(request,token)=>{using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));Check(body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString()=="system","OpenAI receives native messages with an explicit system instruction");Check(body.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("schema").GetProperty("additionalProperties").ValueKind==JsonValueKind.False,"structured plan uses strict OpenAI JSON schema without tools");return new(HttpStatusCode.OK){Content=new StringContent("data: "+JsonSerializer.Serialize(new{choices=new[]{new{delta=new{content=planJson},finish_reason="stop"}}})+"\n\ndata: [DONE]\n\n")};};
        string openPlan=await openClient.GenerateAsync(key,"gpt-test",question,"Tutor",responseSchema:AiPlanRules.ResponseSchema);Check(AiPlanRules.Parse(openPlan,catalog,10).TotalMinutes==10 && openHandler.LastKey==key && openHandler.LastPath=="/v1/chat/completions","OpenAI streaming plan follows the independently pinned endpoint and bearer credential");
        openHandler.Next=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"data\":[{\"id\":\"gpt-test\"},{\"id\":\"gpt-test-realtime\"},{\"id\":\"text-embedding-test\"}]}")});Check((await openClient.ModelsAsync(key)).Models.Single().Id=="gpt-test","OpenAI account model discovery excludes obvious non-chat modalities");
        Console.WriteLine($"PASS: {checks} AI HTTP/stream/cancel/timeout/quota/model/plan assertions (mock transport only).");return checks;
    }
    private sealed class MockHandler:HttpMessageHandler
    {
        public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Next{get;set;}=(_,_)=>throw new InvalidOperationException();public string AllowedHost="generativelanguage.googleapis.com";public int Requests;public string? LastPath,LastKey;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){++Requests;if(request.RequestUri!.Scheme!="https" || request.RequestUri.Host!=AllowedHost || request.RequestUri.Query.Contains("key",StringComparison.Ordinal))throw new Exception("Untrusted credential endpoint");LastPath=request.RequestUri.AbsolutePath;LastKey=request.Headers.Authorization?.Parameter??request.Headers.GetValues("x-goog-api-key").Single();return Next(request,token);}
    }
    private sealed class FragmentStream(byte[] bytes):MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)=>base.ReadAsync(buffer[..Math.Min(3,buffer.Length)],cancellationToken);
    }
}
