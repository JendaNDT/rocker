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
        if(desktopEnabled)return BuildDesktopOverview();
        var panel=Stack();
        string key=CurrentPlanKey;
        bool missingAi=planKind=="ai" && aiSettings.Plan is null;
        var tasks=missingAi?Array.Empty<PlanTask>():CurrentPlanTasks();
        int done=tasks.Count(t=>PlanDone(key,t.Id));
        var remaining=tasks.FirstOrDefault(t=>!PlanDone(key,t.Id));
        var welcome=Stack();var kicker=Text("CVIČ · HRAJ · TVOŘ");kicker.Style=(Style)Application.Current.Resources["VintageSectionTextStyle"];welcome.Children.Add(kicker);
        var title=Text("Pojďme si zahrát.");title.FontSize=32;title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;welcome.Children.Add(title);
        welcome.Children.Add(Text(instrumentProfile.Name+" · "+CurrentTuning().Label,true));
        welcome.Children.Add(Text(remaining is null?"Vyber si nástroj nebo si zahraj volně v Live.":"Další krok: "+remaining.Title,true));
        var actions=Stack();
        if(remaining is not null){var begin=PrimaryAction(done==0?(planKind=="ai"?"Začít AI plán":"Začít dnešní plán"):"Pokračovat v plánu");begin.Click+=(_,_)=>OpenPlanTask(key,remaining);actions.Children.Add(begin);}
        var live=new Button{Content="Volné hraní v Live",MinHeight=40};live.Click+=(_,_)=>Navigate(new("02"));actions.Children.Add(live);
        if(appSettings.ResumeTask is { } resume){var next=new Button{Content="Pokračovat · "+TaskTitle(resume),MinHeight=40};next.Click+=(_,_)=>Navigate(resume);actions.Children.Add(next);}
        panel.Children.Add(Card(ResponsiveColumns(welcome,actions)));
        panel.Children.Add(BuildPracticeShortcuts());
        var select=new ComboBox{Header="Cvičební plán",ItemsSource=new[]{"Denní plán · bez účtu a internetu","Můj uložený AI plán"},SelectedIndex=planKind=="ai"?1:0,HorizontalAlignment=HorizontalAlignment.Stretch};
        select.SelectionChanged+=(_,_)=>{planKind=select.SelectedIndex==1?"ai":"daily";workspaceCache.Remove(new("01"));ShowWorkspace();};panel.Children.Add(select);
        if(missingAi){panel.Children.Add(Text("Zatím nemáš uložený AI plán. Denní plán funguje bez účtu i internetu.",true));var create=new Button{Content="Navrhnout AI plán",MinHeight=40};create.Click+=(_,_)=>Navigate(new("24"));panel.Children.Add(create);return panel;}
        panel.Children.Add(Text($"{tasks.Sum(t=>t.Minutes)} minut celkem · dokončeno {done} z {tasks.Count} bloků",true));
        var progress=new ProgressBar{Minimum=0,Maximum=Math.Max(1,tasks.Count),Value=done,Height=4};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(progress,"Dokončené bloky cvičebního plánu");panel.Children.Add(progress);
        var cards=new List<FrameworkElement>();int number=0;
        foreach(var task in tasks)
        {
            bool completed=PlanDone(key,task.Id);var row=Stack();var numberLabel=Text($"{++number:00}  /  {task.Minutes} MIN"+(completed?"  ·  HOTOVO":""));numberLabel.Style=(Style)Application.Current.Resources["VintageSectionTextStyle"];row.Children.Add(numberLabel);
            var taskTitle=Text(task.Title);taskTitle.FontSize=18;taskTitle.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;row.Children.Add(taskTitle);row.Children.Add(Text(task.Detail,true));
            var open=new Button{Content=completed?"Zopakovat blok":"Otevřít cvičení",MinHeight=40};open.Click+=(_,_)=>OpenPlanTask(key,task);row.Children.Add(open);cards.Add(Card(row));
        }
        panel.Children.Add(ResponsiveColumns(cards.ToArray()));
        if(remaining is null)panel.Children.Add(Text("Dnešní plán je hotový. Můžeš si zahrát v Live nebo zopakovat některý blok.",true));
        panel.Children.Add(Text("Hotový blok potvrzuješ sám. Čas se započítává až po zahájení cvičení a při aktivitě.",true));return panel;
    }
    private string TaskTitle(NavigationTarget target)
    {
        if(target.ViewId=="06" && target.ItemId is { } lesson)return catalog!.Lessons.FirstOrDefault(l=>l.Id==lesson)?.Title??WorkspaceViews.Get(target.ViewId).Title;
        if(target.ItemId is { } id){foreach(string group in new[]{"riffs","surgicalExercises","powerChordExercises","gallopExercises","palmMuteDrills","chords","scales"})if(catalog!.Find(group,id) is { } item)return item.TryGetProperty("title",out var title)?title.GetString()??id:item.TryGetProperty("name",out var name)?name.GetString()??id:id;}
        return WorkspaceViews.Get(target.ViewId).Title;
    }
    private void OpenPlanTask(string key,PlanTask task){openedPlanTask=(key,task);appliedNavigationTask=null;workspaceCache.Remove(task.Target);Navigate(task.Target);}
    private void RefreshPlanControls(){PlanButtons.Visibility=openedPlanTask is not null && CurrentTarget.ViewId!="01"?Visibility.Visible:Visibility.Collapsed;PlanTaskLabel.Text=openedPlanTask is { } p?"Plán · "+p.Task.Title:"";CompletePlan.IsEnabled=openedPlanTask is { } o && o.Key==CurrentPlanKey && CurrentTarget==o.Task.Target && !PlanDone(o.Key,o.Task.Id);desktopCompletePlan.Visibility=desktopEnabled && PlanButtons.Visibility==Visibility.Visible?Visibility.Visible:Visibility.Collapsed;desktopCompletePlan.IsEnabled=CompletePlan.IsEnabled;}
    private void ReturnPlan_Click(object sender,RoutedEventArgs args)=>Navigate(new("01"));
    private void CompletePlan_Click(object sender,RoutedEventArgs args)
    {
        if(openedPlanTask is not { } opened || !CompletePlan.IsEnabled)return;var previous=SelectedPlanProgress;var completed=previous?.Key==opened.Key?previous.Completed:Array.Empty<string>();var state=new PlanProgress(opened.Key,completed.Append(opened.Task.Id).Distinct(StringComparer.Ordinal).ToArray());appSettings=planKind=="ai"?appSettings with{AiPlanProgress=state}:appSettings with{PracticePlan=state};SaveSettings();workspaceCache.Remove(new("01"));RefreshPlanControls();
    }
    private async Task VerifyOverviewLiveAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        var previous=appSettings;planKind="daily";Navigate(new("01"));var tasks=CurrentPlanTasks();bool concrete=tasks.All(t=>!(t.Target.ViewId is "19" or "09") || t.Target.ItemId is not null);var task=tasks[0];string key=CurrentPlanKey;OpenPlanTask(key,task);bool reached=CurrentTarget==task.Target && CompletePlan.IsEnabled;CompletePlan_Click(this,new());CompletePlan_Click(this,new());await autosave!.FlushAsync();var snapshot=await (await DataRepository.OpenAsync(userFiles)).SnapshotAsync();var saved=UserData.Read<AppSettings>(snapshot.Documents.Single(d=>d.Kind=="settings").Value);bool once=saved.PracticePlan is { } state && state.Key==key && state.Completed.Count(i=>i==task.Id)==1 && saved.ResumeTask==task.Target;
        Navigate(new("02"));bool live=liveTunerNote is not null && liveTunerNeedle is not null && recordingStarts.Count>0 && audioHostsCreated==1;appSettings=previous;SaveSettings();await autosave.FlushAsync();openedPlanTask=null;Navigate(new("01"));bool pass=concrete && reached && once && live;WriteShellDiagnostic("overview-live.json",new{result=pass?"passed":"failed",exactDailyTargets=concrete,planRouteAndReturn=reached,completionSavedOnce=once,liveTunerAndSharedRecorder=live,hardwareVerified=false});if(!pass)throw new InvalidOperationException("Overview/Live integration failed.");
    }
}
