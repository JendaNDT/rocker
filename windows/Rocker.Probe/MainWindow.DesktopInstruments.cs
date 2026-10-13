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
                ToolTipService.SetToolTip(button,label);
                if (id == "16")
                {
                    button.Content=label switch
                    {
                        "Nahrát první smyčku"=>"Nahrát", "Ukončit první smyčku a opakovat"=>"Uzavřít smyčku",
                        "Přidat další vrstvu na začátku smyčky"=>"Přidat vrstvu", "Pozastavit Looper"=>"Pauza",
                        "Pokračovat v opakování"=>"Pokračovat", _=>label
                    };
                    if(label.StartsWith("Nahrát",StringComparison.Ordinal) || label.StartsWith("Ukončit",StringComparison.Ordinal) || label.StartsWith("Přidat",StringComparison.Ordinal) || label.StartsWith("Pozastavit",StringComparison.Ordinal) || label.StartsWith("Pokračovat",StringComparison.Ordinal))surface.Toolbar.Children.Add(button);
                    else properties.Children.Add(button);
                }
                else properties.Children.Add(button);
            }
            else if (id == "04" && child is StackPanel transport && transport.Children.Count==1 && transport.Children[0] is StackPanel buttons)
            { transport.Children.Remove(buttons); surface.Toolbar.Children.Add(buttons); }
            else if (child is StackPanel { Orientation: Orientation.Horizontal } row)
                surface.Toolbar.Children.Add(row);
            else if (id == "13" && ReferenceEquals(child,ampPresetsBox))
            { ampPresetsBox!.Header=null;ampPresetsBox.Width=220;surface.Toolbar.Children.Add(ampPresetsBox); }
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
