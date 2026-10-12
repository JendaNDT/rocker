// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private async Task VerifyAiViewsAsync()
    {
        var previousSettings=aiSettings;var previousHistory=aiHistory.ToArray();Navigate(new("23"));Navigate(new("25"));Navigate(new("24"));bool views=aiPages.Count==3 && aiPages.Values.All(p=>p.Response.IsTextSelectionEnabled && p.Context.Text.Contains("Neobdržel jsi žádný zvuk",StringComparison.Ordinal));
        string lesson=catalog!.Lessons[0].Id;aiPlanDraft=new([new("Ladění","Nalaď kytaru",2,"03"),new("Rytmus","Rozehřej prsty",3,"04"),new("Lekce","Pokračuj lekcí",5,"06",lesson)],10);RenderAiPlanDraft();aiPlanEditors[2].Detail.Text="Upravený pokyn";AcceptAiPlan();var editedPlan=aiSettings.Plan;bool edited=editedPlan is not null && editedPlan.Items[2].Detail=="Upravený pokyn" && editedPlan.Items[2].ItemId==lesson;
        aiPlanEditors[2].Item.ItemsSource=new[]{new ListEntry("unknown-task","Neplatná úloha","")};aiPlanEditors[2].Item.SelectedValue="unknown-task";bool rejected=false;try{AcceptAiPlan();}catch(System.IO.InvalidDataException){rejected=aiSettings.Plan?.Items[2].ItemId==lesson;}
        aiHistory.Clear();aiHistory.Add(new("user","Původní zpráva bez času"));aiHistory.Add(new("model","Ukázkový text; žádná síťová odpověď",DateTimeOffset.UtcNow));PersistAiHistory();RefreshAiChat();bool history=aiChatList?.Items.Count==2;
        await autosave!.FlushAsync();var snapshot=await (await DataRepository.OpenAsync(userFiles)).SnapshotAsync();var saved=UserData.Read<AiSettings>(snapshot.Documents.Single(d=>d.Kind=="ai-settings").Value);var messages=UserData.Read<SavedChatMessage[]>(snapshot.Documents.Single(d=>d.Kind=="chat").Value);bool persisted=saved.Plan is {TotalMinutes:10,AcceptedAt:not null} && messages.Length==2 && messages[0].CreatedAt is null && messages[1].CreatedAt is not null;
        aiSettings=previousSettings;aiPlanDraft=previousSettings.Plan;aiHistory.Clear();aiHistory.AddRange(previousHistory);PersistAiSettings();PersistAiHistory();await autosave.FlushAsync();RenderAiPlanDraft();RefreshAiChat();Navigate(new("01"));bool pass=views && edited && rejected && history && persisted;
        WriteShellDiagnostic("ai-views.json",new{result=pass?"passed":"failed",nativeViews=3,editablePlanVerified=edited,unknownTaskRejected=rejected,nativeHistoryList=history,persistenceReopened=persisted,realProviderRequestVerified=false,realCredentialUsed=false});if(!pass)throw new InvalidOperationException("Native AI views/persistence check failed.");
    }
}
