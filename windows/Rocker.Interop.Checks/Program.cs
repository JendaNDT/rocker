// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Rocker.Interop;

if (args.Length is < 1 or > 2)
    throw new ArgumentException("Supply a test library path and optional --mismatched-abi or --no-device.");
var mode = args.Length == 2 ? args[1] : "--fake-device";
if (mode is not ("--fake-device" or "--mismatched-abi" or "--no-device")) throw new ArgumentException("Unknown check mode.");
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
