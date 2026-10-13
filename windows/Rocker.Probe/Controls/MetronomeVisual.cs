// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
using Rocker.Domain;
using Rocker.Interop;

namespace Rocker.Probe.Controls;

/// <summary>A passive instrument driven by snapshots of the shared native audio clock.</summary>
public sealed class MetronomeVisual : UserControl
{
    private readonly Windows.UI.ViewManagement.UISettings settings = new();
    private readonly StackPanel layout = new() { Spacing = 6 };
    private readonly Grid lamps = new() { ColumnSpacing = 4, RowSpacing = 4 };
    private readonly List<Border> beatFaces = [];
    private readonly TextBlock tempo = new() { FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    private readonly Viewbox pendulum;
    private readonly Line arm;
    private readonly Border weight;
    private readonly RotateTransform rotation = new() { CenterX = 150, CenterY = 133 };
    private string mode = "dots";
    private readonly bool compact;
    public int ActiveBeat { get; private set; } = -1;
    public int BeatCount => beatFaces.Count;
    public double Angle => rotation.Angle;
    public string StatusText => status.Text;
    public bool PendulumVisible => pendulum.Visibility == Visibility.Visible;
    public bool Silent { get; private set; }

    public MetronomeVisual(bool compact = false)
    {
        this.compact = compact;
        VerticalAlignment = VerticalAlignment.Top;
        var housing = new Border { Style = VintageHousing.Material("VintageInstrumentCardStyle"), Padding = new(compact ? 8 : 16) };
        tempo.Style = status.Style = VintageHousing.Material("VintageInstrumentTextStyle");
        if (!compact) layout.Children.Add(tempo);
        var face = new Canvas { Width = 300, Height = 152 };
        VintageHousing.Decorative(face);
        for (int i = -3; i <= 3; ++i)
        {
            double a = i * 10 * Math.PI / 180;
            face.Children.Add(new Line { X1 = 150 + Math.Sin(a) * 110, Y1 = 133 - Math.Cos(a) * 110, X2 = 150 + Math.Sin(a) * 119, Y2 = 133 - Math.Cos(a) * 119, Style = VintageHousing.Material("VintageInstrumentLineStyle") });
        }
        foreach (var (label, y) in new[] { ("40", 32d), ("120", 69d), ("240", 112d) })
        {
            var text = new TextBlock { Text = label, FontSize = 11, Style = VintageHousing.Material("VintageInstrumentTextStyle") };
            Canvas.SetLeft(text, 205); Canvas.SetTop(text, y); face.Children.Add(text);
        }
        var stem = new Canvas { Width = 300, Height = 152, RenderTransform = rotation };
        arm = new Line { X1 = 150, Y1 = 133, X2 = 150, Y2 = 26, Style = VintageHousing.Material("VintageDialPointerStyle"), StrokeThickness = 4 };
        // A dark ridge gives the copper arm a readable outline on the cream face.
        stem.Children.Add(new Line { X1 = 150, Y1 = 133, X2 = 150, Y2 = 26, Style = VintageHousing.Material("VintageInstrumentLineStyle"), StrokeThickness = 7 });
        stem.Children.Add(arm);
        weight = new Border { Width = 30, Height = 16, Style = VintageHousing.Material("VintagePlaqueStyle"), Padding = new(0) };
        Canvas.SetLeft(weight, 135); Canvas.SetTop(weight, 61); stem.Children.Add(weight); face.Children.Add(stem);
        var pivot = new Ellipse { Width = 16, Height = 16, Style = VintageHousing.Material("VintageDialFaceStyle") };
        Canvas.SetLeft(pivot, 142); Canvas.SetTop(pivot, 125); face.Children.Add(pivot);
        pendulum = new Viewbox { Child = face, Height = compact ? 58 : 152, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch };
        layout.Children.Add(pendulum); layout.Children.Add(lamps); layout.Children.Add(status); housing.Child = layout; Content = housing;
        AutomationProperties.SetName(this, "Vizualizace metronomu");
    }
    public void Configure(MetronomeConfig config)
    {
        mode = config.VisualMode == "dots" ? "dots" : "pendulum";
        pendulum.Visibility = mode == "pendulum" ? Visibility.Visible : Visibility.Collapsed;
        pendulum.Height = compact ? 58 : config.BeatsPerBar > 8 ? 114 : 152;
        if (beatFaces.Count != config.BeatsPerBar)
        {
            lamps.Children.Clear(); lamps.ColumnDefinitions.Clear(); lamps.RowDefinitions.Clear(); beatFaces.Clear();
            int columns = Math.Min(8, config.BeatsPerBar);
            for (int i = 0; i < columns; ++i) lamps.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            for (int i = 0; i < (config.BeatsPerBar + columns - 1) / columns; ++i) lamps.RowDefinitions.Add(new() { Height = GridLength.Auto });
            for (int i = 0; i < config.BeatsPerBar; ++i)
            {
                var label = new TextBlock { Text = (i + 1).ToString(), FontSize = compact ? 13 : 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var lamp = new Border { Child = label, Height = compact ? 24 : 38, CornerRadius = new(6) };
                Grid.SetColumn(lamp, i % columns); Grid.SetRow(lamp, i / columns); lamps.Children.Add(lamp); beatFaces.Add(lamp);
            }
        }
        tempo.Text = config.Bpm + " BPM";
        // Position expresses the selected tempo; it is not a physical oscillator model.
        Canvas.SetTop(weight, 29 + 80 * (config.Bpm - 40) / 200.0);
        RefreshLamps(config);
    }
    public void Render(MetronomeSnapshot snapshot, ClockSnapshot clock, MetronomeConfig config, bool? allowMotion = null, bool awaitingStart = false)
    {
        Configure(config);
        bool running = snapshot.Running && (snapshot.Enabled || awaitingStart) && clock.Running;
        bool valid = snapshot.Enabled && double.IsFinite(clock.BeatPhase) && clock.BeatPhase >= 0 && clock.BeatPhase <= 1 && snapshot.Beat >= 0 && snapshot.Beat < BeatCount;
        int currentBpm = running && double.IsFinite(snapshot.Bpm) && snapshot.Bpm is >= 40 and <= 240 ? Music.ClampBpm(snapshot.Bpm) : config.Bpm;
        tempo.Text = currentBpm + " BPM"; Canvas.SetTop(weight, 29 + 80 * (currentBpm - 40) / 200.0);
        ActiveBeat = running && valid ? snapshot.Beat : -1;
        Silent = running && snapshot.SilentBar;
        bool motion = allowMotion ?? settings.AnimationsEnabled;
        // Absolute clock parity remains continuous across odd meters and count-in bars.
        rotation.Angle = ActiveBeat >= 0 && motion ? 28 * ((clock.Beat & 1) == 0 ? -1 : 1) * Math.Cos(Math.PI * clock.BeatPhase) : 0;
        status.Text = !running ? "Zastaveno" : !valid ? "Čekám na první dobu…" :
            snapshot.CountIn ? $"Odpočet · {Math.Max(1, -snapshot.Bar)} taktů · doba {ActiveBeat + 1}" :
            snapshot.SilentBar ? $"Tichý takt · doba {ActiveBeat + 1} / {BeatCount}" :
            $"Doba {ActiveBeat + 1} / {BeatCount}" + (config.AccentPattern[ActiveBeat] == "muted" ? " · bez kliku" : config.AccentPattern[ActiveBeat] == "accent" ? " · důraz" : "");
        if (!motion && PendulumVisible) status.Text += " · bez animace";
        RefreshLamps(config);
        AutomationProperties.SetHelpText(this, status.Text);
    }
    private void RefreshLamps(MetronomeConfig config)
    {
        for (int i = 0; i < beatFaces.Count; ++i)
        {
            bool active = i == ActiveBeat;
            beatFaces[i].Style = VintageHousing.Material(active ? "VintageBeatActiveStyle" : "VintageBeatIdleStyle");
            // Keep labels fully opaque, including Windows high contrast. Silence is explicit text.
            beatFaces[i].Opacity = 1;
            ((TextBlock)beatFaces[i].Child).Style = VintageHousing.Material(active ? "VintageAccentTextStyle" : "VintageInstrumentTextStyle");
        }
    }
}
