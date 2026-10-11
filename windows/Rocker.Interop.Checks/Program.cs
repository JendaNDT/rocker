// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Rocker.Interop;

if (args.Length is < 1 or > 3)
    throw new ArgumentException("Supply a test library path and optional --mismatched-abi or --no-device.");
var mode = args.Length >= 2 ? args[1] : "--fake-device";
if (mode is not ("--fake-device" or "--mismatched-abi" or "--no-device" or "--media")) throw new ArgumentException("Unknown check mode.");
if (mode != "--no-device" && !Path.GetFileName(args[0]).Contains("rocker_audio_test", StringComparison.Ordinal))
    throw new ArgumentException("Fake and ABI checks require the separately named test library.");
NativeLibrary.SetDllImportResolver(typeof(NativeAudio).Assembly,
    (name, _, _) => name == "rocker_audio" ? NativeLibrary.Load(Path.GetFullPath(args[0])) : IntPtr.Zero);
void Check(bool value, string message) { if (!value) throw new Exception(message); }
[MethodImpl(MethodImplOptions.NoInlining)]
static void AbandonAudioHandle()
{
    var audio = new NativeAudio();
    _ = audio.GetSnapshot();
    GC.KeepAlive(audio);
}
Check(Marshal.SizeOf<AudioSnapshot>() == 40, "C# snapshot layout");
Check(Marshal.SizeOf<TransportSnapshot>() == 56, "C# transport layout");
if (mode == "--media")
{
    if (args.Length != 3) throw new ArgumentException("Media checks need the repository root.");
    byte[] midi = [77,84,104,100,0,0,0,6,0,0,0,1,1,224,77,84,114,107,0,0,0,20,
        0,255,81,3,7,161,32,0,144,69,100,131,96,128,69,0,0,255,47,0];
    using (var audio = new NativeAudio())
    {
        await audio.PrepareMidiAsync(midi);
        var transport = audio.GetTransport();
        Check(transport.Ready == 1 && transport.Playing == 0 && transport.DurationSeconds > 0.57, "MIDI preparation through real P/Invoke");
        audio.Play(); audio.Play();
        Check(audio.GetTransport().Playing == 1, "Common media playback");
        audio.Stop(); audio.Seek(0.2); audio.SetVolume(0.4);
        transport = audio.GetTransport();
        Check(Math.Abs(transport.PositionSeconds - 0.2) < 1e-6 && Math.Abs(transport.Volume - 0.4) < 1e-6, "Seek and volume layout");
        await audio.PrepareFileAsync(Path.Combine(args[2], "docs/windows/baseline/fixtures/audio/a4-440.wav"), 0.5);
        Check(Math.Abs(audio.GetTransport().DurationSeconds - 2) < 0.001, "UTF-8 file preparation and stretch");
        try { await audio.PrepareFileAsync(Path.Combine(args[2], "missing.wav")); throw new Exception("Missing media accepted"); }
        catch (InvalidOperationException error) { Check(error.Message.Contains("stav 6", StringComparison.Ordinal), "Worker error copied before releasing handle"); }
        Check(audio.GetTransport().Ready == 1, "Failed load preserves PCM");
        foreach (var file in new[] { "notation-phrases.gp.mid", "notation-scale.musicxml.mid" })
        {
            await audio.PrepareMidiAsync(File.ReadAllBytes(Path.Combine(args[2], "windows/out/reports", file)));
            Check(audio.GetTransport().DurationSeconds > 0.5, "Actual AlphaTab MIDI is synthesized by C++");
        }
    }
    // Retain the handle before queueing work, then dispose immediately. This
    // exercises the shutdown race without timing-dependent delays or a device.
    for (var i = 0; i < 10; ++i)
    {
        var audio = new NativeAudio();
        var pending = audio.PrepareMidiAsync(midi, 0.5);
        audio.Dispose();
        await pending;
        try { audio.GetTransport(); throw new Exception("Disposed worker handle reused"); }
        catch (ObjectDisposedException) { }
    }
    Console.WriteLine("PASS: media/MIDI P/Invoke, AlphaTab synthesis, transport, worker errors and disposal during preparation (fake device)");
    return;
}
if (mode == "--mismatched-abi")
{
    try { using var audio = new NativeAudio(); throw new Exception("Incompatible library was accepted"); }
    catch (InvalidOperationException error) { Check(error.Message.Contains("jiné verzi", StringComparison.Ordinal), "Managed ABI rejection"); }
    Console.WriteLine("PASS: incompatible native library rejected before handle creation");
    return;
}
if (mode == "--no-device")
{
    // Run only with a real JUCE build that has all device backends disabled.
    for (var attempt = 0; attempt < 5; attempt++)
    {
        using var audio = new NativeAudio();
        try { audio.Start(); throw new Exception("Expected a headless build with no device backend"); }
        catch (InvalidOperationException) { }
        Check(audio.GetSnapshot().State == AudioState.Faulted, "JUCE missing device state");
        audio.Stop();
        Check(audio.GetSnapshot().State == AudioState.Stopped, "JUCE stop after failure");
    }
    AbandonAudioHandle();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    using (var recovered = new NativeAudio())
        Check(recovered.GetSnapshot().State == AudioState.Stopped, "SafeHandle finalization released the singleton JUCE runtime");
    Console.WriteLine("PASS: real JUCE message thread, no-device errors, disposal and SafeHandle finalization (no audio device)");
    return;
}
for (var i = 0; i < 20; i++)
{
    using var audio = new NativeAudio();
    audio.Start(); audio.Start();
    var state = audio.GetSnapshot();
    Check(state.State == AudioState.Running && state.SampleRate == 48000 && state.OutputChannels == 2
        && state.RenderedFrames == 1024, "Native snapshot through P/Invoke");
    audio.Stop(); audio.Stop();
    Check(audio.GetSnapshot().State == AudioState.Stopped, "Repeated stop");
    audio.Start();
    audio.Dispose(); audio.Dispose();
    try { audio.Start(); throw new Exception("Disposed handle reused"); }
    catch (ObjectDisposedException) { }
}
for(var attempt=0;attempt<20;attempt++)
{
    await using var audio=new NativeAudio();var starting=audio.StartAsync();var stopping=audio.StopAsync();await Task.WhenAll(starting,stopping);
    Check(audio.GetSnapshot().State==AudioState.Stopped,"stop before worker scheduling invalidates reserved start");
}
await using(var deviceAudio=new NativeAudio())
{
    var devices=await deviceAudio.GetDevicesAsync();Check(devices.Backends.Length==1 && devices.Backends[0].Name=="Test ASIO","device catalog uses the real native query");
    var choice=new DeviceConfiguration("Test ASIO","Test interface","Test interface",44100,128,1);
    var caps=await deviceAudio.GetCapabilitiesAsync(choice);Check(caps.Inputs.Length==2 && caps.Rates.Contains(48000) && caps.Buffers.Contains(128u),"physical channel names and driver formats cross the ABI");
    await deviceAudio.ConfigureDeviceAsync(choice);var detail=await deviceAudio.GetDeviceDetailsAsync();
    Check(detail.SampleRate==44100 && detail.BufferFrames==128 && detail.InputLatencyFrames==32 && deviceAudio.GetSnapshot().State==AudioState.Stopped,"configuration validates without starting sound");
    try{await deviceAudio.ConfigureDeviceAsync(choice with{BufferFrames=512});throw new Exception("Unsupported driver format accepted");}
    catch(InvalidOperationException){ }
    detail=await deviceAudio.GetDeviceDetailsAsync();Check(detail.SampleRate==44100 && detail.BufferFrames==128,"failed configuration preserves the previous selection");
    try{await deviceAudio.GetCapabilitiesAsync(choice with{Output="Missing interface"});throw new Exception("Missing named device accepted");}
    catch(InvalidOperationException){ }
    try{await deviceAudio.ConfigureDeviceAsync(choice with{InputChannel=64});throw new Exception("Invalid physical channel accepted");}
    catch(ArgumentException){ }
    await deviceAudio.ShowDeviceControlPanelAsync();
    await deviceAudio.StartAsync();var active=deviceAudio.GetSnapshot();Check(active.SampleRate==44100 && active.BufferFrames==128,"confirmed format is actually used by the driver");await deviceAudio.StopAsync();
}
// Slow vendor initialization stays on a retained managed worker. Stop and polling remain responsive.
var delayedLibrary=NativeLibrary.Load(Path.GetFullPath(args[0]));
var setDelay=Marshal.GetDelegateForFunctionPointer<SetDelay>(NativeLibrary.GetExport(delayedLibrary,"rocker_test_set_start_delay"));
setDelay(1000);
try
{
    await using var audio=new NativeAudio();var start=audio.StartAsync();
    var deadline=System.Diagnostics.Stopwatch.StartNew();
    while(audio.GetSnapshot().State!=AudioState.Starting && deadline.ElapsedMilliseconds<3000)await Task.Delay(1);
    Check(audio.GetSnapshot().State==AudioState.Starting,"managed starting state");
    var poll=System.Diagnostics.Stopwatch.StartNew();_ = audio.GetTransport();var stopping=audio.StopAsync();
    Check(poll.ElapsedMilliseconds<200,"managed polling and stop submission do not wait for slow driver");
    await Task.WhenAll(start,stopping);Check(audio.GetSnapshot().State==AudioState.Stopped,"slow start cancelled through P/Invoke");
}
finally{setDelay(0);NativeLibrary.Free(delayedLibrary);}
// The failure switch is exported only by the separate test backend.
var library = NativeLibrary.Load(Path.GetFullPath(args[0]));
var setFailure = Marshal.GetDelegateForFunctionPointer<SetFailure>(NativeLibrary.GetExport(library, "rocker_test_set_failure"));
setFailure(1);
try
{
    using var audio = new NativeAudio();
    try { audio.Start(); throw new Exception("Device failure was hidden"); }
    catch (InvalidOperationException e) { Check(e.Message.Contains("zařízení", StringComparison.Ordinal), "UTF-8 error copy"); }
    Check(audio.GetSnapshot().State == AudioState.Faulted, "Device failure state");
    setFailure(0);
    audio.Start();
    Check(audio.GetSnapshot().State == AudioState.Running, "Recovery after device failure");
}
finally { setFailure(0); NativeLibrary.Free(library); }
Console.WriteLine("PASS: real C# to C++ calls, layout, UTF-8 errors, start/stop, disposal and recovery (fake device)");

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void SetFailure(int enabled);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void SetDelay(uint milliseconds);
