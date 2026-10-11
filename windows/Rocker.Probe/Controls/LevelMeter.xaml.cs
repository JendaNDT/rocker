// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
namespace Rocker.Probe.Controls;
public sealed partial class LevelMeter : UserControl
{
    public double Level { get; private set; }
    public bool HasSignal { get; private set; }
    public LevelMeter() { InitializeComponent(); SetLevel(0,false); }
    public void SetLevel(double value, bool hasSignal, bool demonstration = false)
    {
        HasSignal = hasSignal && double.IsFinite(value) && value >= 0;
        Level = HasSignal ? Math.Clamp(value,0,1) : 0;
        Meter.Value = Level;
        string reading = !HasSignal ? "Bez signálu" : Level <= 1e-6 ? "Ticho" : $"{20 * Math.Log10(Level):0.0} dBFS";
        if (HasSignal && Level >= 1) reading += " · přebuzení";
        Reading.Text = demonstration ? "Ukázkový signál · " + reading : reading;
        AutomationProperties.SetName(Meter,demonstration ? "Ukázková úroveň" : "Úroveň vstupu");
        AutomationProperties.SetHelpText(Meter,Reading.Text);
    }
}
