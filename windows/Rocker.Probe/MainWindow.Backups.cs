// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Infrastructure;
using Windows.Storage.Pickers;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private bool backupOperationRunning;
    private FrameworkElement BuildBackups()
    {
        var panel=Stack();panel.Children.Add(Text("Zálohy a soubory"));
        panel.Children.Add(Text("Záloha obsahuje uložená data a podle volby také nahrávky a celé projekty. AI klíč, dočasné soubory a cache se nezahrnují.",true));
        var recordings=new CheckBox{Content="Zahrnout nahrávky",IsChecked=true};var projects=new CheckBox{Content="Zahrnout projekty a jejich stopy",IsChecked=true};
        var undo=new Button {Content="Vrátit poslední obnovu",IsEnabled=persistenceReady && database is not null && new BackupService(userFiles,database).LastRollback() is not null};
        var export=new Button{Content="Vytvořit zálohu…",IsEnabled=persistenceReady};var restore=new Button{Content="Otevřít zálohu a zobrazit náhled…",IsEnabled=persistenceReady};var status=Text("Vyber export nebo obnovu. Původní data se před obnovou odloží do vratné kopie.",true);
        export.Click+=async (_,_)=>
        {
            if(database is null || backupOperationRunning)return;
            var picker=new FileSavePicker {SuggestedStartLocation=PickerLocationId.DocumentsLibrary,SuggestedFileName="Rocker-zaloha-"+DateTime.Now.ToString("yyyy-MM-dd")};picker.FileTypeChoices.Add("Záloha Rockeru",[".rockerbackup"]);
            WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file=await picker.PickSaveFileAsync();if(file is null || closed)return;
            backupOperationRunning=true;export.IsEnabled=restore.IsEnabled=false;
            try
            {
                await autosave!.FlushAsync();
                await new BackupService(userFiles,database).ExportAsync(file.Path,recordings.IsChecked==true,projects.IsChecked==true);
                status.Text="Záloha je uložená: "+file.Path;
            }
            catch(Exception){status.Text="Zálohu se nepodařilo vytvořit. Zkontroluj volné místo, přístup k souboru a dostupnost všech médií.";}
            finally{backupOperationRunning=false;if(!closed)export.IsEnabled=restore.IsEnabled=true;}
        };
        restore.Click+=async (_,_)=>
        {
            if(database is null || backupOperationRunning)return;
            var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".rockerbackup");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file=await picker.PickSingleFileAsync();if(file is null || closed)return;
            backupOperationRunning=true;export.IsEnabled=restore.IsEnabled=false;
            try
            {
                await autosave!.FlushAsync();var service=new BackupService(userFiles,database);var preview=await service.PreviewAsync(file.Path);
                var choice=new ComboBox {Header="Řešení konfliktů",ItemsSource=new[]{"Sloučit · existující položky zachovat","Nahradit zahrnutá data ze zálohy"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};
                var details=Stack();details.Children.Add(Text($"{preview.Documents} záznamů · {preview.Recordings} nahrávek · {preview.Projects} projektů\n{preview.Bytes/1024d/1024:F1} MB · {preview.Conflicts} konfliktů"));
                details.Children.Add(Text("Nahradí se jen typy zahrnuté v této záloze. Vratná kopie zachová původní databázi a soubory. AI klíč zůstává ve Windows.",true));details.Children.Add(choice);
                var dialog=new ContentDialog {XamlRoot=ShellRoot.XamlRoot,Title="Obnovit zálohu?",Content=details,PrimaryButtonText="Obnovit",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};
                if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
                ShellRoot.IsEnabled=false;audio?.Stop();
                var result=await service.RestoreAsync(file.Path,preview,choice.SelectedIndex==0 ? RestoreStrategy.MergeKeepCurrent:RestoreStrategy.ReplaceIncluded);
                lessonSessions.Clear();completedLessons.Clear();savedLessonPositions.Clear();persistenceReady=false;
                await InitializePersistenceAsync();
                ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Success;ShellNotice.Title="Záloha obnovena";ShellNotice.Message=$"Převzato {result.Imported}, zachováno {result.KeptCurrent}. Vratná kopie: {result.RollbackDirectory}";
            }
            catch(Exception){status.Text="Záloha se neobnovila. Soubor může být poškozený, z novější verze nebo chybí místo. Původní data zůstávají zachovaná; případnou přerušenou obnovu Rocker vrátí při příštím spuštění.";}
            finally{backupOperationRunning=false;if(!closed){ShellRoot.IsEnabled=true;export.IsEnabled=restore.IsEnabled=true;}}
        };
        undo.Click+=async (_,_)=>
        {
            if(database is null || backupOperationRunning)return;
            var dialog=new ContentDialog {XamlRoot=ShellRoot.XamlRoot,Title="Vrátit poslední obnovu?",Content="Aktuální data se odloží a obnoví se stav před posledním importem zálohy.",PrimaryButtonText="Vrátit obnovu",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};
            if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
            backupOperationRunning=true;ShellRoot.IsEnabled=false;
            try
            {
                await autosave!.FlushAsync();audio?.Stop();await new BackupService(userFiles,database).UndoLastRestoreAsync();
                lessonSessions.Clear();completedLessons.Clear();savedLessonPositions.Clear();persistenceReady=false;await InitializePersistenceAsync();
                ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Success;ShellNotice.Title="Obnova vrácena";ShellNotice.Message="Předchozí stav dat je obnovený.";
            }
            catch(Exception){status.Text="Vratnou kopii se nepodařilo použít. Původní soubory kopie zůstávají zachované.";}
            finally{backupOperationRunning=false;if(!closed)ShellRoot.IsEnabled=true;}
        };
        panel.Children.Add(recordings);panel.Children.Add(projects);panel.Children.Add(export);panel.Children.Add(restore);panel.Children.Add(undo);panel.Children.Add(status);
        panel.Children.Add(Text("Převod ze staré webové aplikace bude dostupný po dokončení importu. Její místní data se tímto postupem nemažou.",true));return Card(panel);
    }
}
