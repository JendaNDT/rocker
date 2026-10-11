// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Infrastructure;
using Windows.Storage.Pickers;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private FrameworkElement BuildLegacyImport()
    {
        var panel=Stack();panel.Children.Add(Text("Převod z webové aplikace"));
        panel.Children.Add(Text("Ve stejném prohlížeči a profilu otevři původní Rocker. Export pro Windows zahrnuje nahrávky i značky; stará záloha JSON přenese pouze nastavení a pokrok. Webová data se nemažou.",true));
        var import=new Button{Content="Otevřít soubor pro převod…",IsEnabled=persistenceReady};var status=Text("Podporované soubory: .rockermigration a původní .json zálohy.",true);
        import.Click+=async (_,_)=>
        {
            if(database is null || catalog is null || backupOperationRunning)return;
            var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".rockermigration");picker.FileTypeFilter.Add(".json");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file=await picker.PickSingleFileAsync();if(file is null || closed)return;
            backupOperationRunning=true;import.IsEnabled=false;
            try
            {
                await CancelAiAsync();CancelPalm();await StopRhythmUiAsync();if(!await StopCaptureAsync())return;await autosave!.FlushAsync();var service=new LegacyImport(userFiles,database,catalog);
                await using var migration=await service.PrepareAsync(file.Path);
                var details=Stack();details.Children.Add(Text($"{migration.Preview.Documents} záznamů · {migration.Preview.Recordings} nahrávek · {migration.Preview.Conflicts} konfliktů"));
                var choice=new ComboBox{Header="Řešení konfliktů",ItemsSource=new[]{"Sloučit · existující položky zachovat","Nahradit zahrnutá data"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};details.Children.Add(choice);
                var key=new CheckBox{Content="Převzít starý AI klíč do chráněného úložiště Windows",IsChecked=false,Visibility=migration.HasSecret?Visibility.Visible:Visibility.Collapsed};details.Children.Add(key);
                if(migration.Warnings.Count>0)details.Children.Add(Text(string.Join("\n",migration.Warnings.Take(10)),true));
                var scroll=new ScrollViewer{Content=details,MaxHeight=420,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
                var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Převést webová data?",Content=scroll,PrimaryButtonText="Převést",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};
                if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;
                ShellInteractionRoot.IsEnabled=false;if(audio is not null)await audio.StopAsync();
                var result=await service.CommitAsync(migration,choice.SelectedIndex==0?RestoreStrategy.MergeKeepCurrent:RestoreStrategy.ReplaceIncluded,key.IsChecked==true);
                lessonSessions.Clear();completedLessons.Clear();savedLessonPositions.Clear();persistenceReady=false;await InitializePersistenceAsync();
                ShellNotice.IsOpen=true;ShellNotice.Title="Webová data převedena";ShellNotice.Severity=result.SecretFailed?InfoBarSeverity.Warning:InfoBarSeverity.Success;
                ShellNotice.Message=$"Převzato {result.Data.Imported}, zachováno {result.Data.KeptCurrent}. Původní webová data zůstávají."+(result.SecretFailed?" AI klíč se neuložil; zadej ho znovu v Nastavení.":result.SecretImported?" AI klíč chrání Windows.":"");
            }
            catch(Exception){status.Text="Převod se nezdařil. Zkontroluj formát, dostupnost médií a volné místo. Původní webový soubor se nepřepisuje.";}
            finally{backupOperationRunning=false;if(!closed){ShellInteractionRoot.IsEnabled=true;import.IsEnabled=persistenceReady;}}
        };
        panel.Children.Add(import);panel.Children.Add(status);return Card(panel);
    }
}
