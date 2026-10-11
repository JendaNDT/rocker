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
Check(Marshal.SizeOf<MeterSnapshot>()==112,"C# meter layout");
Check(Marshal.SizeOf<ClockSnapshot>()==72,"C# clock layout");
Check(Marshal.SizeOf<MetronomeSnapshot>()==80,"C# metronome observation layout");
Check(Marshal.SizeOf<AnalysisSnapshot>()==144,"C# analysis snapshot layout");
Check(Marshal.SizeOf<LoopSnapshot>()==120,"C# looper snapshot layout");
Check(Marshal.SizeOf<RhythmSnapshot>()==72,"native rhythm snapshot layout");
Check(Marshal.SizeOf<PreviewNote>()==32,"native teaching note layout");
Check(Marshal.SizeOf<ClipInfo>()==32 && Marshal.SizeOf<RecordingAlignment>()==64,"source info and recording alignment layout");
Check(Marshal.SizeOf<RecordingSnapshot>()==88,"C# recording snapshot layout");
Check(Marshal.SizeOf<DiagnosticSnapshot>()==160,"C# diagnostics snapshot layout");
if (mode == "--media")
{
    if (args.Length != 3) throw new ArgumentException("Media checks need the repository root.");
    byte[] midi = [77,84,104,100,0,0,0,6,0,0,0,1,1,224,77,84,114,107,0,0,0,20,
        0,255,81,3,7,161,32,0,144,69,100,131,96,128,69,0,0,255,47,0];
    using (var audio = new NativeAudio())
    {
        await audio.PrepareMidiAsync(midi);
        var transport = audio.GetTransport();
        await audio.SetLoopAsync(0.1,0.3,true);Check(audio.GetClock().Flags==2,"loop configuration never starts audio");await audio.SetLoopAsync(0,0,false);
        Check(transport.Ready == 1 && transport.Playing == 0 && transport.DurationSeconds > 0.57, "MIDI preparation through real P/Invoke");
        var orderingLibrary=NativeLibrary.Load(Path.GetFullPath(args[0]));try{var process=Marshal.GetDelegateForFunctionPointer<ProcessSamples>(NativeLibrary.GetExport(orderingLibrary,"rocker_test_process"));
            for(int ordering=0;ordering<30;++ordering){var play=audio.PlayAsync();var pause=audio.PauseAsync();await Task.WhenAll(play,pause);process(1,0);for(int retry=0;retry<100 && audio.GetTransport().Playing!=0;++retry)await Task.Delay(5);Check(audio.GetTransport().Playing==0,"ordered controls preserve final pause at callback boundary");var one=audio.SeekAsync(.1);var two=audio.SeekAsync(.2);await Task.WhenAll(one,two);process(1,0);for(int retry=0;retry<100 && Math.Abs(audio.GetTransport().PositionSeconds-.2)>1e-6;++retry)await Task.Delay(5);Check(Math.Abs(audio.GetTransport().PositionSeconds-.2)<1e-6,"ordered seeks preserve last position at callback boundary");}
        }finally{NativeLibrary.Free(orderingLibrary);}
        audio.Stop();audio.Seek(0);
        audio.Play(); audio.Play();
        Check(audio.GetTransport().Playing == 1, "Common media playback");
        var beforeMetro=audio.GetClock();var positionBeforeMetro=audio.GetTransport().PositionSeconds;await audio.SetMetronomeAsync(new(Enabled:true));await audio.StartMetronomeAsync();
        Check(audio.GetClock().Epoch==beforeMetro.Epoch && audio.GetTransport().PositionSeconds==positionBeforeMetro && audio.GetTransport().Playing==1,"metronome joins backing without a cursor/epoch restart");
        await audio.SetMetronomeAsync(new());Check(audio.GetTransport().Playing==1,"metronome stop keeps backing active");
        audio.Stop(); audio.Seek(0.2); audio.SetVolume(0.4);
        transport = audio.GetTransport();
        Check(Math.Abs(transport.PositionSeconds - 0.2) < 1e-6 && Math.Abs(transport.Volume - 0.4) < 1e-6, "Seek and volume layout");
        await audio.PrepareFileAsync(Path.Combine(args[2], "docs/windows/baseline/fixtures/audio/a4-440.wav"), 0.5);
        Check(Math.Abs(audio.GetTransport().DurationSeconds - 2) < 0.001, "UTF-8 file preparation and stretch");
        try { await audio.PrepareFileAsync(Path.Combine(args[2], "missing.wav")); throw new Exception("Missing media accepted"); }
        catch (InvalidOperationException error) { Check(error.Message.Contains("stav 6", StringComparison.Ordinal), "Worker error copied before releasing handle"); }
        Check(audio.GetTransport().Ready == 1, "Failed load preserves PCM");
        foreach (var file in new[] { "notation-phrases.gp.mid", "notation-scale.musicxml.mid", "notation-original.gp3.mid", "notation-original.gp4.mid", "notation-original.gp5.mid", "notation-original.gpx.mid", "notation-original.mxl.mid", "notation-multitrack-track0.mid", "notation-multitrack-track1.mid", "notation-multitrack-track2.mid", "notation-multitrack-all.mid" })
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
await using(var captureAudio=new NativeAudio())
{
    var native=NativeLibrary.Load(Path.GetFullPath(args[0]));
    try{
        var process=Marshal.GetDelegateForFunctionPointer<ProcessSamples>(NativeLibrary.GetExport(native,"rocker_test_process"));
        await captureAudio.ConfigureDeviceAsync(new("Test ASIO","Test interface","Test interface",48000,128,0));
        var path=Path.Combine(Path.GetTempPath(),"rocker-clr-capture-"+Guid.NewGuid().ToString("N")+".wav");
        try{var token=await captureAudio.StartRecordingAsync(path,0);for(int i=0;i<3;++i)process(127,.25f);var finishing=captureAudio.StopRecordingAsync();process(128,.25f);await finishing;var state=captureAudio.GetRecording();Check(state.Token==token && state.Complete && state.WrittenFrames==381 && state.Channels==1,"C# recording captures actual selected mono input and final partial block");
            var exported=path+".export.wav";try{await MediaExport.WavAsync(path,exported,.001,.005);Check(new FileInfo(exported).Length==44+192*3,"C# offline WAV range exports exact samples");}finally{if(File.Exists(exported))File.Delete(exported);}
        }finally{if(File.Exists(path))File.Delete(path);}
        await captureAudio.LoopCommandAsync(1);process(672,.25f);await captureAudio.LoopCommandAsync(2);process(1,0);for(int retry=0;retry<100 && captureAudio.GetLoop().Layers!=1;++retry)await Task.Delay(5);Check(captureAudio.GetLoop().Frames==800 && captureAudio.GetLoop().Layers==1,"C# looper uses actual callback frames on the shared device");await captureAudio.StopAsync();
        var loopFile=Path.Combine(Path.GetTempPath(),"rocker-clr-loop-"+Guid.NewGuid().ToString("N")+".wav");try{await captureAudio.ExportLoopAsync(loopFile,0);await captureAudio.LoadLoopLayerAsync(loopFile,0,.3);await captureAudio.SetLoopMetadataAsync(1,true,64);var restored=captureAudio.GetLoop();Check(restored.Frames==800 && restored.State==6 && Math.Abs(restored.Gain0-.3)<1e-6 && (restored.Flags&1)!=0 && restored.Bus==1,"C# layer restoration preserves rate, gain and partial provenance without playback");}finally{if(File.Exists(loopFile))File.Delete(loopFile);}await captureAudio.LoopCommandAsync(7);
        var studioFile=Path.Combine(Path.GetTempPath(),"rocker-clr-studio-"+Guid.NewGuid().ToString("N")+".wav");
        try{await captureAudio.StartRecordingAsync(studioFile,0);process(960,.25f);var ending=captureAudio.StopRecordingAsync();process(1,0);await ending;await captureAudio.StopAsync();var tracks=new ProjectAudioTrack[]{new(studioFile,.5),new(studioFile,.5,-1,OffsetFrames:100,TrimStartFrames:20)};var ticket=await captureAudio.PrepareProjectAsync(tracks,48000);Check(captureAudio.GetClipInfo().Frames==1040 && (await captureAudio.GetProjectTrackInfoAsync(ticket,1)).Frames==960,"project descriptor copies UTF-8 paths and exact source frames through P/Invoke");await captureAudio.SetProjectTrackAsync(ticket,1,.25,1,false,true);
            var mixed=studioFile+".mixed.wav";try{await ProjectExport.WavAsync(tracks,48000,mixed,.002,.01);Check(new FileInfo(mixed).Length==44+384*6,"offline stereo project range preserves exact frame extent through P/Invoke");}finally{if(File.Exists(mixed))File.Delete(mixed);}
            var overdub=studioFile+".take.wav";try{await captureAudio.SeekAsync(0);await captureAudio.StartRecordingAsync(overdub,0,0,ticket);process(127,.125f);var finish=captureAudio.StopRecordingAsync();process(1,0);await finish;var placement=captureAudio.GetRecordingAlignment();Check(placement.Started && placement.Continuous && placement.FirstProjectFrame==0 && placement.MediaTicket==ticket,"first overdub sample records the backing source coordinate independently of UI timing");}finally{if(File.Exists(overdub))File.Delete(overdub);}
            await captureAudio.StopAsync();await captureAudio.ClearMediaAsync();Check(captureAudio.GetTransport().Ready==0,"project switch clears the old playable source");
        }finally{if(File.Exists(studioFile))File.Delete(studioFile);}
        await captureAudio.StartLiveAsync();var beforePhrase=captureAudio.GetClock();await captureAudio.PlayPreviewAsync([new(0,.2,110),new(.018,.2,165),new(.036,.2,220)]);process(128,.125f);var afterPhrase=captureAudio.GetClock();Check(beforePhrase.Epoch==afterPhrase.Epoch && afterPhrase.Frame>=beforePhrase.Frame,"polyphonic teaching phrase joins the existing live graph without another device epoch");await captureAudio.StopPreviewAsync();process(1024,0);await captureAudio.StopAsync();
        var pcmProcess=Marshal.GetDelegateForFunctionPointer<ProcessPcmSamples>(NativeLibrary.GetExport(native,"rocker_test_process_pcm"));
        await captureAudio.StartLiveAsync();var gate=new Rocker.Domain.SurgicalNoteGate();gate.Reset(new(captureAudio.GetClock().Epoch,0,48000,null,true,true,0,0,.171));var phrase=Rocker.Domain.SurgicalEngine.Start(new("repeat-note","Repeat note","",[40,40,42]));var accepted=new List<int>();ulong expectedFrame=captureAudio.GetClock().Frame;uint expectedEpoch=captureAudio.GetClock().Epoch;
        foreach(var tone in new[]{(Midi:40,Seconds:.8),(Midi:0,Seconds:.03),(Midi:40,Seconds:.8),(Midi:0,Seconds:.18),(Midi:42,Seconds:.8)})
        {
            int length=(int)Math.Round(tone.Seconds*48000);for(int offset=0;offset<length;offset+=256){int count=Math.Min(256,length-offset);var pcm=new float[count];for(int i=0;i<count;++i)pcm[i]=tone.Midi==0?0:(float)(.2*Math.Sin(2*Math.PI*Rocker.Domain.Music.MidiToFrequency(tone.Midi)*(offset+i)/48000));pcmProcess((uint)count,pcm);expectedFrame+=(uint)count;AnalysisSnapshot observed=default;for(int retry=0;retry<200;++retry){observed=captureAudio.GetAnalysis();if(observed.Epoch==expectedEpoch && observed.Frame>=expectedFrame)break;await Task.Delay(2);}Check(observed.Epoch==expectedEpoch && observed.Frame>=expectedFrame,"prepared guitar signal reaches the real off-thread analyzer without dropped callback data");int? midi=observed.PitchDetected?Rocker.Domain.Music.FrequencyToNote(observed.Frequency)?.Midi:null;var played=gate.Observe(new(observed.Epoch,observed.Frame,observed.SampleRate,midi,observed.ActiveInput&&!observed.Stale,!observed.PitchDetected&&observed.Rms*3<.06,observed.HitCount,observed.OnsetFrame,observed.WindowSeconds));if(played is int value){accepted.Add(value);phrase=Rocker.Domain.SurgicalEngine.FeedNote(phrase,value);}}
        }
        Check(accepted.SequenceEqual(new[]{40,40,42}) && phrase.Phase==Rocker.Domain.SurgicalPhase.Complete,"actual PCM analysis distinguishes two attacks of the same note without advancing repeatedly on a held note");await captureAudio.StopAsync();
        await captureAudio.StartLiveAsync();var rhythmEpoch=captureAudio.GetClock().Epoch;var rhythmToken=await captureAudio.StartRhythmAsync([new(0,1,110,.2)],1,1,120,true,0);for(int block=0;block<3;++block)process(16000,0);
        RhythmSnapshot rhythm=default;for(int retry=0;retry<100;++retry){rhythm=captureAudio.GetRhythm();if(rhythm.Token==rhythmToken && rhythm.Rounds>=1)break;await Task.Delay(5);}
        Check(rhythm.Epoch==rhythmEpoch && rhythm.Rounds==1 && rhythm.Bpm==126 && rhythm.LastCompletedBpm==120,$"musical trainer joins one device and completes a round at the actual native tempo: epoch {rhythm.Epoch}/{rhythmEpoch}, token {rhythm.Token}/{rhythmToken}, rounds {rhythm.Rounds}, BPM {rhythm.Bpm}, last {rhythm.LastCompletedBpm}, start {rhythm.StartFrame}, elapsed {rhythm.ElapsedBeats}");
        try{await captureAudio.SetMetronomeAsync(new(Enabled:true));throw new Exception("trainer tempo overwritten");}catch(InvalidOperationException){}
        await captureAudio.StopRhythmAsync();process(1024,0);for(int retry=0;retry<100 && captureAudio.GetRhythm().Active;++retry)await Task.Delay(5);Check(!captureAudio.GetRhythm().Active,"CLR cancellation reaches the rhythm actor");await captureAudio.StopAsync();
        // Calibration uses real prepared PCM through the analyzer, not direct fabricated centroid values.
        await captureAudio.StartLiveAsync();expectedFrame=captureAudio.GetClock().Frame;expectedEpoch=captureAudio.GetClock().Epoch;var openHits=new List<double>();var mutedHits=new List<double>();ulong lastPalmHit=0;
        async Task FeedPalm(double frequency,double seconds,List<double>? results)
        {
            int frames=(int)Math.Round(seconds*48000);for(int offset=0;offset<frames;offset+=256){int count=Math.Min(256,frames-offset);var pcm=new float[count];for(int i=0;i<count;++i)pcm[i]=frequency==0?0:(float)(.2*Math.Sin(2*Math.PI*frequency*(offset+i)/48000));pcmProcess((uint)count,pcm);expectedFrame+=(uint)count;AnalysisSnapshot observed=default;for(int retry=0;retry<200;++retry){observed=captureAudio.GetAnalysis();if(observed.Epoch==expectedEpoch && observed.Frame>=expectedFrame)break;await Task.Delay(2);}Check(observed.Epoch==expectedEpoch && observed.Frame>=expectedFrame,"prepared palm signal reaches analyzer");if(observed.HitCount>lastPalmHit){lastPalmHit=observed.HitCount;results?.Add(observed.HitCentroid);}}
        }
        for(int hit=0;hit<3;++hit){await FeedPalm(0,.2,null);await FeedPalm(2400,.3,openHits);await FeedPalm(0,.2,null);await FeedPalm(600,.3,mutedHits);}
        var palmCalibration=Rocker.Domain.PalmMuteRules.Calibrate(openHits,mutedHits);Check(openHits.Count==3 && mutedHits.Count==3 && palmCalibration is not null,"actual centroid capture supplies three independent samples per technique");Check(openHits.All(x=>Rocker.Domain.PalmMuteRules.ClassifyHit(x,palmCalibration!)=="open") && mutedHits.All(x=>Rocker.Domain.PalmMuteRules.ClassifyHit(x,palmCalibration!)=="muted"),"prepared open and muted tones calibrate and classify through the complete PCM/CLR/domain path");await captureAudio.StopAsync();
        var failed=captureAudio.SetAmplifierAsync(new(Drive:double.NaN));var valid=captureAudio.SetAmplifierAsync(new());try{await failed;throw new Exception("invalid amp accepted");}catch(InvalidOperationException){}await valid;Check(captureAudio.GetAmplifier()==new AmplifierConfiguration(),"a failed queued control does not poison its successor");
    }finally{NativeLibrary.Free(native);}
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
    var clock=audio.GetClock();Check(clock.Frame==1024 && clock.Epoch>0 && !clock.Running && clock.SampleRate==48000,"common sample clock includes output-only callbacks");
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
for(var attempt=0;attempt<20;attempt++)
{
    await using var audio=new NativeAudio();var stopping=audio.StopAsync();var starting=audio.StartAsync();await Task.WhenAll(stopping,starting);
    Check(audio.GetSnapshot().State==AudioState.Running,"an older unscheduled stop never cancels the newer reserved start");
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
    var ampSettings=new AmplifierConfiguration(true,2,1,0.4,0.7,0.2,-0.1,0.3,0.2,0.3,0.2,0.5);
    await deviceAudio.SetAmplifierAsync(ampSettings);Check(deviceAudio.GetAmplifier()==ampSettings,"all amplifier parameters and enable state round-trip through C++");
    try{await deviceAudio.SetAmplifierAsync(ampSettings with{Drive=double.NaN});throw new Exception("NaN amp parameter accepted");}catch(InvalidOperationException){}
    Check(deviceAudio.GetAmplifier()==ampSettings,"invalid amplifier parameters preserve the previous configuration");
    deviceAudio.SetTempo(137);
    await deviceAudio.StartLiveAsync();Check(deviceAudio.GetSnapshot().Reserved==2 && !deviceAudio.GetMeters().Monitoring,"Live starts without monitoring");
    Check(deviceAudio.GetClock().Bpm==137 && deviceAudio.GetClock().Frame==128 && deviceAudio.GetClock().Running,"tempo and sample count cross the ABI independently of UI timing");
    Check(deviceAudio.GetClock().DspLatencyFrames==15,"enabled amplifier publishes FIR group delay independently of driver estimates");
    deviceAudio.SetMonitoring(true,0.15);Check(deviceAudio.GetMeters().Monitoring,"explicit monitor control crosses the ABI");
    await deviceAudio.StopAsync();Check(!deviceAudio.GetMeters().Monitoring && deviceAudio.GetMeters().InputPeak==0,"stop clears monitoring and stale level");
    await deviceAudio.StartAsync();var active=deviceAudio.GetSnapshot();Check(active.SampleRate==44100 && active.BufferFrames==128,"confirmed format is actually used by the driver");await deviceAudio.StopAsync();
}
await using(var metroAudio=new NativeAudio())
{
    metroAudio.SetNoiseGate(.08);var initialAnalysis=metroAudio.GetAnalysis();Check(!initialAnalysis.ActiveInput && initialAnalysis.Frequency==0,"no device does not invent a measured note");
    try{metroAudio.SetNoiseGate(double.NaN);throw new Exception("NaN gate accepted");}catch(ArgumentException){}
    try{metroAudio.CalibrateNoise();throw new Exception("Calibration without input accepted");}catch(InvalidOperationException){}
    var metro=new MetronomeConfiguration(Enabled:true,Bpm:123,Beats:7,Subdivision:4,Sound:1,SubSound:3,CountIn:2,Ramp:true,RampEvery:3,RampStep:7,RampTarget:201,Gap:true,AudibleBars:2,SilentBars:3,Poly:true,PolyTicks:5,PolySound:2,Volume:.7,AccentVolume:.9,Swing:.65,PolyVolume:.4,Accents:[2,1,0,1,2,1,1],Sounds:[1,5,2,3,4,5,0]);
    await metroAudio.SetMetronomeAsync(metro);var roundtrip=metroAudio.GetMetronome();
    Check(roundtrip with{Accents=null,Sounds=null}==metro with{Accents=null,Sounds=null} && roundtrip.Accents!.Take(7).SequenceEqual(metro.Accents!) && roundtrip.Sounds!.Take(7).SequenceEqual(metro.Sounds!),"all 240 bytes of metronome configuration cross the ABI");
    try{await metroAudio.SetMetronomeAsync(metro with{Swing=double.NaN});throw new Exception("NaN metronome accepted");}catch(InvalidOperationException){}
    Check(metroAudio.GetMetronome().Bpm==123,"invalid metronome preserves earlier configuration");
    await metroAudio.StartMetronomeAsync();await metroAudio.RefreshDiagnosticsAsync();var diagnostic=metroAudio.GetDiagnostics();Check(diagnostic.Callbacks==1 && diagnostic.DeviceStarts==1 && diagnostic.SampleRate==48000 && diagnostic.MinFrames>0 && diagnostic.MaxNanoseconds>0 && !diagnostic.DriverXrunsKnown && diagnostic.DriverXruns==-1,"native timing counters and unavailable vendor underruns cross the ABI honestly");var xrunLibrary=NativeLibrary.Load(Path.GetFullPath(args[0]));try{var setXruns=Marshal.GetDelegateForFunctionPointer<SetXruns>(NativeLibrary.GetExport(xrunLibrary,"rocker_test_set_xruns"));setXruns(7);await metroAudio.RefreshDiagnosticsAsync();Check(metroAudio.GetDiagnostics().DriverXrunsKnown && metroAudio.GetDiagnostics().DriverXruns==7,"explicit vendor count stays distinct from over-budget callback timings");setXruns(-1);}finally{NativeLibrary.Free(xrunLibrary);}
    var state=metroAudio.GetSnapshot();Check(state.State==AudioState.Running && state.Reserved==3,"metronome starts without a guitar or test tone");
    var observed=metroAudio.GetMetronomeSnapshot();Check(observed.Running && observed.Enabled && observed.CountIn && observed.Bar==-2 && observed.PrimaryEvents>0,"count-in metadata crosses the ABI");
    await metroAudio.StopAsync();Check(!metroAudio.GetMetronome().Enabled && !metroAudio.GetMetronomeSnapshot().Enabled,"global stop disarms metronome");
    await metroAudio.StartAsync();Check(!metroAudio.GetMetronomeSnapshot().Enabled,"a subsequent tone does not resurrect stopped clicks");
    var before=metroAudio.GetClock();await metroAudio.SetMetronomeAsync(metro with{CountIn=0});await metroAudio.StartMetronomeAsync();var after=metroAudio.GetClock();
    Check(before.Epoch==after.Epoch && before.Frame==after.Frame && metroAudio.GetSnapshot().Reserved==0,"metronome joins an active source without opening or resetting the device");
    await metroAudio.SetMetronomeAsync(metro with{Enabled=false});Check(metroAudio.GetSnapshot().State==AudioState.Running,"disabling metronome preserves other sources");
    await metroAudio.StopAsync();await metroAudio.PlayReferenceAsync(440,.3);Check(metroAudio.GetSnapshot().Reserved==4 && metroAudio.GetAnalysis().ReferencePlaying && metroAudio.GetAnalysis().Frequency==0,"reference uses common graph and cannot pretend to be measured guitar");
    await metroAudio.StopAsync();Check(!metroAudio.GetAnalysis().ReferencePlaying,"global stop clears reference");
    await metroAudio.StartAsync();var beforeReference=metroAudio.GetClock();await metroAudio.PlayReferenceAsync(329.627,.2);Check(metroAudio.GetClock().Epoch==beforeReference.Epoch && metroAudio.GetSnapshot().Reserved==0,"reference joins an existing graph without a device restart");await metroAudio.StopAsync();
}
for(int i=0;i<20;++i)
{
    await using var referenceAudio=new NativeAudio();var playingReference=referenceAudio.PlayReferenceAsync(440,.3);var stoppingReference=referenceAudio.StopAsync();await Task.WhenAll(playingReference,stoppingReference);
    Check(referenceAudio.GetSnapshot().State==AudioState.Stopped && !referenceAudio.GetAnalysis().ReferencePlaying,"release/full stop cannot be resurrected by a late reference worker");
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

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void SetXruns(long value);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void ProcessSamples(uint frames,float sample);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void ProcessPcmSamples(uint frames,[In]float[] samples);
