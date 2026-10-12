// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
namespace Rocker.Probe.Controls;
public sealed partial class LevelMeter : UserControl
{
    public string Label {get=>MeterLabel.Text;set{MeterLabel.Text=value;AutomationProperties.SetName(Meter,value);}}
    public double Level { get; private set; }
    public bool HasSignal { get; private set; }
    private readonly List<Microsoft.UI.Xaml.Shapes.Rectangle> segments=[];
    public LevelMeter()
    {
        InitializeComponent();
        for(int i=0;i<24;++i){Segments.ColumnDefinitions.Add(new(){Width=new Microsoft.UI.Xaml.GridLength(1,Microsoft.UI.Xaml.GridUnitType.Star)});var segment=new Microsoft.UI.Xaml.Shapes.Rectangle{RadiusX=1,RadiusY=1};segment.Style=(Microsoft.UI.Xaml.Style)Application.Current.Resources[i<18?"VintageMeterGreenStyle":i<22?"VintageMeterAmberStyle":"VintageMeterRedStyle"];Grid.SetColumn(segment,i);Segments.Children.Add(segment);segments.Add(segment);}
        SetLevel(0,false);
    }
    public void SetLevel(double value, bool hasSignal, bool demonstration = false)
    {
        HasSignal = hasSignal && double.IsFinite(value) && value >= 0;
        Level = HasSignal ? Math.Clamp(value,0,1) : 0;
        Meter.Value = Level;
        double db=Level<=1e-6?-120:20*Math.Log10(Level);
        int lit=HasSignal?(int)Math.Ceiling(Math.Clamp((db+60)/60,0,1)*24):0;
        for(int i=0;i<segments.Count;++i)segments[i].Opacity=i<lit?1:0.18;
        string reading = !HasSignal ? "Bez signálu" : Level <= 1e-6 ? "Ticho" : $"{20 * Math.Log10(Level):0.0} dBFS";
        if (HasSignal && Level >= 1) reading += " · přebuzení";
        Reading.Text = demonstration ? "Ukázkový signál · " + reading : reading;
        AutomationProperties.SetName(Meter,demonstration ? "Ukázková úroveň" : Label);
        AutomationProperties.SetHelpText(Meter,Reading.Text);
    }
}
