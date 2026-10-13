// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Rocker.AppCore;
using Rocker.Domain;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private DocumentSession? studioCaptureSession;
    private async Task StopDesktopSessionsAsync()
    {
        if(!desktopEnabled)return;
        foreach(var operation in services.Operations.Snapshot().Where(o=>o.CanCancel))services.Operations.Cancel(operation.Id);
        foreach(var session in services.Documents.All)
        {
            using var scope=documentContext.Enter(session);
            CancelPalm();surgicalListening=false;await StopRhythmUiAsync();await CancelAiAsync();
            if(session.Target.ViewId=="08")SendVideo(new{op="pause"});
        }
        services.Transport.SetGrader(null);
    }
    private void DisposeDesktopViews()
    {
        if(!desktopEnabled)return;
        services.Operations.Changed-=DesktopOperationsChanged;
        foreach(var session in services.Documents.All)
        {
            using var scope=documentContext.Enter(session);
            if(session.Target.ViewId=="08")VideoView.Close();
            session.Close();
        }
        foreach(var window in desktopWindows.ToArray()){dialogService?.Forget(window.Root.XamlRoot);window.ExitApproved=true;window.Close();}
        desktopWindows.Clear();floatingHosts.Clear();desktopDocuments.Clear();desktopTools.Clear();
    }
    private bool DesktopHasCriticalOperation()=>services.Operations.Snapshot().Any(o=>!o.CanCancel && o.Kind is not "dialog");
}
