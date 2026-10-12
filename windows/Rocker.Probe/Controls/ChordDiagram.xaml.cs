// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Automation;
using System.Text.Json;
namespace Rocker.Probe.Controls;
public sealed partial class ChordDiagram : UserControl
{
    public ChordDiagram() { InitializeComponent(); }
    public void ShowChord(JsonElement chord, bool leftHanded = false)
    {
        Diagram.Children.Clear(); ChordTitle.Text = chord.GetProperty("name").GetString()!;
        var frets = chord.GetProperty("frets").EnumerateArray().ToArray();
        var fingers = chord.GetProperty("fingers").EnumerateArray().ToArray(); int baseFret = chord.GetProperty("baseFret").GetInt32();
        double X(int index) => 20 + (leftHanded ? 5 - index : index) * 32;
        void Line(double x1,double y1,double x2,double y2) => Diagram.Children.Add(new Line { X1=x1,Y1=y1,X2=x2,Y2=y2,Style=(Style)Application.Current.Resources["VintageInstrumentLineStyle"] });
        for (int i = 0; i < 6; ++i) Line(X(i),35,X(i),195);
        for (int i = 0; i <= 4; ++i) Line(20,35+i*40,180,35+i*40);
        if(baseFret==1)Diagram.Children.Add(new Line{X1=20,X2=180,Y1=35,Y2=35,Style=(Style)Application.Current.Resources["VintageInstrumentLineStyle"],StrokeThickness=4});
        for(int i=0;i<6;++i){var stringLabel=new TextBlock{Text=(6-i).ToString(),FontSize=11,Width=24,TextAlignment=TextAlignment.Center,Style=(Style)Application.Current.Resources["VintageInstrumentTextStyle"]};Canvas.SetLeft(stringLabel,X(i)-12);Canvas.SetTop(stringLabel,207);Diagram.Children.Add(stringLabel);}
        var barre = chord.GetProperty("barre");
        if (barre.ValueKind == JsonValueKind.Object)
        {
            double y = 35 + (barre.GetProperty("fret").GetInt32()-baseFret+0.5)*40;
            Diagram.Children.Add(new Line { X1 = X(barre.GetProperty("fromString").GetInt32()), X2 = X(barre.GetProperty("toString").GetInt32()), Y1=y, Y2=y, Style=(Style)Application.Current.Resources["VintageBarreStyle"] });
        }
        var descriptions = new List<string>();
        for (int i = 0; i < 6; ++i)
        {
            bool muted = frets[i].ValueKind == JsonValueKind.String;
            int fret = muted ? -1 : frets[i].GetInt32();
            var label = new TextBlock { Text = muted ? "×" : fret == 0 ? "0" : "", Style=(Style)Application.Current.Resources["VintageInstrumentTextStyle"] };
            Canvas.SetLeft(label,X(i)-4); Canvas.SetTop(label,8); Diagram.Children.Add(label);
            descriptions.Add($"Struna {6-i}: " + (muted ? "ztlumená" : fret == 0 ? "prázdná" : $"pražec {fret}"));
            if (fret <= 0) continue;
            double y = 35 + (fret-baseFret+0.5)*40;
            var dot = new Ellipse { Width=23,Height=23,Style=(Style)Application.Current.Resources["VintageFretDotStyle"] };
            Canvas.SetLeft(dot,X(i)-11.5); Canvas.SetTop(dot,y-11.5); Diagram.Children.Add(dot);
            if (fingers[i].ValueKind == JsonValueKind.Number)
            {
                var finger = new TextBlock { Text=fingers[i].GetInt32().ToString(),Width=23,TextAlignment=TextAlignment.Center,FontSize=12,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Style=(Style)Application.Current.Resources["VintageAccentTextStyle"] };
                Canvas.SetLeft(finger,X(i)-11.5); Canvas.SetTop(finger,y-9); Diagram.Children.Add(finger);
            }
        }
        Description.Text = (leftHanded ? "Pro leváka. " : "") + $"Výchozí pražec {baseFret}. " + string.Join("; ",descriptions);
        AutomationProperties.SetName(Diagram,ChordTitle.Text + ". " + Description.Text);
    }
}
