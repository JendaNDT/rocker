// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private AiPracticePlan? aiPlanDraft;private StackPanel? aiPlanBlocks;
    private readonly List<(TextBox Title,TextBox Detail,NumberBox Minutes,ComboBox View,ComboBox Item)> aiPlanEditors=[];
    private FrameworkElement BuildAiPlan()
    {
        var(panel,page)=BuildAiCommon("24","Navrhni mi cvičební plán");var minutes=new NumberBox{Header="Kolik máš minut",Minimum=5,Maximum=120,Value=20,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};var focus=new TextBox{Header="Na co se zaměřit",MaxLength=2000};panel.Children.Add(minutes);panel.Children.Add(focus);page.Editors.AddRange([minutes,focus]);
        page.Send.Content="Vygenerovat návrh plánu";page.Send.Click+=(_,_)=>BeginAi(page,async(key,token)=>
        {
            if(!double.IsFinite(minutes.Value))throw new AiRequestException("Vyber délku cvičení.");int target=(int)minutes.Value;string instruction=AiContext(page.Progress.IsChecked==true,page.Instrument.IsChecked==true)+"\nVytvoř 3 až 6 stručných českých bloků. Celková délka odpovídá zadání s tolerancí 2 minuty. Začni naladěním, rozehřej prsty a skonči hudební frází. Používej jen tyto nativní pohledy a existující ID; pokud konkrétní úloha není vhodná, itemId je null:\n"+AiPlanTargetContext();
            string answer=await new AiHttpClient(aiHttp).GenerateAsync(key,aiSettings.Model,[new("user",$"Mám {target} minut. Zaměření (text uživatele): {focus.Text}")],instruction,cancellation:token,responseSchema:AiPlanRules.ResponseSchema);token.ThrowIfCancellationRequested();var draft=AiPlanRules.Parse(answer,catalog!,target);aiPlanDraft=draft;RenderAiPlanDraft();page.Status.Text=$"Návrh má {draft.TotalMinutes} minut. Uprav bloky a potom jej výslovně ulož; zatím se neaplikoval.";
        });panel.Children.Add(page.Send);aiPlanBlocks=Stack();panel.Children.Add(aiPlanBlocks);RenderAiPlanDraft();var accept=new Button{Content="Uložit upravený plán pro cvičení"};accept.Click+=(_,_)=>{if(aiBusy)return;try{AcceptAiPlan();page.Status.Text="Plán je uložený. Každý blok otevře odpovídající úlohu.";}catch(Exception){page.Status.Text="Uprav délky, texty a odkazy. Plán musí mít 3–6 bloků a existující úlohy.";}};panel.Children.Add(accept);var discard=new Button{Content="Vrátit poslední uložený plán"};discard.Click+=(_,_)=>{if(aiBusy)return;aiPlanDraft=aiSettings.Plan;RenderAiPlanDraft();};panel.Children.Add(discard);return Card(panel);
    }
    private string AiPlanTargetContext()=>string.Join("\n",AiPlanRules.Targets.Select(pair=>pair.Key+" "+WorkspaceViews.Get(pair.Key).Title+": "+(pair.Value=="lessons"?string.Join(", ",catalog!.Lessons.Select(l=>l.Id)):pair.Value is { } group?string.Join(", ",catalog!.Items(group).Select(i=>i.GetProperty("id").GetString())):"itemId=null")));
    private void RenderAiPlanDraft()
    {
        if(aiPlanBlocks is null)return;aiPlanBlocks.Children.Clear();aiPlanEditors.Clear();if(aiPlanDraft is null){aiPlanBlocks.Children.Add(Text("Žádný plán není uložený. Můžeš použít i denní plán na Přehledu bez AI.",true));return;}
        foreach(var block in aiPlanDraft.Items)
        {
            var panel=Stack();var title=new TextBox{Header="Název bloku",Text=block.Title,MaxLength=128};var detail=new TextBox{Header="Pokyn",Text=block.Detail,MaxLength=1024,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap};var minutes=new NumberBox{Header="Minuty",Minimum=1,Maximum=60,Value=block.Minutes,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};var view=new ComboBox{Header="Obrazovka",ItemsSource=AiPlanRules.Targets.Keys.Select(id=>new ListEntry(id,WorkspaceViews.Get(id).Title,"")).ToArray(),DisplayMemberPath="Title",SelectedValuePath="Id",SelectedValue=block.ViewId};var item=new ComboBox{Header="Konkrétní úloha",DisplayMemberPath="Title",SelectedValuePath="Id",HorizontalAlignment=HorizontalAlignment.Stretch};
            void Tasks(string? selected){string viewId=(string)view.SelectedValue;string? group=AiPlanRules.Targets.GetValueOrDefault(viewId);var entries=new List<ListEntry>{new("","Obecný pohled","")};if(group=="lessons")entries.AddRange(catalog!.Lessons.Select(l=>new ListEntry(l.Id,l.Title,"")));else if(group is not null)entries.AddRange(catalog!.Items(group).Select(v=>new ListEntry(v.GetProperty("id").GetString()!,v.TryGetProperty("title",out var name)?name.GetString()!:v.TryGetProperty("name",out name)?name.GetString()!:v.GetProperty("label").GetString()!,"")));item.ItemsSource=entries;item.SelectedValue=selected??"";if(item.SelectedIndex<0)item.SelectedIndex=0;}
            Tasks(block.ItemId);view.SelectionChanged+=(_,_)=>Tasks(null);panel.Children.Add(title);panel.Children.Add(detail);panel.Children.Add(minutes);panel.Children.Add(view);panel.Children.Add(item);var open=new Button{Content="Otevřít tento blok"};open.Click+=(_,_)=>{try{var chosen=new AiPlanItem(title.Text,detail.Text,(int)minutes.Value,(string)view.SelectedValue,SelectedAiTask(item));AiPlanRules.ValidateTarget(chosen,catalog!);Navigate(new(chosen.ViewId,chosen.ItemId));}catch(Exception){ShowPersistenceError("Odkaz na úlohu není platný.");}};panel.Children.Add(open);aiPlanEditors.Add((title,detail,minutes,view,item));aiPlanBlocks.Children.Add(Card(panel));
        }
    }
    private static string? SelectedAiTask(ComboBox box)=>box.SelectedValue is string {Length:>0} id?id:null;
    private void AcceptAiPlan()
    {
        if(aiPlanEditors.Count==0)throw new InvalidDataException("Plán chybí.");var blocks=aiPlanEditors.Select(e=>new AiPlanItem(e.Title.Text.Trim(),e.Detail.Text.Trim(),double.IsFinite(e.Minutes.Value)?(int)e.Minutes.Value:0,(string)e.View.SelectedValue,SelectedAiTask(e.Item))).ToArray();var plan=AiPlanRules.Validate(new(blocks,blocks.Sum(i=>i.Minutes),DateTimeOffset.UtcNow),catalog);aiSettings=aiSettings with{Plan=plan};PersistAiSettings();aiPlanDraft=plan;workspaceCache.Remove(new("01"));
    }
}
