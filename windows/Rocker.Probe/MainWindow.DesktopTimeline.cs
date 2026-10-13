// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private void DrawDesktopTimeline()
    {
        if (studioTimeline is null || studioProject is null) return;
        var owner = documentContext.Current;
        var project = studioProject;
        double end = project.Tracks.Select(track => track.OffsetFrames / (double)project.SampleRate +
            (track.SourceFrames is long frames && track.SourceSampleRate is int rate ? (frames - track.TrimStartFrames) / (double)rate : track.DurationSec)).DefaultIfEmpty(0).Max();
        double width = Math.Clamp(Math.Max(12, end * 1.08) * studioZoom, 300, 200000);
        double height = Math.Max(320, 32 + project.Tracks.Count * 78);
        studioTimeline.Width = width; studioTimeline.Height = height;
        var gridLine = (Style)Application.Current.Resources["VintageWorkbenchGridLineStyle"];
        double tickSeconds = Math.Max(1, Math.Ceiling(80 / studioZoom));
        // Bound the vector count even at the maximum project duration and zoom.
        tickSeconds = Math.Max(tickSeconds, Math.Ceiling(width / studioZoom / 300));
        for (double seconds = 0; seconds * studioZoom < width; seconds += tickSeconds)
        {
            double x = seconds * studioZoom;
            studioTimeline.Children.Add(new Line { X1 = x, X2 = x, Y1 = 28, Y2 = height, Style = gridLine, IsHitTestVisible = false });
            var label = new TextBlock { Text = FormatTime(seconds), FontSize = 11, Style = (Style)Application.Current.Resources["VintageSecondaryTextStyle"], IsHitTestVisible = false };
            Canvas.SetLeft(label, x + 4); Canvas.SetTop(label, 4); studioTimeline.Children.Add(label);
        }
        for (int i = 0; i < project.Tracks.Count; i++)
        {
            var track = project.Tracks[i]; double top = 32 + i * 78;
            studioTimeline.Children.Add(new Line { X1 = 0, X2 = width, Y1 = top + 72, Y2 = top + 72, Style = gridLine, IsHitTestVisible = false });
            double start = track.OffsetFrames / (double)project.SampleRate;
            double duration = track.SourceFrames is long frames && track.SourceSampleRate is int rate ? (frames - track.TrimStartFrames) / (double)rate : track.DurationSec;
            var text = Text(track.Name); text.TextTrimming = TextTrimming.CharacterEllipsis; text.TextWrapping = TextWrapping.NoWrap; text.FontSize = 12;
            var contents = new StackPanel { Spacing = 5 }; contents.Children.Add(text);
            contents.Children.Add(new TextBlock { Text = $"{FormatTime(start)} · {duration:0.##} s" + (track.Muted ? " · ztlumeno" : track.Solo ? " · sólo" : ""), FontSize = 10, Style = (Style)Application.Current.Resources["VintageSecondaryTextStyle"] });
            var region = new Border
            {
                Width = Math.Max(3, duration * studioZoom), Height = 64, Padding = new(7, 5, 7, 5),
                Style = (Style)Application.Current.Resources["VintageWorkbenchRegionStyle"],
                BorderThickness = new(3, 1, 1, 1), CornerRadius = new(2), Child = contents, Opacity = track.Muted ? .5 : 1
            };
            ToolTipService.SetToolTip(region, $"{track.Name}\nZačátek {start:0.###} s · délka {duration:0.###} s\nKliknutím vybereš stopu.");
            region.PointerPressed += (_, e) => { using var scope = documentContext.Enter(owner); if (owner.IsAlive && studioTrackList is not null) studioTrackList.SelectedItem = track; e.Handled = true; };
            Canvas.SetLeft(region, start * studioZoom); Canvas.SetTop(region, top); studioTimeline.Children.Add(region);
        }
    }
}
