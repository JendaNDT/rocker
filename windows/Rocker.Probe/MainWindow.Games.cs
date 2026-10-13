// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Probe.Controls;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private static readonly int[] GameStandardTuning=[40,45,50,55,59,64];
    private GameScore fretScore { get => documentContext.Current.Get<GameScore>("fretScore", () => new()); set => documentContext.Current.Set("fretScore",value); }
    private GameScore earScore { get => documentContext.Current.Get<GameScore>("earScore", () => new()); set => documentContext.Current.Set("earScore",value); }private int fretTarget { get => documentContext.Current.Get<int>("fretTarget", () => default); set => documentContext.Current.Set("fretTarget",value); }
    private int earIndex { get => documentContext.Current.Get<int>("earIndex", () => default); set => documentContext.Current.Set("earIndex",value); }
    private int earRoot { get => documentContext.Current.Get<int>("earRoot", () => 48); set => documentContext.Current.Set("earRoot",value); }
    private int earKind { get => documentContext.Current.Get<int>("earKind", () => default); set => documentContext.Current.Set("earKind",value); }
    private bool fretStarted { get => documentContext.Current.Get<bool>("fretStarted", () => default); set => documentContext.Current.Set("fretStarted",value); }
    private bool fretHintUsed { get => documentContext.Current.Get<bool>("fretHintUsed", () => default); set => documentContext.Current.Set("fretHintUsed",value); }
    private bool earStarted { get => documentContext.Current.Get<bool>("earStarted", () => default); set => documentContext.Current.Set("earStarted",value); }
    private bool earHeard { get => documentContext.Current.Get<bool>("earHeard", () => default); set => documentContext.Current.Set("earHeard",value); }
    private bool earOperation { get => documentContext.Current.Get<bool>("earOperation", () => default); set => documentContext.Current.Set("earOperation",value); }private int earGeneration { get => documentContext.Current.Get<int>("earGeneration", () => default); set => documentContext.Current.Set("earGeneration",value); }
    private FretboardDiagram? gameBoard { get => documentContext.Current.Get<FretboardDiagram?>("gameBoard", () => default); set => documentContext.Current.Set("gameBoard",value); }private TextBlock? fretQuestion { get => documentContext.Current.Get<TextBlock?>("fretQuestion", () => default); set => documentContext.Current.Set("fretQuestion",value); }
    private TextBlock? fretFeedback { get => documentContext.Current.Get<TextBlock?>("fretFeedback", () => default); set => documentContext.Current.Set("fretFeedback",value); }
    private TextBlock? earQuestion { get => documentContext.Current.Get<TextBlock?>("earQuestion", () => default); set => documentContext.Current.Set("earQuestion",value); }
    private TextBlock? earFeedback { get => documentContext.Current.Get<TextBlock?>("earFeedback", () => default); set => documentContext.Current.Set("earFeedback",value); }
    private ComboBox? earMode { get => documentContext.Current.Get<ComboBox?>("earMode", () => default); set => documentContext.Current.Set("earMode",value); }
    private ComboBox? earOrder { get => documentContext.Current.Get<ComboBox?>("earOrder", () => default); set => documentContext.Current.Set("earOrder",value); }private List<Button> earAnswers { get => documentContext.Current.Get<List<Button>>("earAnswers", () => []); }
    private DateTimeOffset? recentExerciseInteraction;
    private FrameworkElement BuildFretboardGame()
    {var capturedDocument = documentContext.Current;
        var panel=Stack();panel.Children.Add(Text("Hra na hmatníku · najdi tón"));panel.Children.Add(Text("Hra používá standardní ladění, stejně jako původní Rocker. Šipky přesouvají kurzor, Enter vybírá pozici. Nápověda ukáže všechny správné pozice; taková odpověď je bez bodu.",true));fretQuestion=Text("Začni novou hru.");fretQuestion.FontSize=28;panel.Children.Add(fretQuestion);fretFeedback=Text("",true);panel.Children.Add(fretFeedback);gameBoard=new FretboardDiagram();panel.Children.Add(gameBoard);
        var start=new Button{Content="Nová hra"};start.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);fretScore=new();fretTarget=MusicShapes.PickIndex(12,null,Random.Shared.NextDouble());fretStarted=true;fretHintUsed=false;fretFeedback.Text="";UpdateFretGame();};panel.Children.Add(start);
        var hint=new Button{Content="Ukázat správné pozice bez bodu"};hint.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!fretStarted)return;fretHintUsed=true;gameBoard.SetTuning(GameStandardTuning,[0],fretTarget,appSettings.Handedness=="left",noteNotation:SelectedNotation,concealNotes:true);fretFeedback.Text="Nápověda: 1 označuje hledaný tón. Tato odpověď je bez bodu.";};panel.Children.Add(hint);gameBoard.NoteSelected+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(fretStarted && gameBoard.SelectedMidi is int midi)AnswerFretGame(midi,true);};UpdateFretGame();return Card(panel);
    }
    private void AnswerFretGame(int midi,bool userInteraction)
    {
        if(!fretStarted)return;var result=LibraryPractice.Answer(fretScore,fretTarget,MusicShapes.NoteAtFret(midi,0),fretHintUsed);fretScore=result.Score;
        if(userInteraction)recentExerciseInteraction=DateTimeOffset.UtcNow;
        fretFeedback!.Text=result.Correct?(fretHintUsed?"Správně s nápovědou, bez bodu.":"Správně. Najdi další tón."):"Na této pozici je "+Music.PitchClassName(midi,SelectedNotation)+". Zkus jinou.";
        if(result.Correct){fretTarget=MusicShapes.PickIndex(12,fretTarget,Random.Shared.NextDouble());fretHintUsed=false;UpdateFretGame();}else UpdateFretQuestion();
    }
    private void UpdateFretQuestion(){if(fretQuestion is not null)fretQuestion.Text=fretStarted?$"Najdi {Music.PitchClassName(fretTarget,SelectedNotation)} · skóre {fretScore.Score}, série {fretScore.Streak}":"Začni novou hru.";}
    private void UpdateFretGame(){UpdateFretQuestion();gameBoard?.SetTuning(GameStandardTuning,mirror:appSettings.Handedness=="left",noteNotation:SelectedNotation,concealNotes:true);}
    private IReadOnlyList<EarQuestion> EarOptions=>earMode?.SelectedIndex==1?LibraryPractice.EarChords:LibraryPractice.EarIntervals;
    private FrameworkElement BuildEarGame()
    {var capturedDocument = documentContext.Current;
        var panel=Stack();panel.Children.Add(Text("Sluch · intervaly a druhy akordů"));panel.Children.Add(Text("Mikrofon není potřeba. Poslech používá společný zvukový výstup a syntetické tóny. Nejdřív si otázku přehraj; pak zvol odpověď myší nebo klávesnicí.",true));earMode=new ComboBox{Header="Režim",ItemsSource=new[]{"Intervaly","Akordy"},SelectedIndex=earKind};earOrder=new ComboBox{Header="Poslech tónů",ItemsSource=new[]{"Současně","Postupně"},SelectedIndex=0};panel.Children.Add(earMode);panel.Children.Add(earOrder);earQuestion=Text("Začni novou hru.");panel.Children.Add(earQuestion);earFeedback=Text("",true);panel.Children.Add(earFeedback);
        var answers=Stack();panel.Children.Add(answers);void BuildAnswers(){earAnswers.Clear();answers.Children.Clear();for(int i=0;i<EarOptions.Count;++i){int chosen=i;var button=new Button{Content=$"{i+1}. {EarOptions[i].Name}",IsEnabled=false,AccessKey=(i+1).ToString()};button.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(AnswerEarGame(chosen,true))await PlayEarAsync();};earAnswers.Add(button);answers.Children.Add(button);}}
        earMode.SelectionChanged+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);earKind=earMode.SelectedIndex;++earGeneration;if(audio is not null)try{await audio.StopPreviewAsync();}catch(Exception error){ShowError(error);}earStarted=false;earHeard=false;earScore=new();earFeedback.Text="Režim změněn. Začni novou hru.";BuildAnswers();UpdateEarGame();};
        var start=new Button{Content="Nová hra a první poslech"};start.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(earOperation)return;earScore=new();earIndex=MusicShapes.PickIndex(EarOptions.Count,null,Random.Shared.NextDouble());earRoot=MusicShapes.PickRoot(Random.Shared.NextDouble());earStarted=true;earHeard=false;earFeedback.Text="";await PlayEarAsync();};panel.Children.Add(start);var replay=new Button{Content="Zopakovat stejnou otázku"};replay.Click+=async(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);await PlayEarAsync();};panel.Children.Add(replay);var explain=new Button{Content="Odhalit odpověď a pokračovat bez bodu"};explain.Click+=(_,_)=>{using var documentScope = documentContext.Enter(capturedDocument);if(!earStarted || earOperation)return;var name=EarOptions[earIndex].Name;earScore=earScore with{Streak=0};NextEarQuestion();earFeedback.Text="Předchozí odpověď: "+name+". Novou otázku si přehraj.";UpdateEarGame();};panel.Children.Add(explain);BuildAnswers();UpdateEarGame();return Card(panel);
    }
    private void NextEarQuestion(){++earGeneration;earIndex=MusicShapes.PickIndex(EarOptions.Count,earIndex,Random.Shared.NextDouble());earRoot=MusicShapes.PickRoot(Random.Shared.NextDouble());earHeard=false;}
    private bool AnswerEarGame(int chosen,bool userInteraction)
    {
        if(!earStarted || !earHeard || earOperation)return false;var options=EarOptions;var result=LibraryPractice.Answer(earScore,earIndex,chosen);earScore=result.Score;if(userInteraction)recentExerciseInteraction=DateTimeOffset.UtcNow;
        earFeedback!.Text=result.Correct?"Správně: "+options[earIndex].Name+". Připravuji další otázku.":"Zvolená odpověď nesedí. Zopakuj poslech a zkus to znovu.";if(result.Correct)NextEarQuestion();UpdateEarGame();return result.Correct;
    }
    private async Task PlayEarAsync()
    {
        if(!earStarted || earOperation || audio is null)return;earOperation=true;int generation=earGeneration;var question=EarOptions[earIndex];int root=earRoot;bool sequential=earOrder?.SelectedIndex==1;earHeard=false;UpdateEarGame();
        try{bool accepted=await PlayTeachingAsync(question.Intervals.Select((n,i)=>new TeachingNote(sequential?i*.4:0,sequential?.35:1.2,root+n)).ToArray());if(generation!=earGeneration || CurrentTarget.ViewId!="22")return;earHeard=accepted;if(accepted)recentExerciseInteraction=DateTimeOffset.UtcNow;else earFeedback!.Text="Výstup není připravený. Vyber zvukové zařízení a zopakuj poslech.";}
        finally{earOperation=false;UpdateEarGame();}
    }
    private void UpdateEarGame(){if(earQuestion is null)return;earQuestion.Text=earStarted?$"Skóre {earScore.Score} · série {earScore.Streak} · "+(earOperation?"Připravuji poslech…":earHeard?"Vyber odpověď.":"Přehraj otázku."):"Začni novou hru.";foreach(var button in earAnswers)button.IsEnabled=earStarted && earHeard && !earOperation;}
    private void ResetLibraryGames(){++earGeneration;earStarted=earHeard=fretStarted=fretHintUsed=false;fretScore=new();earScore=new();earKind=0;earIndex=fretTarget=0;earRoot=48;selectedRiff=null;gameBoard=null;libraryChord=null;libraryScale=null;earQuestion=fretQuestion=null;earFeedback=fretFeedback=null;earAnswers.Clear();libraryCounts.Clear();recentExerciseInteraction=null;}
    private async Task PauseEarPreviewAsync(){++earGeneration;earHeard=false;if(audio is not null)try{await audio.StopPreviewAsync();}catch(Exception error){if(!closed)ShowError(error);}UpdateEarGame();}

}
