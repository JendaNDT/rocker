// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
using Windows.Storage.Pickers;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private ListView? aiChatList { get => documentContext.Current.Get<ListView?>("aiChatList", () => default); set => documentContext.Current.Set("aiChatList",value); }
    private FrameworkElement BuildAiChat()
    {var capturedDocument = documentContext.Current;
        var(panel,page)=BuildAiCommon("23","Zeptej se kytarového tutora");aiChatList=new ListView{Header="Uložený rozhovor",SelectionMode=ListViewSelectionMode.Single};panel.Children.Add(aiChatList);RefreshAiChat();
        var prompt=new TextBox{Header="Tvoje otázka",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxLength=8000,MinHeight=100};panel.Children.Add(prompt);page.Editors.Add(prompt);
        page.Send.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;BeginAi(page,async(key,token)=>
        {
            string question=prompt.Text.Trim();if(question.Length==0)throw new AiRequestException("Nejdřív napiš otázku.");var user=new SavedChatMessage("user",question,DateTimeOffset.UtcNow);var proposed=aiHistory.Append(user).ToArray();_ = UserData.Document("chat","default",proposed);var history=proposed.TakeLast(40).Select(m=>new ChatMessage(m.Role,m.Text)).ToArray();if(history.Sum(m=>m.Text.Length)>64000)throw new AiRequestException("Posledních 40 zpráv je příliš dlouhých. Exportuj historii a začni nový rozhovor.");
            aiHistory.Add(user);PersistAiHistory();RefreshAiChat();prompt.Text="";page.Response.Text="";page.Status.Text="Odesláno. Kontext rozhovoru zahrnuje nejvýše posledních 40 zpráv.";
            string text=await new AiHttpClient(aiHttp).GenerateAsync(key,aiSettings.Model,history,AiContext(page.Progress.IsChecked==true,page.Instrument.IsChecked==true),piece=>ShowAiPartial(page,piece,token),cancellation:token);token.ThrowIfCancellationRequested();var answer=new SavedChatMessage("model",text,DateTimeOffset.UtcNow);_ =UserData.Document("chat","default",aiHistory.Append(answer).ToArray());aiHistory.Add(answer);PersistAiHistory();RefreshAiChat();page.Response.Text=text;page.Status.Text="Odpověď je dokončená a uložená.";
        });};panel.Children.Add(page.Send);panel.Children.Add(page.Response);
        var delete=new Button{Content="Smazat vybranou zprávu"};delete.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(aiBusy || aiChatList?.SelectedIndex is not >=0)return;aiHistory.RemoveAt(aiChatList.SelectedIndex);PersistAiHistory();RefreshAiChat();};panel.Children.Add(delete);
        var clear=new Button{Content="Vymazat historii rozhovoru"};clear.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;if(aiBusy)return;var dialog=new ContentDialog{XamlRoot=DialogRoot,Title="Vymazat historii?",Content="Zprávy budou odstraněné z místní aplikace. Před smazáním je můžeš exportovat.",PrimaryButtonText="Vymazat",CloseButtonText="Zrušit",DefaultButton=ContentDialogButton.Close};if(await ShowDialogAsync(dialog)==ContentDialogResult.Primary){aiHistory.Clear();PersistAiHistory();RefreshAiChat();page.Response.Text="";}};panel.Children.Add(clear);
        var export=new Button{Content="Exportovat rozhovor do textového souboru…"};export.Click+=(__sender,__args)=>{using var documentScope=documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;ExportAiChat_Click(__sender,__args);};panel.Children.Add(export);return Card(panel);
    }
    private void RefreshAiChat(){if(aiChatList is not null)aiChatList.ItemsSource=aiHistory.Select(m=>$"{(m.Role=="user"?"Ty":"Tutor")}{(m.CreatedAt is { } date?" · "+date.ToLocalTime().ToString("g"):"")}\n{m.Text}").ToArray();}
    private async void ExportAiChat_Click(object sender,RoutedEventArgs args)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        try{var picker=new FileSavePicker{SuggestedFileName="Rocker-rozhovor",SuggestedStartLocation=PickerLocationId.DocumentsLibrary};picker.FileTypeChoices.Add("Textový rozhovor",[".txt"]);WinRT.Interop.InitializeWithWindow.Initialize(picker,DialogWindowHandle);var file=await picker.PickSaveFileAsync();if(file is null || closed)return;string text=string.Join("\n\n",aiHistory.Select(m=>$"{m.Role} · {m.CreatedAt?.ToString("O")??"čas neuveden"}\n{m.Text}"));await UserFiles.WriteTextAsync(file.Path,text);if(aiPages.TryGetValue("23",out var page))page.Status.Text="Rozhovor byl exportován bez API klíče.";}catch(Exception error){ShowError(error);}
    }
}
