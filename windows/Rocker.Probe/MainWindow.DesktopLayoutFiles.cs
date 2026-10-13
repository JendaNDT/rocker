// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml.Controls;
using Rocker.Infrastructure;
using Windows.Storage.Pickers;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private async Task ExportDesktopLayoutAsync()
    {
        using var scope=documentContext.Enter(documentContext.Current);
        try
        {
            var picker=new FileSavePicker{SuggestedFileName="Rocker-pracovni-plocha"};picker.FileTypeChoices.Add("Rozložení Rockeru",[".rockerlayout"]);WinRT.Interop.InitializeWithWindow.Initialize(picker,DialogWindowHandle);
            using var operation=services.Operations.Begin(documentContext.Current.Id,"picker","Uložení rozložení",cancellable:false);
            var file=await picker.PickSaveFileAsync();if(file is null)return;await SaveDesktopLayoutAsync();await Windows.Storage.FileIO.WriteBytesAsync(file,await File.ReadAllBytesAsync(new WorkspaceLayoutStore(userFiles).FilePath));
        }
        catch(Exception error){ShowPersistenceError("Rozložení nelze uložit: "+error.Message);}
    }
    private async Task ImportDesktopLayoutAsync()
    {
        using var scope=documentContext.Enter(documentContext.Current);
        bool interactionDisabled=false;
        if(captureDraft is not null || workspaceOperation || backupOperationRunning || DesktopHasCriticalOperation()){ShowPersistenceError("Nejdřív dokonči nahrávání nebo probíhající operaci.");return;}
        try
        {
            var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".rockerlayout");WinRT.Interop.InitializeWithWindow.Initialize(picker,DialogWindowHandle);
            Windows.Storage.StorageFile? file;
            using(var operation=services.Operations.Begin(documentContext.Current.Id,"picker","Načtení rozložení",cancellable:false))file=await picker.PickSingleFileAsync();
            if(file is null)return;var layout=WorkspaceLayoutStore.LoadFile(file.Path)??throw new InvalidDataException("Rozložení je prázdné.");
            var confirmation=new ContentDialog{XamlRoot=DialogRoot,Title="Načíst pracovní plochu?",Content="Nahradí otevření dokumentů a polohy oken. Hudební soubory zůstanou zachované. Zvuk se sám nespustí.",PrimaryButtonText="Načíst",CloseButtonText="Zrušit"};
            if(await ShowDialogAsync(confirmation)!=ContentDialogResult.Primary)return;
            if(captureDraft is not null || DesktopHasCriticalOperation())throw new InvalidOperationException("Nejdřív dokonči nahrávání nebo probíhající operaci.");
            SetDesktopInteraction(false);interactionDisabled=true;
            using var layoutOperation=services.Operations.Begin("application","layout-import","Obnova pracovní plochy",cancellable:false);
            if(!await services.Exit.ResolveAsync(async guard=>
            {
                var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Rozpracovaná práce",Content=guard.State.Prompt,PrimaryButtonText="Uložit",SecondaryButtonText=guard.State.Recording?"":"Zahodit",CloseButtonText="Zrušit"};
                return await ShowDialogAsync(dialog) switch{ContentDialogResult.Primary=>Rocker.Domain.CloseChoice.Save,ContentDialogResult.Secondary=>Rocker.Domain.CloseChoice.Discard,_=>Rocker.Domain.CloseChoice.Cancel};
            }))return;
            await StopDesktopAudioAsync();
            foreach(var session in services.Documents.All){var loading=session.Get<Task?>("song-loading",()=>null);if(loading is not null)await loading;var recording=session.Get<Task<bool>?>("recording-loading",()=>null);if(recording is not null)await recording;}
            foreach(var tab in desktopDocuments.Values.ToArray())await CloseDesktopTabAsync(tab);
            foreach(var pair in desktopTools.Values.Where(p=>Rocker.Domain.DesktopRoutes.ToolViews.Contains(p.Session.Target.ViewId))){if(floatingHosts.TryGetValue(pair.Tab,out var host))host.AppWindow.Hide();else toolTabs.TabItems.Remove(pair.Tab);}
            await new WorkspaceLayoutStore(userFiles).SaveAsync(layout);await RestoreDesktopLayoutAsync();
        }
        catch(Exception error){ShowPersistenceError("Rozložení nelze načíst: "+error.Message);}
        finally{if(interactionDisabled)SetDesktopInteraction(true);}
    }
}
