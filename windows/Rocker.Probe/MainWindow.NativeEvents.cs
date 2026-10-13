// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private delegate nint WindowSubclass(nint window,uint message,nuint wParam,nint lParam,nuint id,nuint data);
    [DllImport("comctl32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowSubclass(nint window,WindowSubclass callback,nuint id,nuint data);
    [DllImport("comctl32.dll")] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool RemoveWindowSubclass(nint window,WindowSubclass callback,nuint id);
    [DllImport("comctl32.dll")]private static extern nint DefSubclassProc(nint window,uint message,nuint wParam,nint lParam);
    [DllImport("kernel32.dll")]private static extern uint SetThreadExecutionState(uint flags);
    private bool wakeLockApplied;
    private void UpdateWakeLock(){bool wanted=appSettings.KeepAwake && practiceSession.Phase==Rocker.Domain.PracticePhase.Running && !closed && !closingWindow;if(wanted==wakeLockApplied)return;uint result=SetThreadExecutionState(wanted?0x80000003u:0x80000000u);if(result!=0)wakeLockApplied=wanted;}
    private WindowSubclass? nativeWindowCallback;private nint nativeWindow;private int timeZoneNotifications;
    private TimeZoneInfo practiceTimeZone=TimeZoneInfo.Local;
    private Task powerStopTask=Task.CompletedTask;
    private bool powerTransition;
    private async Task StopForPowerTransitionAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        powerTransition=true;
        try
        {
            practiceSession.Pause();FlushPractice();RefreshPracticeControls();
            SendVideo(new{op="pause"});CancelPalm();++playbackIntent;
            songLoadCancelled=true;fileCancellation?.Cancel();exportCancellation?.Cancel();
            if(audio is not null){audio.SetMonitoring(false,liveGain);audio.CancelMediaPreparation();}
            if(liveMonitor is not null){liveUpdating=true;liveMonitor.IsChecked=false;liveUpdating=false;}
            metroEnabled=false;++metroRevision;metroApplyTimer.Stop();
            await StopDesktopSessionsAsync();await CancelAiAsync();await StopRhythmUiAsync();
            if(audio is not null){await audio.StopAsync();await StopCaptureAsync();await ApplyMetronomeAsync();}
            if(autosave is not null)await autosave.FlushAsync();
            if(!closed){RefreshState();ShellNotice.IsOpen=true;ShellNotice.Message="Po uspání je zvuk zastavený a monitoring vypnutý. Spusť jej znovu až podle potřeby.";}
        }
        catch(Exception error){if(!closed)ShowPersistenceError("Zvuk byl přerušen. Vyber zařízení znovu; dostupná část nahrávky zůstává zachovaná. "+error.Message);}
        finally{powerTransition=false;}
    }
    private void InitializeNativeEvents()
    {
        nativeWindow=WinRT.Interop.WindowNative.GetWindowHandle(this);nativeWindowCallback=OnNativeWindowMessage;if(!SetWindowSubclass(nativeWindow,nativeWindowCallback,0x524b,0))throw new InvalidOperationException("Nativní události okna nelze připojit.");
    }
    private nint OnNativeWindowMessage(nint window,uint message,nuint wParam,nint lParam,nuint id,nuint data)
    {
        try
        {
            if(message is 0x001e or 0x001a){TimeZoneInfo.ClearCachedData();practiceTimeZone=TimeZoneInfo.Local;++timeZoneNotifications;}
            // Both suspend and resume leave the explicit session paused. Never
            // count an unseen interval or automatically resume the user's audio.
            if(message==0x0218 && wParam is 4 or 7 or 0x12 && powerStopTask.IsCompleted)powerStopTask=StopForPowerTransitionAsync();
            if(message==0x0082)DetachNativeEvents();
        }
        catch(Exception){/* Never unwind a managed exception through a window procedure. */}
        return DefSubclassProc(window,message,wParam,lParam);
    }
    private void DetachNativeEvents(){SetThreadExecutionState(0x80000000u);wakeLockApplied=false;if(nativeWindowCallback is { } callback){RemoveWindowSubclass(nativeWindow,callback,0x524b);nativeWindowCallback=null;}}
    private async Task VerifyPowerRecoveryAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(audio is null)throw new InvalidOperationException("Power recovery requires the native host.");
        practiceSession.Start(0,DateTimeOffset.UtcNow);
        SendMessageW(nativeWindow,0x0218,4,0);await powerStopTask;
        bool suspended=audio.GetSnapshot().State==Rocker.Interop.AudioState.Stopped && !audio.GetMeters().Monitoring && practiceSession.Phase!=Rocker.Domain.PracticePhase.Running && !wakeLockApplied;
        SendMessageW(nativeWindow,0x0218,0x12,0);await powerStopTask;
        bool resumed=audio.GetSnapshot().State==Rocker.Interop.AudioState.Stopped && !audio.GetMeters().Monitoring;
        practiceSession.End();RefreshPracticeControls();
        WriteShellDiagnostic("power-recovery.json",new{result=suspended && resumed?"passed":"failed",syntheticWindowMessages=true,suspendStoppedAudio=suspended,resumeRemainedSilent=resumed,physicalSuspendVerified=false});
        if(!suspended || !resumed)throw new InvalidOperationException("Power transition resumed audio unexpectedly.");
    }
}
