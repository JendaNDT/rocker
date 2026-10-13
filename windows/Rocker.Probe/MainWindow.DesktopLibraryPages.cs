// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private FrameworkElement BuildDesktopLibraryPage(NavigationTarget target)
    {
        var grid=new Grid{RowSpacing=8};grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var back=new Button{Content="Celá knihovna",MinHeight=30};back.Click+=(_,_)=>{desktopLibraryPage=null;RefreshDesktopLibrary();};grid.Children.Add(back);
        if(target.ViewId=="05")
        {
            var filter=new TextBox{PlaceholderText="Hledat lekci",MaxLength=128};Grid.SetRow(filter,1);grid.Children.Add(filter);
            var list=new ListView{DisplayMemberPath="Title",IsItemClickEnabled=true,SelectionMode=ListViewSelectionMode.Single};
            void Refresh()=>list.ItemsSource=catalog!.Lessons.Where(l=>PracticeWorkspaceRules.SearchText(l.Title).Contains(PracticeWorkspaceRules.SearchText(filter.Text),StringComparison.Ordinal)).Select(l=>new ListEntry(l.Id,(completedLessons.Contains(l.Id)?"Hotovo · ":"")+l.Title,$"{l.EstimatedMinutes} minut")).ToArray();
            filter.TextChanged+=(_,_)=>Refresh();list.ItemClick+=(_,e)=>{if(e.ClickedItem is ListEntry entry)Navigate(new("06",entry.Id));};Grid.SetRow(list,2);grid.Children.Add(list);Refresh();return grid;
        }
        recordingList=new ListView{DisplayMemberPath="LibraryLabel",IsItemClickEnabled=true,SelectionMode=ListViewSelectionMode.Multiple};
        recordingList.ItemClick+=async(_,e)=>{if(e.ClickedItem is RecordingData value){try{await OpenDesktopRecordingAsync(value);}catch(Exception error){ShowPersistenceError(error.Message);}}};
        var menu=new MenuFlyout();
        void Command(string title,Func<RecordingData,Task> action){var item=new MenuFlyoutItem{Text=title};item.Click+=async(_,_)=>{if(recordingList.SelectedItem is not RecordingData value)return;try{await action(value);}catch(Exception error){ShowPersistenceError(error.Message);}};menu.Items.Add(item);}
        Command("Otevřít průběh",async r=>{await OpenDesktopRecordingAsync(r);});Command("Přehrát",PlayRecordingAsync);Command("Značky a poznámky…",EditRecordingMarkersAsync);Command("Exportovat…",ExportRecordingAsync);
        Command("Přejmenovat…",async r=>{var name=await AskRecordingNameAsync("Název nahrávky",r.Name);if(name is not null){await recordingLibrary!.SaveAsync(r with{Name=name});await ReloadRecordingsAsync();}});
        Command("Odstranit…",async r=>{var confirmation=new ContentDialog{XamlRoot=DialogRoot,Title="Odstranit nahrávku?",Content=r.Name,PrimaryButtonText="Odstranit",CloseButtonText="Zrušit"};if(await ShowDialogAsync(confirmation)==ContentDialogResult.Primary){await recordingLibrary!.RemoveManyAsync([r]);await ReloadRecordingsAsync();}});
        async Task Compare(int index)
        {
            if(recordingList.SelectedItems.Count!=2)throw new InvalidOperationException("Vyber právě dvě nahrávky.");var pair=recordingList.SelectedItems.Cast<RecordingData>().ToArray();double position=0;
            using(var scope=documentContext.Enter(services.Transport.Owner??documentContext.Default))position=(audio?.GetTransport().PositionSeconds??0)*currentSpeed;
            await PlayRecordingAsync(pair[index]);using var ownerScope=documentContext.Enter(services.Transport.Owner??documentContext.Default);if(currentSpeed!=1)await ChangeSongSpeedAsync(1);if(audio is not null)await audio.SeekAsync(Math.Min(position,pair[index].DurationSec));
        }
        Command("Porovnat A · první vybraná",_=>Compare(0));Command("Porovnat B · druhá vybraná",_=>Compare(1));
        Command("Odstranit vybrané nahrávky…",async _=>{var values=recordingList.SelectedItems.Cast<RecordingData>().ToArray();var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Odstranit vybrané nahrávky?",Content=$"Počet: {values.Length}. Poslední odstranění lze vrátit.",PrimaryButtonText="Odstranit",CloseButtonText="Zrušit"};if(await ShowDialogAsync(dialog)==ContentDialogResult.Primary){await recordingLibrary!.RemoveManyAsync(values);await ReloadRecordingsAsync();}});
        Command("Vrátit poslední odstranění",async _=>{await recordingLibrary!.UndoAsync();await ReloadRecordingsAsync();});
        recordingList.ContextFlyout=menu;
        var actions=new Button{Content="Akce vybrané nahrávky",Flyout=menu,MinHeight=30};Grid.SetRow(actions,1);grid.Children.Add(actions);Grid.SetRow(recordingList,2);grid.Children.Add(recordingList);_=ReloadRecordingsAsync();return grid;
    }
}
