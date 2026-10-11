// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Domain;
using Rocker.Probe.Controls;
using System.Text.Json;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private ChordDiagram? libraryChord;private FretboardDiagram? libraryScale;
    private readonly Dictionary<string,int> libraryCounts=[];
    private NoteNotation SelectedNotation=>appSettings.Notation=="german"?NoteNotation.German:NoteNotation.English;
    private FrameworkElement BuildChords(string? id)
    {
        var panel=Stack();panel.Children.Add(Text("Akordy · hmat, prstoklad a poslech"));var search=new TextBox{Header="Hledat akord",MaxLength=128};var category=new ComboBox{Header="Kategorie",ItemsSource=new[]{"Všechny","Otevřené","Barré","Power chordy"},SelectedIndex=0};panel.Children.Add(search);panel.Children.Add(category);
        var entries=catalog!.Items("chords").ToArray();libraryCounts["chords"]=entries.Length;JsonElement selected=entries.FirstOrDefault(c=>c.GetProperty("id").GetString()==id);if(selected.ValueKind==JsonValueKind.Undefined)selected=entries[0];
        var list=new ListView{Header="Akordy",DisplayMemberPath="Title",SelectionMode=ListViewSelectionMode.Single};panel.Children.Add(list);var diagram=new ChordDiagram();libraryChord=diagram;panel.Children.Add(diagram);var details=Text("",true);panel.Children.Add(details);
        void Render(){diagram.ShowChord(selected,appSettings.Handedness=="left");details.Text="Diagram je pro standardní ladění E A D G B E. "+(CurrentTuning().Id!="standard"?"Aktuálně máš jiné ladění; tyto hmaty nejsou ověřené pro něj.":"Čísla uvnitř značek označují prsty, spojení barré, × ztlumenou strunu.");}
        void Filter(){string? key=category.SelectedIndex switch{1=>"open",2=>"barre",3=>"power",_=>null};var items=entries.Where(c=>(key is null || c.GetProperty("category").GetString()==key)&&c.GetProperty("name").GetString()!.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).Select(c=>new ListEntry(c.GetProperty("id").GetString()!,c.GetProperty("name").GetString()!,c.GetProperty("category").GetString()!)).ToArray();list.ItemsSource=items;list.SelectedItem=items.FirstOrDefault(e=>e.Id==selected.GetProperty("id").GetString())??items.FirstOrDefault();}
        list.SelectionChanged+=(_,_)=>{if(list.SelectedItem is ListEntry entry){selected=catalog.Find("chords",entry.Id)!.Value;Render();}};search.TextChanged+=(_,_)=>Filter();category.SelectionChanged+=(_,_)=>Filter();
        async Task Listen(bool arpeggio){if(CurrentTuning().Id!="standard"){ShowPersistenceError("Tento diagram používá standardní ladění. Přepni nejprve nastavení a fyzicky nalaď kytaru.");return;}await PlayTeachingAsync(TeachingPreview.Chord(selected,arpeggio:arpeggio));}
        var play=new Button{Content="Poslechnout akord"};play.Click+=async(_,_)=>await Listen(false);panel.Children.Add(play);var strings=new Button{Content="Poslechnout struny postupně"};strings.Click+=async(_,_)=>await Listen(true);panel.Children.Add(strings);var standard=new Button{Content="Použít standardní ladění"};standard.Click+=async(_,_)=>await UseLibraryTuningAsync("standard");panel.Children.Add(standard);Filter();Render();return Card(panel);
    }
    private FrameworkElement BuildScales(string? id)
    {
        var panel=Stack();panel.Children.Add(Text("Stupnice · tónina a polohy na krku"));var scales=catalog!.Items("scales").Select(s=>new ListEntry(s.GetProperty("id").GetString()!,s.GetProperty("name").GetString()!,"")).ToArray();libraryCounts["scales"]=scales.Length;var choose=new ComboBox{Header="Stupnice",ItemsSource=scales,DisplayMemberPath="Title"};var root=new ComboBox{Header="Tonika",ItemsSource=Enumerable.Range(0,12).Select(n=>Music.PitchClassName(n,SelectedNotation)).ToArray(),SelectedIndex=0};var position=new NumberBox{Header="První zobrazený pražec",Minimum=0,Maximum=12,Value=0,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};panel.Children.Add(choose);panel.Children.Add(root);panel.Children.Add(position);var board=new FretboardDiagram();libraryScale=board;panel.Children.Add(board);var details=Text("",true);panel.Children.Add(details);var clicked=Text("Vyber tón na hmatníku.",true);panel.Children.Add(clicked);
        JsonElement Selected()=>catalog.Find("scales",((ListEntry)choose.SelectedItem).Id)!.Value;
        void Render(){if(choose.SelectedItem is null || root.SelectedIndex<0 || !double.IsFinite(position.Value))return;var scale=Selected();var intervals=scale.GetProperty("intervals").EnumerateArray().Select(v=>v.GetInt32()).ToArray();board.SetTuning(CurrentTuning().Strings.Select(s=>s.Midi).ToArray(),intervals,root.SelectedIndex,appSettings.Handedness=="left",(int)Math.Clamp(position.Value,0,12),SelectedNotation);details.Text=CurrentTuning().Label+" · stupnice: "+string.Join(" · ",intervals.Select(n=>Music.PitchClassName(root.SelectedIndex+n,SelectedNotation)))+" · 1 označuje kořen; — je mimo stupnici.";}
        board.NoteSelected+=(_,_)=>{if(board.SelectedMidi is int midi)clicked.Text=NoteLabel(midi);};choose.SelectionChanged+=(_,_)=>Render();root.SelectionChanged+=(_,_)=>Render();position.ValueChanged+=(_,_)=>Render();
        var play=new Button{Content="Poslechnout stupnici nahoru a dolů"};play.Click+=async(_,_)=>{if(choose.SelectedItem is null)return;var up=LibraryPractice.ScaleNotes(Selected(),48+Math.Max(0,root.SelectedIndex));var sequence=up.Concat(up.Reverse().Skip(1)).ToArray();await PlayTeachingAsync(sequence.Select((m,i)=>new TeachingNote(i*.35,.3,m)).ToArray());};panel.Children.Add(play);var note=new Button{Content="Poslechnout vybraný tón"};note.Click+=async(_,_)=>{if(board.SelectedMidi is int midi)await PlayTeachingAsync([new(0,1,midi)]);};panel.Children.Add(note);var tune=new Button{Content="Změnit ladění"};tune.Click+=(_,_)=>Navigate(new("03"));panel.Children.Add(tune);choose.SelectedItem=scales.FirstOrDefault(s=>s.Id==id)??scales[0];Render();return Card(panel);
    }
    private async Task UseLibraryTuningAsync(string id)
    {
        if(!persistenceReady || closingWindow || deviceOperationRunning || backupOperationRunning)return;if(!AllTunings().Any(t=>t.Id==id))return;
        CancelPalm();surgicalListening=false;await StopRhythmUiAsync();appSettings=appSettings with{TuningId=id};SaveSettings();workspaceCache.Clear();RefreshTuningSelector();RebuildTunerStrings();ShowWorkspace();ShellNotice.IsOpen=true;ShellNotice.Severity=InfoBarSeverity.Informational;ShellNotice.Message="Nastavení ladění je změněné. Fyzické struny musíš naladit sám pomocí ladičky.";
    }
}
