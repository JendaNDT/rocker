// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Probe.Desktop;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private FrameworkElement BuildDesktopOverview()
    {
        var owner=documentContext.Current;string key=CurrentPlanKey;IReadOnlyList<PlanTask> tasks=planKind=="ai" && aiSettings.Plan is null?[]:CurrentPlanTasks();
        var list=new ListView{IsItemClickEnabled=true,SelectionMode=ListViewSelectionMode.Single,DisplayMemberPath="Title"};
        list.ItemsSource=tasks.Select(t=>new ListEntry(t.Id,(PlanDone(key,t.Id)?"Hotovo · ":"")+t.Title,t.Detail)).ToArray();
        var description=Text("Vyber blok plánu. Otevře se jako dokument nebo nástroj; ostatní práce zůstane otevřená.",true);
        var title=Text("Dnešní plán");title.FontSize=22;title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;
        var body=new Grid{RowSpacing=12};body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});body.Children.Add(title);Grid.SetRow(description,1);body.Children.Add(description);Grid.SetRow(list,2);body.Children.Add(list);
        var details=Stack();details.Children.Add(Text("MOJE KYTARA",true));details.Children.Add(Text(instrumentProfile.Name));details.Children.Add(Text(CurrentTuning().Label,true));
        var taskTitle=Text("Další krok");var taskDescription=Text("",true);details.Children.Add(taskTitle);details.Children.Add(taskDescription);
        var open=PrimaryAction("Otevřít vybraný blok");open.MinHeight=32;details.Children.Add(open);
        void Select(){if(list.SelectedItem is not ListEntry entry)return;var task=tasks.First(t=>t.Id==entry.Id);taskTitle.Text=task.Title;taskDescription.Text=task.Detail+$"\n{task.Minutes} minut";open.IsEnabled=true;}
        void Open(){if(list.SelectedItem is ListEntry entry)OpenPlanTask(key,tasks.First(t=>t.Id==entry.Id));}
        list.SelectionChanged+=(_,_)=>{using var scope=documentContext.Enter(owner);if(owner.IsAlive)Select();};list.ItemClick+=(_,e)=>{using var scope=documentContext.Enter(owner);if(!owner.IsAlive)return;list.SelectedItem=e.ClickedItem;Open();};open.Click+=(_,_)=>{using var scope=documentContext.Enter(owner);if(owner.IsAlive)Open();};
        var surface=new DocumentSurface(body,inspector:details);
        void Action(string label,Action callback){var button=new Button{Content=label,MinHeight=32};button.Click+=(_,_)=>{using var scope=documentContext.Enter(owner);if(owner.IsAlive)callback();};surface.Toolbar.Children.Add(button);}
        Action("Live · F11",()=>SetFocusMode(true));Action("Sestavy",()=>Setups_Click(this,new()));Action("Hledat · Ctrl+K",()=>Search_Click(this,new()));
        if(appSettings.ResumeTask is{ } resume)Action("Pokračovat",()=>Navigate(resume));
        var plan=new ComboBox{ItemsSource=new[]{"Denní plán","Uložený AI plán"},SelectedIndex=planKind=="ai"?1:0,MinWidth=150};Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(plan,"Typ cvičebního plánu");
        plan.SelectionChanged+=(_,_)=>{using var scope=documentContext.Enter(owner);if(!owner.IsAlive)return;planKind=plan.SelectedIndex==1?"ai":"daily";RebuildDesktopDocument(owner);};surface.Toolbar.Children.Add(plan);
        surface.Footer.Children.Add(Text($"{tasks.Sum(t=>t.Minutes)} minut · dokončeno {tasks.Count(t=>PlanDone(key,t.Id))} / {tasks.Count} bloků. Čas se počítá po zahájení cvičení při aktivitě.",true));
        if(tasks.Count>0){list.SelectedIndex=Math.Max(0,tasks.ToList().FindIndex(t=>!PlanDone(key,t.Id)));Select();}else{open.IsEnabled=false;description.Text="Uložený AI plán zatím chybí. Denní plán je dostupný bez internetu.";Action("Navrhnout AI plán",()=>Navigate(new("24")));}
        return surface;
    }
}
