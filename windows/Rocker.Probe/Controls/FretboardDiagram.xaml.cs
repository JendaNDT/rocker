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
    private bool leftHanded;
    public int? SelectedMidi { get; private set; }
    public event EventHandler? NoteSelected;
    public FretboardDiagram() { InitializeComponent(); SetTuning([40,45,50,55,59,64]); }
    public void SetTuning(IReadOnlyList<int> tuning, IReadOnlyList<int>? scale = null, int rootPitch = 0, bool mirror = false)
    {
        if (tuning.Count != 6 || tuning.Any(note => note is < 0 or > 103)) throw new ArgumentException("Hmatník vyžaduje šest platných strun.");
        SelectedMidi = null; SelectedNote.Text = "Vyber tón. 1 označuje kořen stupnice.";
        leftHanded = mirror; cells.Clear(); Board.Children.Clear(); Board.RowDefinitions.Clear(); Board.ColumnDefinitions.Clear();
        for (int row = 0; row < 6; ++row) Board.RowDefinitions.Add(new() { Height = new(42) });
        for (int col = 0; col <= 12; ++col) Board.ColumnDefinitions.Add(new() { Width = new(58) });
        for (int str = 0; str < 6; ++str) for (int fret = 0; fret <= 12; ++fret)
        {
            int midi = tuning[str] + fret; var address = (String:str,Fret:fret);
            bool included = scale is null || MusicShapes.ScaleOffset(scale,rootPitch,midi%12) is not null;
            bool root = scale is not null && MusicShapes.NoteAtFret(midi,0) == ((rootPitch%12)+12)%12;
            var button = new Button { Content = Music.PitchClassName(midi) + (root ? " 1" : ""), Margin = new(2), HorizontalAlignment = HorizontalAlignment.Stretch, IsTabStop = str == selectedString && fret == selectedFret };
            if (root) button.Style = (Style)Application.Current.Resources["VintageRootButtonStyle"];
            AutomationProperties.SetName(button,$"Struna {6-str}, pražec {fret}, {Music.MidiToLabel(midi)}" + (root ? ", kořen stupnice" : included ? ", ve stupnici" : ", mimo stupnici"));
            button.Click += (_,_) => Select(address,midi);
            button.GotFocus += (_,_) => { selectedString = address.String; selectedFret = address.Fret; foreach (var cell in cells) cell.Value.IsTabStop = cell.Key == address; };
            Grid.SetRow(button,5-str); Grid.SetColumn(button,mirror ? 12-fret : fret); Board.Children.Add(button); cells.Add(address,button);
        }
    }
    private void Select((int String,int Fret) address, int midi)
    {
        selectedString = address.String; selectedFret = address.Fret; SelectedMidi = midi;
        SelectedNote.Text = $"Struna {6-selectedString} · pražec {selectedFret} · {Music.MidiToLabel(midi)}";
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
        args.Handled = true; selectedString = Math.Clamp(str,0,5); selectedFret = Math.Clamp(fret,0,12);
        cells[(selectedString,selectedFret)].Focus(FocusState.Keyboard);
    }
}
