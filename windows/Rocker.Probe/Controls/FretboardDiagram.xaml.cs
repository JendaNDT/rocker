// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using Rocker.Domain;
using Windows.System;
namespace Rocker.Probe.Controls;
public sealed partial class FretboardDiagram : UserControl
{
    private readonly Dictionary<(int String,int Fret),Button> cells = [];
    private int selectedString, selectedFret;
    private bool leftHanded;private int firstFret;private NoteNotation notation;
    public int CellsCount=>cells.Count;
    public (int String,int Fret) SelectedPosition=>(selectedString,selectedFret);
    public int? SelectedMidi { get; private set; }
    public event EventHandler? NoteSelected;
    public FretboardDiagram() { InitializeComponent(); SetTuning([40,45,50,55,59,64]); }
    public void SetTuning(IReadOnlyList<int> tuning, IReadOnlyList<int>? scale = null, int rootPitch = 0, bool mirror = false,int startFret=0,NoteNotation noteNotation=NoteNotation.English,bool concealNotes=false)
    {
        if (tuning.Count != 6 || tuning.Any(note => note is < 0 or > 103)) throw new ArgumentException("Hmatník vyžaduje šest platných strun.");
        if(startFret is <0 or >12)throw new ArgumentOutOfRangeException(nameof(startFret));firstFret=startFret;notation=noteNotation;selectedFret=Math.Clamp(selectedFret,firstFret,firstFret+12);
        SelectedMidi = null; SelectedNote.Text = "Vyber tón. 1 označuje kořen stupnice.";
        leftHanded = mirror; cells.Clear(); Board.Children.Clear(); Board.RowDefinitions.Clear(); Board.ColumnDefinitions.Clear();
        for (int row = 0; row < 7; ++row) Board.RowDefinitions.Add(new() { Height = new(42) });
        for (int col = 0; col <= 13; ++col) Board.ColumnDefinitions.Add(new() { Width = new(58) });
        for(int fret=firstFret;fret<=firstFret+12;++fret){var label=new TextBlock{Text=fret.ToString(),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};Grid.SetRow(label,0);Grid.SetColumn(label,1+(mirror?12-(fret-firstFret):fret-firstFret));Board.Children.Add(label);}
        for(int str=0;str<6;++str){var label=new TextBlock{Text=$"{6-str} · {Music.MidiToLabel(tuning[str],notation)}",VerticalAlignment=VerticalAlignment.Center};Grid.SetRow(label,6-str);Board.Children.Add(label);}
        for (int str = 0; str < 6; ++str) for (int fret = firstFret; fret <= firstFret+12; ++fret)
        {
            int midi = tuning[str] + fret; var address = (String:str,Fret:fret);
            bool included = scale is null || MusicShapes.ScaleOffset(scale,rootPitch,midi%12) is not null;
            bool root = scale is not null && MusicShapes.NoteAtFret(midi,0) == ((rootPitch%12)+12)%12;
            var button = new Button { Content = concealNotes?(root?"1":fret==0?"0":"·"):Music.PitchClassName(midi,notation) + (root ? " 1" : ""), Margin = new(2), HorizontalAlignment = HorizontalAlignment.Stretch, IsTabStop = str == selectedString && fret == selectedFret };
            if(!included){button.Content="—";button.Opacity=.55;}
            if (root) button.Style = (Style)Application.Current.Resources["VintageRootButtonStyle"];
            AutomationProperties.SetName(button,$"Struna {6-str}, pražec {fret}"+(concealNotes?"":", "+Music.MidiToLabel(midi,notation)) + (scale is null ? "" : root ? ", kořen stupnice" : included ? ", ve stupnici" : ", mimo stupnici"));
            button.Tag=midi;button.Click += (_,_) => Select(address,midi);
            button.GotFocus += (_,_) => { selectedString = address.String; selectedFret = address.Fret; foreach (var cell in cells) cell.Value.IsTabStop = cell.Key == address; };
            Grid.SetRow(button,6-str); Grid.SetColumn(button,1+(mirror ? 12-(fret-firstFret) : fret-firstFret)); Board.Children.Add(button); cells.Add(address,button);
        }
    }
    public void SelectNote(int str,int fret){if(!cells.ContainsKey((str,fret)))throw new ArgumentOutOfRangeException(nameof(fret));Select((str,fret),int.Parse(cells[(str,fret)].Tag!.ToString()!,System.Globalization.CultureInfo.InvariantCulture));}
    private void Select((int String,int Fret) address, int midi)
    {
        selectedString = address.String; selectedFret = address.Fret; SelectedMidi = midi;foreach(var cell in cells)cell.Value.IsTabStop=cell.Key==address;
        SelectedNote.Text = $"Struna {6-selectedString} · pražec {selectedFret} · {Music.MidiToLabel(midi,notation)}";
        NoteSelected?.Invoke(this,EventArgs.Empty);
    }
    private void Board_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        int str = selectedString, fret = selectedFret;
        if (args.Key == VirtualKey.Left) fret += leftHanded ? 1 : -1;
        else if (args.Key == VirtualKey.Right) fret += leftHanded ? -1 : 1;
        else if (args.Key == VirtualKey.Up) ++str;
        else if (args.Key == VirtualKey.Down) --str;
        else return;
        args.Handled = true; selectedString = Math.Clamp(str,0,5); selectedFret = Math.Clamp(fret,firstFret,firstFret+12);
        cells[(selectedString,selectedFret)].Focus(FocusState.Keyboard);
    }
}
