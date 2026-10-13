// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media;

namespace Rocker.Probe.Controls;

/// <summary>Native, scalable cabinet ornamentation; the contained controls retain their peers.</summary>
public sealed class VintageHousing : Grid
{
    public bool HasSpeakerGrille { get; }
    internal static Style Material(string name) => (Style)Application.Current.Resources[name];
    public VintageHousing(UIElement content, string? plaque = null, bool speaker = false, bool compact = false, bool instrument = false)
    {
        HasSpeakerGrille = speaker;
        double cheek = compact ? 8 : 14;
        ColumnDefinitions.Add(new() { Width = new(cheek) });
        ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new() { Width = new(cheek) });
        ColumnSpacing = 3;
        foreach (int column in new[] { 0, 2 })
        {
            var grain = new Canvas { IsHitTestVisible = false };
            var wood = new Border { Style = Material("VintageWoodStyle"), Child = grain };
            Decorative(wood); Grid.SetColumn(wood, column); Children.Add(wood);
            wood.SizeChanged += (_, args) => DrawGrain(grain, cheek, args.NewSize.Height);
        }
        var panel = new StackPanel { Spacing = 12 };
        if (plaque is not null) panel.Children.Add(Plaque(plaque));
        if (speaker) panel.Children.Add(Speaker());
        panel.Children.Add(content);
        var lining = new Grid();
        var texture = new Canvas(); Decorative(texture);
        texture.SizeChanged += (_, args) =>
        {
            texture.Children.Clear();
            double width = args.NewSize.Width, height = args.NewSize.Height;
            double step = Math.Max(20, Math.Sqrt(Math.Max(0, width * height) / 500));
            for (double y = 2; y < height; y += step)
                for (double x = 2; x < width - 4; x += step)
                    texture.Children.Add(new Line { X1 = x, Y1 = y, X2 = x + 3, Y2 = Math.Min(height, y + 2), Style = Material("VintageGrainStyle"), Opacity = .55 });
        };
        lining.Children.Add(texture); lining.Children.Add(panel);
        var face = new Border { Child = lining, Style = Material(instrument ? "VintageInstrumentCardStyle" : "VintageCardStyle") };
        Grid.SetColumn(face, 1); Children.Add(face);
        // Screws float inside the existing padding and never consume content space.
        foreach (var (horizontal, vertical) in new[] { (HorizontalAlignment.Left, VerticalAlignment.Top), (HorizontalAlignment.Right, VerticalAlignment.Top), (HorizontalAlignment.Left, VerticalAlignment.Bottom), (HorizontalAlignment.Right, VerticalAlignment.Bottom) })
        {
            var screw = new Canvas { Width = 8, Height = 8, HorizontalAlignment = horizontal, VerticalAlignment = vertical, Margin = new(5), IsHitTestVisible = false };
            screw.Children.Add(new Ellipse { Width = 8, Height = 8, Style = Material("VintageDialFaceStyle") });
            screw.Children.Add(new Line { X1 = 2, Y1 = 6, X2 = 6, Y2 = 2, Style = Material("VintageDialTickStyle") });
            Decorative(screw); Grid.SetColumn(screw, 1); Children.Add(screw);
        }
    }
    internal static void Decorative(UIElement element)
    {
        element.IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(element, AccessibilityView.Raw);
    }
    public static Border Plaque(string label)
    {
        var text = new TextBlock { Text = label, Style = Material("VintagePlaqueTextStyle"), TextWrapping = TextWrapping.Wrap };
        return new() { Child = text, Style = Material("VintagePlaqueStyle"), HorizontalAlignment = HorizontalAlignment.Left };
    }
    private static void DrawGrain(Canvas canvas, double width, double height)
    {
        canvas.Children.Clear();
        // Bounded shape count, deterministic vector grain, no random animation or bitmap scaling.
        for (int i = 1; i <= 5; ++i)
        {
            double x = width * i / 6;
            for (int j = 0; j < 8; ++j)
            {
                double y = height * j / 8;
                canvas.Children.Add(new Line { X1 = x, Y1 = y, X2 = x + Math.Sin(j + i) * 1.2, Y2 = height * (j + 1) / 8, Style = Material("VintageGrainStyle") });
            }
        }
    }
    private static FrameworkElement Speaker()
    {
        var canvas = new Canvas { Height = 84, IsHitTestVisible = false };
        canvas.SizeChanged += (_, args) =>
        {
            canvas.Children.Clear(); double width = args.NewSize.Width;
            for (double x = 3; x < width; x += 7)
                canvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = 2, Y2 = 82, Style = Material("VintageWeaveStyle") });
            for (double y = 3; y < 84; y += 5)
                canvas.Children.Add(new Line { X1 = 2, X2 = Math.Max(2, width - 2), Y1 = y, Y2 = y, Style = Material("VintageWeaveStyle"), Opacity = .6 });
        };
        var grille = new Border { Style = Material("VintageGrilleStyle"), Child = canvas };
        var grid = new Grid(); grid.Children.Add(grille);
        var badge = Plaque("ROCKER  /  GUITAR STUDIO");
        badge.HorizontalAlignment = HorizontalAlignment.Center; badge.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(badge); Decorative(grid); return grid;
    }
    public static FrameworkElement PowerSwitch(CheckBox control)
    {
        string label = control.Content as string ?? "Aparát";
        AutomationProperties.SetName(control, label);
        var lamp = new Ellipse { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center };
        var lever = new Line { X1 = 13, Y1 = 20, X2 = 13, Y2 = 7, Style = Material("VintageDialPointerStyle") };
        var socket = new Canvas { Width = 26, Height = 32, VerticalAlignment = VerticalAlignment.Center };
        socket.Children.Add(new Ellipse { Width = 20, Height = 20, Style = Material("VintageDialFaceStyle") });
        Canvas.SetLeft(socket.Children[0], 3); Canvas.SetTop(socket.Children[0], 10); socket.Children.Add(lever);
        var decoration = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        decoration.Children.Add(socket); decoration.Children.Add(lamp); Decorative(decoration);
        void Refresh() { bool enabled = control.IsChecked == true; lamp.Style = Material(enabled ? "VintageLampOnStyle" : "VintageLampOffStyle"); lever.Y2 = enabled ? 7 : 30; }
        control.Checked += (_, _) => Refresh(); control.Unchecked += (_, _) => Refresh(); control.Indeterminate += (_, _) => Refresh(); Refresh();
        // The whole ornament is checkbox content: clicking the lever or lamp
        // reaches the same native checkbox, rather than creating a dead control.
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        content.Children.Add(decoration);
        content.Children.Add(new TextBlock { Text = label, Style = Material("VintageBodyTextStyle"), VerticalAlignment = VerticalAlignment.Center });
        control.Content = content;
        var row = new Grid(); row.Children.Add(control); return row;
    }
}
