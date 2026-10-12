// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private sealed record PlanTask(string Id,string Title,string Detail,int Minutes,NavigationTarget Target);
    private NavigationTarget? appliedNavigationTask;
    private void ApplyNavigationTask(NavigationTarget target)
    {
        if(!persistenceReady || catalog is null || target==appliedNavigationTask)return;appliedNavigationTask=target;if(target.ItemId is not { } id)return;
        if(target.ViewId=="03" && AllTunings().FirstOrDefault(t=>t.Id==id) is { } tuning){appSettings=appSettings with{TuningId=tuning.Id};SaveSettings();RefreshTuningSelector();RebuildTunerStrings();}
        if(target.ViewId=="04" && AllMetroPresets().FirstOrDefault(p=>p.Id==id) is { } preset){metroConfig=preset.Config;metroPresetId=preset.Id;QueueMetronome();RebuildMetroBeats();}
    }
    private string planKind="daily";private (string Key,PlanTask Task)? openedPlanTask;
    private string CurrentPlanKey=>planKind=="ai" && aiSettings.Plan is { } ai?"ai:"+(ai.AcceptedAt?.UtcTicks.ToString()??"legacy"):"day:"+DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,practiceTimeZone).DateTime).ToString("yyyy-MM-dd");
    private PlanProgress? SelectedPlanProgress=>planKind=="ai"?appSettings.AiPlanProgress:appSettings.PracticePlan;
    private bool PlanDone(string key,string id)=>SelectedPlanProgress is { } p && p.Key==key && p.Completed.Contains(id,StringComparer.Ordinal);
    private IReadOnlyList<PlanTask> CurrentPlanTasks()
    {
        if(planKind=="ai" && aiSettings.Plan is { } ai)return ai.Items.Select((item,index)=>new PlanTask(index.ToString(),item.Title,item.Detail,item.Minutes,new(item.ViewId,item.ItemId))).ToArray();
        var date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,practiceTimeZone).DateTime);return Practice.GenerateDailyPlan(catalog!,completedLessons,date).Items.Select(item=>new PlanTask(item.Id,item.Title,item.Detail,item.Minutes,DailyTarget(item))).ToArray();
    }
    private NavigationTarget DailyTarget(PlanItem item)=>item.LessonId is not null?new("06",item.LessonId):item.TrainerId=="surgical"?new("09",catalog!.Items("surgicalExercises").First().GetProperty("id").GetString()):item.TrainerId=="riff"?new("19",catalog!.Items("riffs").First().GetProperty("id").GetString()):new(item.Href switch{"/tuner"=>"03","/metronome"=>"04","/youtube"=>"08",_=>"05"});
    private FrameworkElement BuildOverview()
    {
        var panel=Stack();panel.Children.Add(Text("Pojďme si zahrát."));var summary=Stack();summary.Children.Add(Text("Kytara: "+instrumentProfile.Name));summary.Children.Add(Text($"{CurrentTuning().Label} · dokončené lekce {completedLessons.Count}/{catalog!.Lessons.Count}",true));var live=new Button{Content="Otevřít Live · ladění, metronom a nahrávání"};live.Click+=(_,_)=>Navigate(new("02"));summary.Children.Add(live);
        if(appSettings.ResumeTask is { } resume){var next=new Button{Content="Pokračovat · "+TaskTitle(resume)};next.Click+=(_,_)=>Navigate(resume);summary.Children.Add(next);}panel.Children.Add(Card(summary));
        var select=new ComboBox{Header="Cvičební plán",ItemsSource=new[]{"Denní plán bez AI","Uložený AI plán"},SelectedIndex=planKind=="ai"?1:0};select.SelectionChanged+=(_,_)=>{planKind=select.SelectedIndex==1?"ai":"daily";workspaceCache.Remove(new("01"));ShowWorkspace();};panel.Children.Add(select);
        if(planKind=="ai" && aiSettings.Plan is null){panel.Children.Add(Text("Zatím nemáš uložený AI plán. Denní plán funguje bez účtu i internetu.",true));var create=new Button{Content="Navrhnout AI plán"};create.Click+=(_,_)=>Navigate(new("24"));panel.Children.Add(create);return panel;}
        string key=CurrentPlanKey;var tasks=CurrentPlanTasks();panel.Children.Add(Text($"{tasks.Sum(t=>t.Minutes)} minut · hotovo {tasks.Count(t=>PlanDone(key,t.Id))}/{tasks.Count}",true));
        foreach(var task in tasks){var row=Stack();row.Children.Add(Text($"{(PlanDone(key,task.Id)?"Hotovo · ":"")}{task.Title} · {task.Minutes} min"));row.Children.Add(Text(task.Detail,true));row.Children.Add(Text("Otevře: "+TaskTitle(task.Target),true));var open=new Button{Content="Otevřít tento blok"};open.Click+=(_,_)=>OpenPlanTask(key,task);row.Children.Add(open);panel.Children.Add(Card(row));}
        var remaining=tasks.FirstOrDefault(t=>!PlanDone(key,t.Id));if(remaining is not null){var next=new Button{Content="Další krok · "+remaining.Title};next.Click+=(_,_)=>OpenPlanTask(key,remaining);panel.Children.Add(next);}else{panel.Children.Add(Text("Plán je dokončený. Ulož si hru v Live, nebo se vrať k některému bloku.",true));}
        panel.Children.Add(Text("Hotový blok potvrzuješ sám. Označení neznamená automaticky změřenou přesnost ani nepřičítá minuty.",true));return panel;
    }
    private string TaskTitle(NavigationTarget target)
    {
        if(target.ViewId=="06" && target.ItemId is { } lesson)return catalog!.Lessons.FirstOrDefault(l=>l.Id==lesson)?.Title??WorkspaceViews.Get(target.ViewId).Title;
        if(target.ItemId is { } id){foreach(string group in new[]{"riffs","surgicalExercises","powerChordExercises","gallopExercises","palmMuteDrills","chords","scales"})if(catalog!.Find(group,id) is { } item)return item.TryGetProperty("title",out var title)?title.GetString()??id:item.TryGetProperty("name",out var name)?name.GetString()??id:id;}
        return WorkspaceViews.Get(target.ViewId).Title;
    }
    private void OpenPlanTask(string key,PlanTask task){openedPlanTask=(key,task);appliedNavigationTask=null;workspaceCache.Remove(task.Target);Navigate(task.Target);}
    private void RefreshPlanControls(){PlanButtons.Visibility=openedPlanTask is not null && navigation.Current.ViewId!="01"?Visibility.Visible:Visibility.Collapsed;PlanTaskLabel.Text=openedPlanTask is { } p?"Plán · "+p.Task.Title:"";CompletePlan.IsEnabled=openedPlanTask is { } o && o.Key==CurrentPlanKey && navigation.Current==o.Task.Target && !PlanDone(o.Key,o.Task.Id);}
    private void ReturnPlan_Click(object sender,RoutedEventArgs args)=>Navigate(new("01"));
    private void CompletePlan_Click(object sender,RoutedEventArgs args)
    {
        if(openedPlanTask is not { } opened || !CompletePlan.IsEnabled)return;var previous=SelectedPlanProgress;var completed=previous?.Key==opened.Key?previous.Completed:Array.Empty<string>();var state=new PlanProgress(opened.Key,completed.Append(opened.Task.Id).Distinct(StringComparer.Ordinal).ToArray());appSettings=planKind=="ai"?appSettings with{AiPlanProgress=state}:appSettings with{PracticePlan=state};SaveSettings();workspaceCache.Remove(new("01"));RefreshPlanControls();
    }
    private async Task VerifyOverviewLiveAsync()
    {
        var previous=appSettings;planKind="daily";Navigate(new("01"));var tasks=CurrentPlanTasks();bool concrete=tasks.All(t=>!(t.Target.ViewId is "19" or "09") || t.Target.ItemId is not null);var task=tasks[0];string key=CurrentPlanKey;OpenPlanTask(key,task);bool reached=navigation.Current==task.Target && CompletePlan.IsEnabled;CompletePlan_Click(this,new());CompletePlan_Click(this,new());await autosave!.FlushAsync();var snapshot=await (await DataRepository.OpenAsync(userFiles)).SnapshotAsync();var saved=UserData.Read<AppSettings>(snapshot.Documents.Single(d=>d.Kind=="settings").Value);bool once=saved.PracticePlan is { } state && state.Key==key && state.Completed.Count(i=>i==task.Id)==1 && saved.ResumeTask==task.Target;
        Navigate(new("02"));bool live=liveTunerNote is not null && liveTunerNeedle is not null && recordingStarts.Count>0 && audioHostsCreated==1;appSettings=previous;SaveSettings();await autosave.FlushAsync();openedPlanTask=null;Navigate(new("01"));bool pass=concrete && reached && once && live;WriteShellDiagnostic("overview-live.json",new{result=pass?"passed":"failed",exactDailyTargets=concrete,planRouteAndReturn=reached,completionSavedOnce=once,liveTunerAndSharedRecorder=live,hardwareVerified=false});if(!pass)throw new InvalidOperationException("Overview/Live integration failed.");
    }
}
