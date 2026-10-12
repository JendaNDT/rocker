// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Infrastructure;
using Windows.Storage.Pickers;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private void ShowColdRecovery()
    {
        persistenceReady=false;Navigation.IsEnabled=false;MediaPanel.Visibility=Visibility.Collapsed;
        NotationPanel.Visibility=VideoPanel.Visibility=DevicePanel.Visibility=Visibility.Collapsed;WorkspaceContent.Visibility=Visibility.Visible;
        WorkspaceTitle.Text="Obnova uložených dat";WorkspaceTrail.Text="Rocker / Místní profil";
        var panel=Stack();panel.Children.Add(Text("Profil může být poškozený nebo pocházet z novější verze. Původní soubory se nepřepisují. Obnova nejprve prověří zálohu a celý současný profil zachová zvlášť.",true));
        var open=new Button{Content="Otevřít složku původních dat"};open.Click+=async(_,_)=>{try{await Windows.System.Launcher.LaunchFolderAsync(await Windows.Storage.StorageFolder.GetFolderFromPathAsync(userFiles.Root));}catch(Exception){ShowPersistenceError("Složku nelze otevřít. Umístění: "+userFiles.Root);}};panel.Children.Add(open);
        var restore=new Button{Content="Obnovit kompatibilní zálohu…"};restore.Click+=async(_,_)=>
        {
            var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".rockerbackup");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSingleFileAsync();if(file is null)return;
            var confirm=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Obnovit profil a zavřít Rocker?",Content="Vybraná záloha se musí dát načíst touto verzí. Současný profil včetně nahrávek a chráněných klíčů zůstane zachovaný v samostatné složce. Po obnově spusť Rocker znovu.",PrimaryButtonText="Obnovit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await confirm.ShowAsync()!=ContentDialogResult.Primary)return;
            restore.IsEnabled=false;backupOperationRunning=true;
            try{var result=await ColdProfileRecovery.RestoreAsync(userFiles,file.Path);if(audio is not null){await audio.DisposeAsync();audio=null;}var finished=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Data jsou obnovená",Content="Spusť Rocker znovu. Původní profil je zachovaný zde: "+result.RetainedDirectory,CloseButtonText="Zavřít Rocker"};await finished.ShowAsync();closeApproved=true;Close();}
            catch(Exception){ShowPersistenceError("Záloha nebyla přijata nebo obnovu přerušila chyba. Původní profil zůstává zachovaný; při příštím spuštění se dokončí případná připravená obnova.");}
            finally{backupOperationRunning=false;if(!closed)restore.IsEnabled=true;}
        };panel.Children.Add(restore);WorkspaceContent.Content=Card(panel);LabelNativeControls(WorkspaceContent);
    }
}
