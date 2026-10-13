// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Probe.Controls;
using Rocker.Probe.Desktop;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private FrameworkElement ComposeDesktopInstrument(string id, FrameworkElement page)
    {
        StackPanel? source = page is VintageHousing housing ? housing.ReleaseContent() as StackPanel : page is Border { Child: StackPanel panel } ? panel : page as StackPanel;
        if (source is null) return page;
        if (page is Border border) border.Child = null;
        var children = source.Children.ToArray(); source.Children.Clear();
        var main = Stack(); var properties = Stack(); var help = Stack();
        var surface = new DocumentSurface(new ScrollViewer { Content = main, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, inspector: properties);
        foreach (var child in children)
        {
            if (id == "04" && child is Grid columns)
            {
                // The pendulum and BPM used to stack below each other in the narrow tool pane.
                var parts = columns.Children.ToArray(); columns.Children.Clear();
                foreach (var part in parts) { part.ClearValue(Grid.ColumnProperty); part.ClearValue(Grid.RowProperty); }
                if (parts.Length > 0) main.Children.Add(new VintageHousing(parts[0], "RHYTHM MASTER", compact: true));
                if (parts.Length > 1) properties.Children.Add(parts[1]);
            }
            else if (child is Button button)
            {
                button.MinHeight = 32;
                string label = button.Content as string ?? "";
                if (id == "16" && label.StartsWith("Export", StringComparison.Ordinal)) properties.Children.Add(button);
                else surface.Toolbar.Children.Add(button);
            }
            else if (child is StackPanel { Orientation: Orientation.Horizontal } row)
                surface.Toolbar.Children.Add(row);
            else if (child is ComboBox or NumberBox or Slider or CheckBox or Expander)
                properties.Children.Add(child);
            else if (child is TextBlock text)
            {
                if (ReferenceEquals(text, tunerStatus) || ReferenceEquals(text, looperReading) || ReferenceEquals(text, ampStatus)) surface.Footer.Children.Add(text);
                else if (text.Text.Length > 130) help.Children.Add(text);
                else if (main.Children.Count > 0 || id == "16") main.Children.Add(text);
            }
            else if (id == "13" && child is Grid power && power.Children.OfType<CheckBox>().Any())
                surface.Toolbar.Children.Add(child);
            else main.Children.Add(child);
        }
        if (help.Children.Count > 0) properties.Children.Add(new Expander { Header = "Podrobnosti", Content = help, HorizontalAlignment = HorizontalAlignment.Stretch });
        surface.Tag = "desktop-instrument";
        return surface;
    }
}
