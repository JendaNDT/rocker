// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.AppCore;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private void SetDesktopInteraction(bool enabled)
    {
        if(!desktopEnabled)return;
        ShellInteractionRoot.IsEnabled=enabled;
        foreach(var window in desktopWindows){window.Tabs.IsEnabled=window.Menu.IsEnabled=enabled;}
    }
    private async Task FlushDesktopWorkAsync()
    {
        if(!desktopEnabled)return;
        await StopDesktopSessionsAsync();
        foreach(var session in services.Documents.All)
        {
            using var scope=documentContext.Enter(session);
            if(studioEditTask is{ } task)await task;
            foreach(var guard in closeGuards.Where(p=>p.Key.StartsWith(session.Id+"/",StringComparison.Ordinal)).ToArray())
                if(guard.Value.State.RequiresConfirmation && !await guard.Value.Save())throw new IOException("Rozpracovaný projekt nelze uložit. Obnova nebyla zahájena.");
        }
        if(closeGuards.TryGetValue("looper",out var looper) && looper.State.RequiresConfirmation && !await looper.Save())throw new IOException("Looper nelze uložit. Obnova nebyla zahájena.");
    }
    private async Task ResetDesktopDocumentsAfterRestoreAsync()
    {
        if(!desktopEnabled)return;
        if(audio is not null){await audio.ClearMediaAsync();await audio.LoopCommandAsync(7);}
        foreach(var session in services.Documents.All)
        {
            using var scope=documentContext.Enter(session);
            if(session.Target.ViewId=="08")VideoView.Close();
            if(desktopDocuments.Remove(session,out var tab)){RemoveDesktopTabParent(tab);floatingHosts.Remove(tab);}
            foreach(var key in closeGuards.Keys.Where(k=>k.StartsWith(session.Id+"/",StringComparison.Ordinal)).ToArray())closeGuards.Remove(key);
            services.Transport.Release(session);services.Documents.Close(session);
        }
        foreach(var pair in desktopTools.Where(p=>p.Key!="settings").ToArray())
        {
            RemoveDesktopTabParent(pair.Value.Tab);floatingHosts.Remove(pair.Value.Tab);desktopTools.Remove(pair.Key);
        }
        foreach(var window in desktopWindows.Where(w=>!floatingHosts.ContainsValue(w)).ToArray())
        {dialogService?.Forget(window.Root.XamlRoot);window.ExitApproved=true;window.Close();desktopWindows.Remove(window);}
        documentContext.Select(documentContext.Default);
    }
    private void ReopenDesktopAfterRestore()
    {
        if(!desktopEnabled)return;
        OpenDesktop(new("01"));RefreshDesktopLibrary();
    }
}
