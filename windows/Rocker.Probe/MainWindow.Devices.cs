// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Interop;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private DeviceCatalog? deviceCatalog;
    private DeviceCapabilities? deviceCapabilities;
    private bool deviceUiUpdating,deviceOperationRunning,deviceSelectionUnavailable;
    private sealed record DeviceName(string Label,string Name);
    private sealed record DeviceNumber(string Label,double Value);
    private void DeviceBusy(bool value){deviceOperationRunning=value;DeviceControls.IsEnabled=!value;if(!closed)RefreshState();}
    private async Task DeviceOperationAsync(Func<Task> operation)
    {
        if(deviceOperationRunning || closingWindow || audio is null)return;
        DeviceBusy(true);
        try{CancelPalm();await StopRhythmUiAsync();++playbackIntent;await audio.StopAsync();if(!await StopCaptureAsync())return;await operation();}
        catch(Exception error){if(!closed)DeviceSelectionStatus.Text="Zařízení nelze změnit. "+error.Message;}
        finally{if(!closed)DeviceBusy(false);}
    }
    private async void DevicesRefresh_Click(object sender,RoutedEventArgs args)=>await DeviceOperationAsync(RefreshDeviceListAsync);
    private async Task RefreshDeviceListAsync()
    {
        deviceCatalog=await audio!.GetDevicesAsync();if(closed)return;
        deviceUiUpdating=true;
        DeviceBackendBox.ItemsSource=deviceCatalog.Backends.Select(b=>b.Name).ToArray();
        var saved=appSettings.AudioDevice;DeviceBackendBox.SelectedItem=saved?.Backend;
        if(DeviceBackendBox.SelectedIndex<0 && deviceCatalog.Backends.Length>0)DeviceBackendBox.SelectedIndex=0;
        deviceUiUpdating=false;UpdateDeviceNames();
        DeviceSelectionStatus.Text=deviceCatalog.Backends.Any(b=>b.Outputs.Length>0)?"Vyber kartu a načti její možnosti. Změny se použijí až tlačítkem Ověřit a použít.":"Není dostupný žádný výstup. Připoj kartu a obnov seznam.";
    }
    private void DeviceBackend_Changed(object sender,SelectionChangedEventArgs args){if(!deviceUiUpdating)UpdateDeviceNames();}
    private DeviceBackend? SelectedBackend()=>deviceCatalog?.Backends.FirstOrDefault(b=>b.Name==DeviceBackendBox.SelectedItem as string);
    private void UpdateDeviceNames()
    {
        deviceUiUpdating=true;var backend=SelectedBackend();var saved=appSettings.AudioDevice;
        DeviceInputBox.ItemsSource=new[]{new DeviceName("Bez vstupu kytary","")}.Concat(backend?.Inputs.Select(n=>new DeviceName(n,n))??[]).ToArray();
        DeviceOutputBox.ItemsSource=backend?.Outputs.Select(n=>new DeviceName(n,n)).ToArray()??[];
        DeviceInputBox.SelectedIndex=0;DeviceOutputBox.SelectedIndex=-1;
        if(saved?.Backend==backend?.Name){DeviceInputBox.SelectedIndex=Math.Max(0,Array.IndexOf(backend?.Inputs??[],saved!.Input)+1);DeviceOutputBox.SelectedIndex=Array.IndexOf(backend?.Outputs??[],saved.Output);}
        if(DeviceOutputBox.SelectedIndex<0 && backend?.Outputs.Length>0)DeviceOutputBox.SelectedIndex=0;
        deviceUiUpdating=false;InvalidateCapabilities();
    }
    private void DeviceNames_Changed(object sender,SelectionChangedEventArgs args)
    {
        if(deviceUiUpdating)return;
        if(SelectedBackend() is {SeparateDevices:false} && DeviceInputBox.SelectedItem is DeviceName {Name.Length:>0} && DeviceOutputBox.SelectedItem is DeviceName output)
        {
            deviceUiUpdating=true;
            DeviceInputBox.SelectedItem=((DeviceName[])DeviceInputBox.ItemsSource).FirstOrDefault(n=>n.Name==output.Name);
            deviceUiUpdating=false;
        }
        InvalidateCapabilities();
    }
    private void InvalidateCapabilities(){deviceCapabilities=null;ApplyDeviceButton.IsEnabled=false;DeviceChannelBox.ItemsSource=null;DevicePairBox.ItemsSource=null;DeviceRateBox.ItemsSource=null;DeviceBufferBox.ItemsSource=null;}
    private DeviceConfiguration SelectedDevice(bool includeParameters=false)
    {
        if(DeviceBackendBox.SelectedItem is not string backend || DeviceOutputBox.SelectedItem is not DeviceName output)throw new InvalidOperationException("Vyber ovladač a výstupní zařízení.");
        string input=(DeviceInputBox.SelectedItem as DeviceName)?.Name??"";
        return new(backend,input,output.Name,
            includeParameters?(DeviceRateBox.SelectedItem as DeviceNumber)?.Value??0:0,
            includeParameters?(uint)((DeviceBufferBox.SelectedItem as DeviceNumber)?.Value??0):0,
            includeParameters?(int)((DeviceChannelBox.SelectedItem as DeviceNumber)?.Value??-1):-1,
            includeParameters?(uint)((DevicePairBox.SelectedItem as DeviceNumber)?.Value??0):0);
    }
    private async void DeviceInspect_Click(object sender,RoutedEventArgs args)=>await DeviceOperationAsync(async ()=> {
        var choice=SelectedDevice();deviceCapabilities=await audio!.GetCapabilitiesAsync(choice);if(closed)return;
        var caps=deviceCapabilities;var saved=appSettings.AudioDevice;
        DeviceChannelBox.ItemsSource=new[]{new DeviceNumber("Bez vstupu",-1)}.Concat(choice.Input.Length==0?[]:caps.Inputs.Select((n,i)=>new DeviceNumber($"{i+1}: {n}",i))).ToArray();
        DeviceChannelBox.SelectedIndex=0;
        if(saved?.Backend==choice.Backend && saved.Input==choice.Input && saved.InputChannel>=0 && saved.InputChannel<caps.Inputs.Length)DeviceChannelBox.SelectedIndex=saved.InputChannel+1;
        DevicePairBox.ItemsSource=Enumerable.Range(0,(caps.Outputs.Length+1)/2).Select(i=>new DeviceNumber(caps.Outputs.Length==1?caps.Outputs[0]:$"{i*2+1} + {i*2+2}",i*2)).Where(n=>caps.Outputs.Length==1 || n.Value+1<caps.Outputs.Length).ToArray();DevicePairBox.SelectedIndex=0;
        DeviceRateBox.ItemsSource=new[]{new DeviceNumber("Výchozí ovladače",0)}.Concat(caps.Rates.Distinct().Select(r=>new DeviceNumber($"{r:N0}",r))).ToArray();DeviceRateBox.SelectedIndex=0;
        DeviceBufferBox.ItemsSource=new[]{new DeviceNumber("Výchozí ovladače",0)}.Concat(caps.Buffers.Distinct().Select(b=>new DeviceNumber(b.ToString(),b))).ToArray();DeviceBufferBox.SelectedIndex=0;
        if(saved?.Backend==choice.Backend && saved.Output==choice.Output)
        {
            SelectNumber(DevicePairBox,saved.OutputFirst);SelectNumber(DeviceRateBox,saved.SampleRate);SelectNumber(DeviceBufferBox,saved.BufferFrames);
        }
        ApplyDeviceButton.IsEnabled=caps.Outputs.Length>0;
        DeviceSelectionStatus.Text="Možnosti pocházejí z ovladače. Výchozí hodnota se ověří při otevření; monitoring zůstane vypnutý.";
    });
    private static void SelectNumber(ComboBox box,double value){var item=((DeviceNumber[])box.ItemsSource).FirstOrDefault(n=>n.Value==value);if(item is not null)box.SelectedItem=item;}
    private async void DeviceApply_Click(object sender,RoutedEventArgs args)=>await DeviceOperationAsync(async ()=> {
        if(deviceCapabilities is null)throw new InvalidOperationException("Nejdřív načti možnosti zařízení.");
        var choice=SelectedDevice(true);await audio!.ConfigureDeviceAsync(choice);if(closed)return;
        deviceSelectionUnavailable=false;
        appSettings=appSettings with {AudioDevice=new(choice.Backend,choice.Input,choice.Output,choice.SampleRate,choice.BufferFrames,choice.InputChannel,choice.OutputFirst)};SaveSettings();
        await ShowDeviceDetailsAsync();
    });
    private async void DeviceControlPanel_Click(object sender,RoutedEventArgs args)=>await DeviceOperationAsync(async ()=> {await audio!.ShowDeviceControlPanelAsync();if(!closed)await ShowDeviceDetailsAsync();});
    private async Task ShowDeviceDetailsAsync()
    {
        var details=await audio!.GetDeviceDetailsAsync();if(closed)return;
        DevicePanelButton.IsEnabled=details.ControlPanel;
        DeviceSelectionStatus.Text=$"Ověřeno: {details.Backend} · {details.Output}\n{details.SampleRate:N0} Hz · buffer {details.BufferFrames} vzorků · aktivní vstupy {details.InputChannels}, výstupy {details.OutputChannels}\nOdhad ovladače: vstup {details.InputLatencyFrames} a výstup {details.OutputLatencyFrames} vzorků. Nejde o měření celé odezvy. Zařízení je po ověření uvolněné; zvuk spustíš výslovně.";
    }
    private async Task VerifyDevicesAsync()
    {
        if(audio is null)throw new InvalidOperationException("Audio host missing.");
        var value=await audio.GetDevicesAsync();
        bool backendContract=value.Backends.Any(b=>b.Name=="Windows Audio") && value.Backends.Any(b=>b.Name=="Windows Audio (Exclusive Mode)") && value.Backends.Any(b=>b.Name=="ASIO");
        WriteShellDiagnostic("devices-view.json",new {result=backendContract?"passed":"failed",backends=value.Backends.Select(b=>b.Name).ToArray(),listedInputs=value.Backends.Sum(b=>b.Inputs.Length),listedOutputs=value.Backends.Sum(b=>b.Outputs.Length),deviceOpened=false,physicalDriverVerified=false,manualSelectionVerified=false});
        if(!backendContract)throw new InvalidOperationException("Expected compiled Windows audio backends were not enumerated.");
    }
    private async Task RestoreDevicePreferenceAsync()
    {
        if(appSettings.AudioDevice is not { } saved || audio is null)return;
        DeviceBusy(true);
        try{await audio.ConfigureDeviceAsync(new(saved.Backend,saved.Input,saved.Output,saved.SampleRate,saved.BufferFrames,saved.InputChannel,saved.OutputFirst));deviceSelectionUnavailable=false;await ShowDeviceDetailsAsync();}
        catch(Exception error){deviceSelectionUnavailable=true;DeviceSelectionStatus.Text="Uložená karta není dostupná. Obnov seznam a vyber náhradu; zvuk se automaticky nepřepnul. "+error.Message;}
        finally{if(!closed)DeviceBusy(false);}
    }
}
