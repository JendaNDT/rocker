// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private FrameworkElement BuildAiFeedback()
    {var capturedDocument = documentContext.Current;
        var(panel,page)=BuildAiCommon("25","Zpětná vazba ke cvičení");panel.Children.Add(Text("Rady vycházejí jen z vybraných statistik a tvých poznámek. Tutor neslyší kytaru a nehodnotí přesnost nahrávky.",true));var notes=new TextBox{Header="Jak se ti hrálo / s čím bojuješ",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxLength=8000,MinHeight=100};panel.Children.Add(notes);page.Editors.Add(notes);page.Send.Content="Získat zpětnou vazbu";page.Send.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;BeginAi(page,async(key,token)=>{page.Response.Text="";string context=CurrentAiRequestContext+"\nDej krátkou podporující zpětnou vazbu a 2–3 konkrétní tipy. Drž se uvedených statistik a poznámek. Chybějící data nepovažuj za nulové cvičení. Nehodnoť kvalitu, přesnost ani zvuk neodeslané hry.";string text=await new AiHttpClient(aiHttp).GenerateAsync(key,CurrentAiRequestSettings.Model,[new("user","Zhodnoť dostupné podklady. Poznámky: "+notes.Text)],context,piece=>ShowAiPartial(page,piece,token),cancellation:token);token.ThrowIfCancellationRequested();page.Response.Text=text;page.Status.Text="Dokončená rada ze statistik a poznámek; žádný zvuk se neposílal.";});};panel.Children.Add(page.Send);panel.Children.Add(page.Response);return Card(panel);
    }
}
