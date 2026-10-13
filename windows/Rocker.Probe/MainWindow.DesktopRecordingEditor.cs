// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Rocker.AppCore;
using Rocker.Probe.Desktop;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private DocumentSurface ComposeDesktopRecordingEditor(StackPanel source)
    {
        var owner = documentContext.Current;
        source.Children.Clear();
        if (recordingRangeA!.Parent is Panel ranges) ranges.Children.Clear();
        var body = new Grid { RowSpacing = 0 };
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        body.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = Text(playingRecording?.Name ?? "Nahrávka"); heading.FontSize = 18;
        recordingWaveformCaption = heading; heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold; heading.Margin = new(0, 0, 0, 12); body.Children.Add(heading);
        recordingWaveform!.Height = double.NaN; recordingWaveform.MinHeight = 180;
        var wave = new Border { Child = recordingWaveform, BorderThickness = new(1), BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"], Background = (Brush)Application.Current.Resources["VintagePanelMaterial"] };
        Grid.SetRow(wave, 1); body.Children.Add(wave);
        var ruler = new Canvas { Height = 30, IsHitTestVisible = false };
        Grid.SetRow(ruler, 2); body.Children.Add(ruler);
        ruler.SizeChanged += (_, e) =>
        {
            using var scope = documentContext.Enter(owner); if (!owner.IsAlive) return;
            ruler.Children.Clear(); double duration = playingRecording?.DurationSec ?? 0;
            int ticks = Math.Max(2, (int)(e.NewSize.Width / 90));
            for (int i = 0; i <= ticks; i++)
            {
                double x = e.NewSize.Width * i / ticks;
                var label = Text(FormatTime(duration * i / ticks), true); label.FontSize = 11;
                Canvas.SetLeft(label, Math.Clamp(x - 10, 0, Math.Max(0, e.NewSize.Width - 42))); Canvas.SetTop(label, 7); ruler.Children.Add(label);
            }
        };
        var properties = Stack(); properties.Children.Add(Text("ZNAČKY", true));
        var markers = new ListView { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single };
        foreach (var marker in playingRecording?.Markers ?? [])
            markers.Items.Add(new ListViewItem { Content = $"{FormatTime(marker.TimeSec)}  {marker.Label}", Tag = marker.TimeSec });
        markers.ItemClick += async (_, e) =>
        {
            using var scope = documentContext.Enter(owner); if (!owner.IsAlive || e.ClickedItem is not ListViewItem { Tag: double seconds }) return;
            try { await ClaimDesktopSourceAsync(owner); if (audio is not null) await audio.SeekAsync(seconds / currentSpeed); }
            catch (Exception error) { ShowPersistenceError(error.Message); }
        };
        properties.Children.Add(markers);
        if (playingRecording is { } value) properties.Children.Add(Text($"{FormatTime(value.DurationSec)} · {value.Markers.Count} značek\n" + value.RelativePath, true));
        var surface = new DocumentSurface(body, inspector: properties);
        foreach (var action in recordingWaveformActions)
        {
            string label = action.Content as string ?? ""; ToolTipService.SetToolTip(action, label);
            action.Content = label.StartsWith("Opakovat", StringComparison.Ordinal) ? "Smyčka A–B" : label.StartsWith("Vypnout", StringComparison.Ordinal) ? "Bez smyčky" : "Přidat značku";
            action.MinHeight = 32; surface.Toolbar.Children.Add(action);
        }
        var selection = new Grid { ColumnSpacing = 12 };
        selection.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        selection.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        recordingRangeA.Header = "Začátek A (s)"; recordingRangeB!.Header = "Konec B (s)";
        selection.Children.Add(recordingRangeA); Grid.SetColumn(recordingRangeB, 1); selection.Children.Add(recordingRangeB);
        surface.Footer.Children.Add(selection); surface.Footer.Children.Add(recordingWaveformStatus!);
        surface.Tag = "desktop-recording-editor";
        return surface;
    }
}
