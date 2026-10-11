// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Rocker.Domain;
using Rocker.Interop;
using Rocker.Probe.Controls;
using System.Text.Json;
using Windows.System;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private string? activeLessonId;
    private TextBlock? lessonReading;
    private ulong lessonLastSequence;
    private FrameworkElement BuildLessonList()
    {
        var panel=Stack();panel.Children.Add(Text("Lekce · od prvního tónu k metalovým riffům"));
        var search=new TextBox{Header="Hledat lekci",MaxLength=128};var stage=new ComboBox{Header="Část výuky",DisplayMemberPath="Title",ItemsSource=catalog!.Items("stages").Select(s=>new ListEntry(s.GetProperty("id").GetString()!,s.GetProperty("title").GetString()!,s.GetProperty("description").GetString()!)).ToArray()};var incomplete=new CheckBox{Content="Pouze nedokončené",IsChecked=false};
        var list=new ListView{IsItemClickEnabled=true,SelectionMode=ListViewSelectionMode.Single,DisplayMemberPath="Title"};
        void Filter(){string? stageId=(stage.SelectedItem as ListEntry)?.Id;var ids=stageId is null?null:catalog.Items("stages").First(s=>s.GetProperty("id").GetString()==stageId).GetProperty("lessons").EnumerateArray().Select(l=>l.GetProperty("id").GetString()).ToHashSet();list.ItemsSource=catalog.Lessons.Where(l=>(ids is null || ids.Contains(l.Id)) && (incomplete.IsChecked!=true || !completedLessons.Contains(l.Id)) && l.Title.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).Select(l=>new ListEntry(l.Id,(completedLessons.Contains(l.Id)?"Dokončeno · ":savedLessonPositions.ContainsKey(l.Id)?"Rozpracováno · ":"")+l.Title,$"{l.EstimatedMinutes} minut")).ToArray();}
        list.ItemClick+=(_,e)=>{if(e.ClickedItem is ListEntry selected)Navigate(new("06",selected.Id));};search.TextChanged+=(_,_)=>Filter();stage.SelectionChanged+=(_,_)=>Filter();incomplete.Checked+=(_,_)=>Filter();incomplete.Unchecked+=(_,_)=>Filter();
        panel.Children.Add(search);panel.Children.Add(stage);var all=new Button{Content="Všechny části"};all.Click+=(_,_)=>stage.SelectedIndex=-1;panel.Children.Add(all);panel.Children.Add(incomplete);panel.Children.Add(list);Filter();return Card(panel);
    }
    private FrameworkElement BuildLesson(string? id)
    {
        var lesson=catalog!.Lessons.FirstOrDefault(l=>l.Id==id)??LessonRules.NextIncomplete(catalog.Lessons,completedLessons)??catalog.Lessons[0];activeLessonId=lesson.Id;
        if(!lessonSessions.ContainsKey(lesson.Id))lessonSessions.Add(lesson.Id,LessonSession.Start(lesson,savedLessonPositions.GetValueOrDefault(lesson.Id)) with{Completed=completedLessons.Contains(lesson.Id)});
        var panel=Stack();
        async Task Change(bool forward){if(audio is not null)await audio.StopPreviewAsync();var current=lessonSessions[lesson.Id];if(current.Completed)return;lessonSessions[lesson.Id]=forward?(current.IsLast?current.Finish():current.Next()):current.Previous();if(forward && current.IsLast)completedLessons.Add(lesson.Id);SaveLessonProgress();Render();}
        void Render()
        {
            panel.Children.Clear();lessonLastSequence=0;var session=lessonSessions[lesson.Id];var step=session.CurrentStep;string kind=step.GetProperty("kind").GetString()!;
            panel.Children.Add(Text(lesson.Title));panel.Children.Add(Text($"Krok {session.StepIndex+1} / {lesson.Steps.Count} · {lesson.EstimatedMinutes} minut",true));panel.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=session.ProgressPercent});panel.Children.Add(Text(step.GetProperty("title").GetString()!));panel.Children.Add(Text(step.GetProperty("body").GetString()!));
            lessonReading=Text(kind=="note"?"Spusť vstup a zahraj jednotlivý cílový tón. Bez vstupu se krok nepotvrdí měřením.":"",true);panel.Children.Add(lessonReading);
            if(kind=="note")
            {
                int midi=step.GetProperty("targetMidi").GetInt32();panel.Children.Add(Text("Cílový tón: "+step.GetProperty("targetLabel").GetString()));
                var listen=new Button{Content="Poslechnout cílový tón"};listen.Click+=async(_,_)=>await PlayTeachingAsync([new(0,1.2,midi)]);panel.Children.Add(listen);
                var input=new Button{Content="Spustit kytarový vstup"};input.Click+=LiveStart_Click;panel.Children.Add(input);panel.Children.Add(Text("Potvrzení tónu je zpětná vazba. Pokračovat můžeš i ručně; bez měření se neoznačí správně zahraný tón.",true));
            }
            if(kind=="chord")
            {
                var chord=catalog.Find("chords",step.GetProperty("chordId").GetString()!)!.Value;var diagram=new ChordDiagram();diagram.ShowChord(chord,appSettings.Handedness=="left");panel.Children.Add(diagram);
                var listen=new Button{Content="Poslechnout akord"};listen.Click+=async(_,_)=>await PlayTeachingAsync(TeachingPreview.Chord(chord));panel.Children.Add(listen);var strings=new Button{Content="Poslechnout jednotlivé struny"};strings.Click+=async(_,_)=>await PlayTeachingAsync(TeachingPreview.Chord(chord,arpeggio:true));panel.Children.Add(strings);
                panel.Children.Add(Text("Diagram a ukázka jsou pro standardní ladění. Hraj se mnou: poslechni ukázku a zkontroluj čistotu strun sluchem; tento režim nevyhodnocuje celý akord mikrofonem.",true));
            }
            if(session.Completed){panel.Children.Add(Text("Lekce je dokončená."));var replay=new Button{Content="Projít lekci znovu"};replay.Click+=(_,_)=>{lessonSessions[lesson.Id]=LessonSession.Start(lesson);SaveLessonProgress();Render();};panel.Children.Add(replay);}
            var previous=new Button{Content="Zpět",IsEnabled=!session.IsFirst && !session.Completed,AccessKey="P"};var next=new Button{Content=session.IsLast?"Dokončit lekci":"Další",IsEnabled=!session.Completed,AccessKey="D"};previous.Click+=async(_,_)=>await Change(false);next.Click+=async(_,_)=>await Change(true);
            var previousKey=new KeyboardAccelerator{Key=VirtualKey.Left,Modifiers=VirtualKeyModifiers.Control};previousKey.Invoked+=async(_,e)=>{if(navigation.Current.ViewId=="06" && previous.IsEnabled){e.Handled=true;await Change(false);}};previous.KeyboardAccelerators.Add(previousKey);
            var nextKey=new KeyboardAccelerator{Key=VirtualKey.Right,Modifiers=VirtualKeyModifiers.Control};nextKey.Invoked+=async(_,e)=>{if(navigation.Current.ViewId=="06" && next.IsEnabled){e.Handled=true;await Change(true);}};next.KeyboardAccelerators.Add(nextKey);
            panel.Children.Add(previous);panel.Children.Add(next);var back=new Button{Content="Výběr lekcí"};back.Click+=(_,_)=>Navigate(new("05"));panel.Children.Add(back);
        }
        Render();return Card(panel);
    }
    private async Task<bool> PlayTeachingAsync(IReadOnlyList<TeachingNote> notes,bool repeat=false,uint countIn=0)
    {
        if(audio is null || closingWindow || deviceOperationRunning)return false;if(deviceSelectionUnavailable){ShowPersistenceError("Vyber dostupné výstupní zařízení v nastavení zvuku.");return false;}
        try{await audio.PlayPreviewAsync(notes.Select(n=>new PreviewNote(n.Start,n.Duration,TunerFrequency(n.Midi),n.Volume)).ToArray(),repeat,countIn);RefreshState();return true;}catch(Exception error){ShowError(error);return false;}
    }
    private void RefreshLesson(AnalysisSnapshot reading)
    {
        if(navigation.Current.ViewId!="06" || activeLessonId is null || lessonReading is null || !lessonSessions.TryGetValue(activeLessonId,out var session) || session.CurrentStep.GetProperty("kind").GetString()!="note")return;
        if(session.Completed)return;if(session.NotePassed){lessonReading.Text="Správný tón zaznamenán ze vstupu. Můžeš pokračovat.";return;}
        if(!reading.ActiveInput || reading.Stale){lessonReading.Text="Vstup neběží. Čtení a poslech zůstávají dostupné; tón zatím není potvrzený.";return;}
        if(reading.Sequence==lessonLastSequence)return;lessonLastSequence=reading.Sequence;
        var note=reading.PitchDetected?Music.FrequencyToNote(reading.Frequency*440/(appSettings.ReferenceA??440)):null;
        var next=session.ObserveNote(note?.Midi);lessonSessions[activeLessonId]=next;lessonReading.Text=next.NotePassed?"Správný tón zaznamenán ze vstupu. Můžeš pokračovat.":note is null?"Zahraj jednu strunu; signál zatím nemá jistý tón.":"Měřím "+NoteLabel(note.Midi)+". Cíl je "+session.CurrentStep.GetProperty("targetLabel").GetString()+".";
    }
    private async Task VerifyLessonsAsync()
    {
        int kinds=0;foreach(string kind in new[]{"text","note","chord"}){var lesson=catalog!.Lessons.First(l=>l.Steps.Any(s=>s.GetProperty("kind").GetString()==kind));lessonSessions[lesson.Id]=LessonSession.Start(lesson,lesson.Steps.ToList().FindIndex(s=>s.GetProperty("kind").GetString()==kind));Navigate(new("06",lesson.Id));if(WorkspaceContent.Content is not FrameworkElement)throw new InvalidOperationException("Lesson did not construct a native page.");++kinds;}
        var complete=catalog!.Lessons.Last();var session=LessonSession.Start(complete,complete.Steps.Count-1).Finish();lessonSessions[complete.Id]=session;completedLessons.Add(complete.Id);completedLessons.Add(complete.Id);SaveLessonProgress();await autosave!.FlushAsync();var documents=(await (await Rocker.Infrastructure.DataRepository.OpenAsync(userFiles)).SnapshotAsync()).Documents;var persisted=UserData.Read<LessonProgress>(documents.Single(d=>d.Kind=="lessons").Value);bool passed=kinds==3 && persisted.CompletedIds.Count(id=>id==complete.Id)==1 && persisted.Positions[complete.Id]==complete.Steps.Count-1;
        WriteShellDiagnostic("lessons-view.json",new{result=passed?"passed":"failed",nativeStepKinds=kinds,totalLessons=catalog.Lessons.Count,persistenceReopened=true,completionSavedOnce=passed,physicalPitchCheckVerified=false,keyboardManualVerified=false});Navigate(new("01"));if(!passed)throw new InvalidOperationException("Native lesson/session check failed.");
    }
}
