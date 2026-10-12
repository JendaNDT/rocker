// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    [DllImport("user32.dll")]private static extern nint SendMessageW(nint window,uint message,nuint wParam,nint lParam);
    private async Task VerifyPracticeAndProfileAsync()
    {
        var oldProfile=instrumentProfile;var oldSettings=appSettings;var oldProgress=progressData;
        Navigate(new("26"));bool form=profileName is not null && profileTuning is not null && pickupEditors.Count==oldProfile.Pickups.Count;
        profileName!.Text="Testovací kytara · žluťoučká";profileHand!.SelectedIndex=1;profileTuning!.SelectedValue="standard";await SaveInstrumentAsync();bool linked=appSettings.Handedness=="left" && CurrentTuning().Id=="standard" && instrumentProfile.Name.Contains("žluťoučká",StringComparison.Ordinal);
        await autosave!.FlushAsync();var snapshot=await (await DataRepository.OpenAsync(userFiles)).SnapshotAsync();var restored=UserData.Read<InstrumentProfile>(snapshot.Documents.Single(d=>d.Kind=="instrument").Value);bool stored=restored.Name==instrumentProfile.Name && restored.Pickups.SequenceEqual(instrumentProfile.Pickups);
        int before=timeZoneNotifications;SendMessageW(nativeWindow,0x001e,0,0);bool zone=timeZoneNotifications==before+1;
        practiceSession.End();_ =practiceSession.Drain();var instant=DateTimeOffset.UtcNow;practiceSession.Start(0,instant);ObservePractice(1000,instant.AddSeconds(1),true);ObservePractice(2000,instant.AddSeconds(2),true);FlushPractice();double charged=practiceSession.TotalMs;FlushPractice();bool once=charged==1000 && practiceSession.Drain().Count==0;
        practiceSession.Pause();ObservePractice(3000,instant.AddSeconds(3),true);bool paused=practiceSession.TotalMs==charged;practiceSession.End();Navigate(new("27"));bool progress=WorkspaceContent.Content is not null && PracticeStart.IsEnabled && !PracticeEnd.IsEnabled;
        instrumentProfile=oldProfile;appSettings=oldSettings;progressData=oldProgress;autosave.Queue(UserData.Document("instrument","default",oldProfile));SaveSettings();autosave.Queue(UserData.Document("progress","default",oldProgress));await autosave.FlushAsync();workspaceCache.Clear();RefreshTuningSelector();RebuildTunerStrings();Navigate(new("01"));RefreshPracticeControls();bool pass=form && linked && stored && zone && once && paused && progress;
        WriteShellDiagnostic("practice-profile.json",new{result=pass?"passed":"failed",nativeProfile=form,linkedHandAndTuning=linked,profilePersistence=stored,timeZoneNotification=zone,activeMilliseconds=charged,drainedOnce=once,pauseExcluded=paused,nativeProgress=progress,hardwareVerified=false});if(!pass)throw new InvalidOperationException("Native practice/profile check failed.");
    }
}
