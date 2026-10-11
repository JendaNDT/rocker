// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Infrastructure;
using System.Text.Json;
public static class AudioDiagnosticChecks
{
    public static int Run()
    {
        int checks=0;void Check(bool value,string message){++checks;if(!value)throw new Exception(message);}
        var report=new AudioDiagnosticReport(1,new DateTimeOffset(2026,10,11,0,0,0,TimeSpan.Zero),"0.1.0",new string('a',40),"10.0.12","Windows 11",10,"Running","live",new("ASIO","M-Track Solo","M-Track Solo",48000,128,32,64),new(2,48000,1000,128000,128,128,40,120,1.5,2,0,15),new(3,64,5,171),new(2,1,2,0,1,2),null);
        string json=AudioDiagnostics.Serialize(report);using var parsed=JsonDocument.Parse(json);
        Check(parsed.RootElement.GetProperty("driverXrunCount").ValueKind==JsonValueKind.Null && !parsed.RootElement.GetProperty("driverXrunCountAvailable").GetBoolean(),"unsupported driver counter is unknown rather than zero");
        Check(!parsed.RootElement.GetProperty("physicalRoundtripMeasured").GetBoolean() && parsed.RootElement.GetProperty("physicalRoundtripMilliseconds").ValueKind==JsonValueKind.Null,"buffer and FIR cannot become a measured roundtrip claim");
        Check(parsed.RootElement.GetProperty("callbacks").GetProperty("overBudget").GetInt64()==2 && parsed.RootElement.GetProperty("analysis").GetProperty("droppedBlocks").GetInt64()==3,"timing and queue loss keep distinct typed counters");
        Check(!new[]{"apiKey","recordings","chat","progress","userDataPath","relativePath","waveform"}.Any(term=>json.Contains('"'+term+'"',StringComparison.OrdinalIgnoreCase)),"fixed report schema contains no user payload or secret fields");
        using var known=JsonDocument.Parse(AudioDiagnostics.Serialize(report with{DriverXrunCount=7}));Check(known.RootElement.GetProperty("driverXrunCount").GetInt64()==7 && known.RootElement.GetProperty("driverXrunCountAvailable").GetBoolean(),"explicitly refreshed supported vendor count remains separate");
        using var label=JsonDocument.Parse(AudioDiagnostics.Serialize(report with{Device=report.Device with{Input="Input\n1"}}));Check(label.RootElement.GetProperty("device").GetProperty("input").GetString()=="Input 1","device labels cannot inject control characters into reports");
        foreach(var invalid in new[]{report with{DriverXrunCount=-1},report with{SourceCommit="not-a-commit"},report with{Callbacks=report.Callbacks with{AverageMicroseconds=double.NaN}},report with{Analysis=report.Analysis with{ProducerHighWatermark=65}}}){
            try{AudioDiagnostics.Serialize(invalid);throw new Exception("Invalid report accepted");}catch(InvalidDataException){++checks;}}
        Console.WriteLine($"PASS: {checks} audio diagnostic schema, counter availability, latency honesty and privacy assertions.");return checks;
    }
}
