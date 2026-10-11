// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private FrameworkElement BuildProgress()
    {
        FlushPractice();var panel=Stack();var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,practiceTimeZone).DateTime);panel.Children.Add(Text("Pokrok ve cvičení"));panel.Children.Add(Text($"Dnes {progressData.DailyMinutes.GetValueOrDefault(today.ToString("yyyy-MM-dd")):0.##} min · celkem {Practice.TotalMinutes(progressData.DailyMinutes):0.##} min · cvičených dní {ProgressRules.TotalPracticeDays(progressData.DailyMinutes)}"));
        panel.Children.Add(Text($"Aktuální série {Practice.CurrentStreak(progressData.DailyMinutes,today)} dní · nejdelší {ProgressRules.LongestStreak(progressData.DailyMinutes)} dní · lekce {completedLessons.Count}/{catalog!.Lessons.Count}",true));panel.Children.Add(Text("Minuty pocházejí z výslovně zahájených session a skutečné aktivity. Historická webová data měřila pobyt na viditelné obrazovce; zachováváme je, ale neoznačujeme za měřenou hru.",true));
        var history=new ListView{Header="Historie dní",ItemsSource=progressData.DailyMinutes.OrderByDescending(p=>p.Key,StringComparer.Ordinal).Select(p=>$"{p.Key} · {p.Value:0.##} minut").ToArray(),MaxHeight=320};panel.Children.Add(history);var records=new ListView{Header="Nejvyšší dokončená tempa",ItemsSource=progressData.BpmRecords.OrderByDescending(p=>p.Value).Select(p=>$"{ExerciseTitle(p.Key)} · {p.Value:0.##} BPM").ToArray(),MaxHeight=240};panel.Children.Add(records);panel.Children.Add(Text("Tempo rekord není údaj o přesnosti: trenažéry doprovodu nehodnotí každý úhoz. Přesný tónový drill a palm mute mají vlastní výsledky.",true));
        if(!appSettings.ZenMode)panel.Children.Add(new ListView{Header="Odměny",ItemsSource=catalog.Items("achievements").Select(a=>$"{(progressData.UnlockedAchievements.Contains(a.GetProperty("id").GetString()!)?"Odemčeno":"Zatím zamčeno")} · {a.GetProperty("title").GetString()} · {a.GetProperty("description").GetString()}").ToArray(),MaxHeight=320});
        var refresh=new Button{Content="Obnovit přehled"};refresh.Click+=(_,_)=>{workspaceCache.Remove(new("27"));ShowWorkspace();};panel.Children.Add(refresh);var reset=new Button{Content="Vynulovat minuty, tempo rekordy a odměny…"};reset.Click+=async(_,_)=>{var dialog=new ContentDialog{XamlRoot=ShellRoot.XamlRoot,Title="Vynulovat pokrok?",Content="Smažou se minuty, série, tempo rekordy a odměny. Dokončené lekce a nahrávky zůstanou. Před smazáním můžeš vytvořit zálohu.",PrimaryButtonText="Vynulovat",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await dialog.ShowAsync()!=ContentDialogResult.Primary)return;practiceSession.End();_ =practiceSession.Drain();progressData=ProgressRules.Empty;if(persistenceReady)autosave!.Queue(UserData.Document("progress","default",progressData));workspaceCache.Remove(new("27"));workspaceCache.Remove(new("01"));ShowWorkspace();RefreshPracticeControls();};panel.Children.Add(reset);return Card(panel);
    }
    private string ExerciseTitle(string id)
    {
        if(id.StartsWith("riff:",StringComparison.Ordinal))return catalog!.ResolveRiff(id[5..])?.GetProperty("title").GetString()??id;foreach(string group in new[]{"powerChordExercises","gallopExercises","palmMuteDrills","surgicalExercises"})if(catalog!.Find(group,id) is { } item)return item.TryGetProperty("title",out var title)?title.GetString()??id:item.TryGetProperty("name",out var name)?name.GetString()??id:id;return id;
    }
}
