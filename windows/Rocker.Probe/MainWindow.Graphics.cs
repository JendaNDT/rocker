// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private async Task CaptureVisualFinishAsync()
    {
        string? directory=Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");
        if(string.IsNullOrEmpty(directory) || audio is null)return;
        string previousTheme=selectedTheme;
        await audio.StopAsync();await audio.ClearMediaAsync();practiceSession.End();RefreshPracticeControls();
        ShellNotice.IsOpen=PracticeRestNotice.IsOpen=false;
        var captures=new List<object>();
        foreach(var (theme,view) in new[]{("dark","01"),("dark","02"),("dark","03"),("dark","04"),("dark","13"),("dark","17"),("dark","18"),("light","02"),("light","13")})
        {
            ApplyTheme(theme);Navigate(new(view));RefreshState();await Task.Delay(60);
            string file=$"screens/finish-{theme}-{view}.png";await CaptureNativeViewAsync(file);
            captures.Add(new{file,theme,view,clientWidthDip=ShellRoot.ActualWidth,clientHeightDip=ShellRoot.ActualHeight});
        }
        ApplyTheme(previousTheme);await LoadScoreAsync(Path.Combine(AppContext.BaseDirectory,"fixtures","notation-phrases.gp"));Navigate(new("01"));
        bool resources=File.Exists(Path.Combine(AppContext.BaseDirectory,"Assets","Rocker.ico")) && captures.Count==9;
        WriteShellDiagnostic("visual-finish.json",new{result=resources?"passed":"failed",captures,iconPublished=resources,liveInputAndMonitoringNotSimulated=true,manualVisualAcceptanceVerified=false,systemHighContrastManualVerified=false});
        if(!resources)throw new InvalidOperationException("Visual finish resources or native captures are missing.");
    }
}
