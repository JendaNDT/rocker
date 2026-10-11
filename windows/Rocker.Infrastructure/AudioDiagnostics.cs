// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Infrastructure;
public sealed record DiagnosticDevice(string? Backend,string? Input,string? Output,double SampleRate,uint BufferFrames,int InputLatencyFrames,int OutputLatencyFrames)
{public string DriverPackageVersion=>"unknown";}
public sealed record DiagnosticCallbacks(uint Epoch,double SampleRate,ulong Callbacks,ulong Frames,uint MinimumFrames,uint MaximumFrames,double AverageMicroseconds,double MaximumMicroseconds,double GraphBudgetPercent,ulong OverBudget,ulong InvalidCallbacks,uint DspLatencyFrames);
public sealed record DiagnosticAnalysis(ulong DroppedBlocks,ulong ProducerHighWatermark,double ResultAgeMilliseconds,double WindowMilliseconds)
{public int QueueCapacity=>64;}
public sealed record DiagnosticLifecycle(ulong DeviceStarts,ulong DeviceFaults,ulong StopRequests,ulong MediaErrors,ulong ConfigurationFailures,uint LastFailureReason);
public sealed record AudioDiagnosticReport(int SchemaVersion,DateTimeOffset CapturedAtUtc,string ProductVersion,string? SourceCommit,string RuntimeVersion,string OperatingSystem,uint NativeAbi,string State,string Source,
    DiagnosticDevice Device,DiagnosticCallbacks Callbacks,DiagnosticAnalysis Analysis,DiagnosticLifecycle Lifecycle,long? DriverXrunCount)
{
    public bool SourceCommitAvailable=>SourceCommit is not null;
    public bool PhysicalRoundtripMeasured=>false;
    public double? PhysicalRoundtripMilliseconds=>null;
    public bool DriverXrunCountAvailable=>DriverXrunCount is not null;
    public string DeviceScope=>"Current/last validated selection. Callback epoch may refer to the previous device after reconfiguration.";
    public string CallbackScope=>"Rocker graph/copy work; excludes vendor callback overhead. Over-budget is not a confirmed hardware underrun.";
    public string CounterScope=>"Callback counters: current device epoch. Queue and lifecycle counters: host lifetime. Xruns: vendor current open device, refreshed explicitly.";
}
public static class AudioDiagnostics
{
    public static string Serialize(AudioDiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if(report.SchemaVersion!=1 || report.CapturedAtUtc.Offset!=TimeSpan.Zero || report.NativeAbi==0 || report.Device is null || report.Callbacks is null || report.Analysis is null || report.Lifecycle is null)throw new InvalidDataException("Neplatný diagnostický dokument.");
        if(report.SourceCommit is string commit && (commit.Length!=40 || commit.Any(c=>!Uri.IsHexDigit(c))))throw new InvalidDataException("Neplatná revize aplikace.");
        foreach(double number in new[]{report.Device.SampleRate,report.Callbacks.SampleRate,report.Callbacks.AverageMicroseconds,report.Callbacks.MaximumMicroseconds,report.Callbacks.GraphBudgetPercent,report.Analysis.ResultAgeMilliseconds,report.Analysis.WindowMilliseconds})if(!double.IsFinite(number) || number<0)throw new InvalidDataException("Neplatné diagnostické číslo.");
        if(report.DriverXrunCount is <0 || report.Analysis.ProducerHighWatermark>64)throw new InvalidDataException("Neplatné čítače zvuku.");
        static string? Label(string? value)=>value is null?null:new string(value.Take(512).Select(c=>char.IsControl(c)?' ':c).ToArray());
        // The fixed report schema cannot include user settings, keys, recording
        // paths, waveform samples, AI messages or arbitrary application objects.
        var sanitized=report with{ProductVersion=Label(report.ProductVersion)!,RuntimeVersion=Label(report.RuntimeVersion)!,OperatingSystem=Label(report.OperatingSystem)!,State=Label(report.State)!,Source=Label(report.Source)!,Device=report.Device with{Backend=Label(report.Device.Backend),Input=Label(report.Device.Input),Output=Label(report.Device.Output)}};
        return JsonSerializer.Serialize(sanitized,new JsonSerializerOptions{WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
    }
}
