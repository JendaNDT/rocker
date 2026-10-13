// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Interop;
using Rocker.Infrastructure;
using System.Text.Json;
using Windows.Storage.Pickers;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private TextBlock? diagnosticStatus;
    private FrameworkElement BuildAudioDiagnostics()
    {
        var panel=Stack();diagnosticStatus=Text("Čítače se naplní po spuštění zvuku. Diagnostika nepřehrává tón ani neotevírá nové zařízení.",true);panel.Children.Add(diagnosticStatus);
        var refresh=new Button{Content="Načíst také čítač ovladače"};refresh.Click+=async(_,_)=>{try{if(audio is not null)await audio.RefreshDiagnosticsAsync();if(!closed)RefreshAudioDiagnostics();}catch(Exception error){if(!closed)ShowPersistenceError("Čítač ovladače nelze načíst. "+error.Message);}};
        var export=new Button{Content="Prohlédnout a uložit diagnostiku"};export.Click+=ExportDiagnostics_Click;panel.Children.Add(refresh);panel.Children.Add(export);panel.Children.Add(Text("Čas grafu měří práci Rockeru v callbacku. Překročený rozpočet není potvrzený výpadek karty; její vlastní čítač může být nedostupný. Buffer ani FIR prodleva neměří celé zpoždění kytary. Export obsahuje verze, zařízení a čítače; soukromé nahrávky, cesty, AI klíče a zprávy se do něj nevkládají.",true));return new Expander{Header="Diagnostika zvuku",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
    private void RefreshAudioDiagnostics()
    {
        if(audio is null || diagnosticStatus is null)return;var value=audio.GetDiagnostics();
        diagnosticStatus.Text=value.Callbacks==0?"Zvuk zatím nevytvořil callback v této epoše.":$"Epocha {value.Epoch} · callbacky {value.Callbacks:N0} · bloky {value.MinFrames}–{value.MaxFrames} vzorků\nPrůměr grafu {value.AverageMicroseconds:0.0} µs · maximum {value.MaxMicroseconds:0.0} µs · rozpočet {value.GraphBudgetPercent:0.0} %\nPřekročené rozpočty {value.OverBudget} · ztracené analytické bloky {value.AnalysisDropped} · maximum fronty {value.AnalysisHighWater}/64\nChyby zařízení {value.DeviceFaults} · odmítnuté konfigurace {value.ConfigurationFailures} · čítač výpadků ovladače: "+(value.DriverXrunsKnown?value.DriverXruns.ToString():"nedostupný / ještě nenačtený");
    }
    private async Task<string> CaptureDiagnosticsAsync()
    {
        if(audio is null)throw new InvalidOperationException("Zvukové jádro není dostupné.");await audio.RefreshDiagnosticsAsync();var device=await audio.GetDeviceDetailsAsync();var diagnostics=audio.GetDiagnostics();var analysis=audio.GetAnalysis();var clock=audio.GetClock();var state=audio.GetSnapshot();
        string? commit=null;var manifest=Path.Combine(AppContext.BaseDirectory,"app-build.json");try{if(File.Exists(manifest) && new FileInfo(manifest).Length<=16384){using var metadata=JsonDocument.Parse(await File.ReadAllTextAsync(manifest));if(metadata.RootElement.TryGetProperty("sourceCommit",out var source) && source.ValueKind==JsonValueKind.String){var candidate=source.GetString();if(candidate is {Length:40} && candidate.All(Uri.IsHexDigit))commit=candidate;}}}catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException){commit=null;}
        var report=new AudioDiagnosticReport(1,DateTimeOffset.UtcNow,typeof(MainWindow).Assembly.GetName().Version?.ToString()??"unknown",commit,Environment.Version.ToString(),Environment.OSVersion.VersionString,diagnostics.AbiVersion,state.State.ToString(),diagnostics.SourceMode switch{0=>"tone",1=>"media",2=>"live",3=>"metronome",4=>"reference",_=>"unknown"},new(device.Backend,device.Input,device.Output,device.SampleRate,device.BufferFrames,device.InputLatencyFrames,device.OutputLatencyFrames),new(diagnostics.Epoch,diagnostics.SampleRate,diagnostics.Callbacks,diagnostics.Frames,diagnostics.MinFrames,diagnostics.MaxFrames,diagnostics.AverageMicroseconds,diagnostics.MaxMicroseconds,diagnostics.GraphBudgetPercent,diagnostics.OverBudget,diagnostics.InvalidCallbacks,clock.DspLatencyFrames),new(diagnostics.AnalysisDropped,diagnostics.AnalysisHighWater,analysis.AgeSeconds*1000,analysis.WindowSeconds*1000),new(diagnostics.DeviceStarts,diagnostics.DeviceFaults,diagnostics.StopRequests,diagnostics.MediaErrors,diagnostics.ConfigurationFailures,diagnostics.LastFailureReason),diagnostics.DriverXrunsKnown?diagnostics.DriverXruns:null);
        return AudioDiagnostics.Serialize(report);
    }
    private async void ExportDiagnostics_Click(object sender,RoutedEventArgs args)
    {
        if(audio is null || closingWindow || busy)return;try{SetBusy(true);string json=await CaptureDiagnosticsAsync();if(closed || closingWindow)return;
            var text=new TextBox{Header="Místní diagnostický report",Text=json,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,MaxHeight=500,MinWidth=400};var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Diagnostika zvuku",Content=text,PrimaryButtonText="Uložit JSON…",CloseButtonText="Zavřít",DefaultButton=ContentDialogButton.Close};if(await ShowDialogAsync(dialog)!=ContentDialogResult.Primary)return;
            var picker=new FileSavePicker{SuggestedFileName="Rocker-diagnostika-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"),SuggestedStartLocation=PickerLocationId.DocumentsLibrary};picker.FileTypeChoices.Add("Diagnostika JSON",new List<string>{".json"});WinRT.Interop.InitializeWithWindow.Initialize(picker,DialogWindowHandle);var file=await picker.PickSaveFileAsync();if(file is null || closed)return;await Windows.Storage.FileIO.WriteTextAsync(file,json);diagnosticStatus!.Text="Diagnostika byla uložená. Zvuk běžel bez změny zařízení.";
        }catch(Exception error){if(!closed)ShowPersistenceError("Diagnostiku nelze uložit. "+error.Message);}finally{if(!closed)SetBusy(false);}
    }
    private async Task VerifyDiagnosticsAsync()
    {
        var json=await CaptureDiagnosticsAsync();using var value=JsonDocument.Parse(json);bool unmeasured=!value.RootElement.GetProperty("physicalRoundtripMeasured").GetBoolean() && value.RootElement.GetProperty("driverXrunCount").ValueKind==JsonValueKind.Null;
        WriteShellDiagnostic("diagnostics-view.json",new{result=unmeasured && diagnosticStatus is not null?"passed":"failed",cachedCountersRead=true,unavailableDriverXrunsNotZero=unmeasured,fixedReportSchema=true,manualExportPickerVerified=false,physicalAudioVerified=false});if(!unmeasured || diagnosticStatus is null)throw new InvalidOperationException("Diagnostic report or controls failed.");
    }
}
