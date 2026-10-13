// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private readonly ComboBox desktopLibrarySection = new()
    {
        ItemsSource = new[] { "Pracovní plocha", "Výuka a cvičení", "Moje soubory", "Hudební reference" },
        SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch
    };

    private void InitializeDesktopWorkbench()
    {
        ShellRoot.RowDefinitions.Insert(1, new() { Height = GridLength.Auto });
        Grid.SetRow(Navigation, 2);
        Grid.SetRow(desktopRoot, 2);
        Grid.SetRow(MediaPanel, 3);
        foreach (var child in ShellRoot.Children.OfType<Border>().Where(b => b != MediaPanel && Grid.GetRow(b) == 3))
            Grid.SetRow(child, 4);

        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        commands.Children.Add(new TextBlock
        {
            Text = "ROCKER", CharacterSpacing = 100, FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Style = (Style)Application.Current.Resources["VintageSectionTextStyle"], Margin = new(4, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center
        });
        void Command(string label, Symbol icon, string help, Action action)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            content.Children.Add(new SymbolIcon(icon) { Width = 16, Height = 16 });
            content.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Content = content, MinHeight = 32, Padding = new(9, 4, 9, 4) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, help);
            ToolTipService.SetToolTip(button, help);
            button.Click += (_, _) => action(); commands.Children.Add(button);
        }
        Command("Otevřít", Symbol.OpenFile, "Otevřít soubor · Ctrl+O", () => OpenFile_Click(this, new()));
        Command("Studio", Symbol.Audio, "Otevřít Studio", () => Navigate(new("15")));
        commands.Children.Add(new Border { Width = 1, Margin = new(8, 3, 8, 3), Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"] });
        Command("Ladička", Symbol.Target, "Ladička v samostatném okně", () => Navigate(new("03")));
        Command("Metronom", Symbol.Clock, "Metronom v samostatném okně", () => Navigate(new("04")));
        Command("Aparát", Symbol.Volume, "Aparát v samostatném okně", () => Navigate(new("13")));
        Command("Looper", Symbol.RepeatAll, "Looper v samostatném okně", () => Navigate(new("16")));
        Command("Live", Symbol.Play, "Otevřít Live · F11", () => SetFocusMode(true));
        var strip = new StackPanel { Spacing = 0 };
        strip.Children.Add(new ScrollViewer { Content = commands, HorizontalScrollMode = ScrollMode.Enabled, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        if (ShellNotice.Parent is Panel noticeParent) noticeParent.Children.Remove(ShellNotice);
        strip.Children.Add(ShellNotice);
        var frame = new Border
        {
            Child = strip, Padding = new(8, 5, 8, 5), Style = (Style)Application.Current.Resources["VintageWorkbenchStripStyle"]
        };
        Grid.SetRow(frame, 1); ShellRoot.Children.Add(frame);
        InitializeDesktopTransportStrip();
    }

    private void InitializeDesktopTransportStrip()
    {
        // Move the actual controls so event handlers, keyboard access and audio ownership stay intact.
        var previous = (StackPanel)MediaPanel.Child;
        MediaTransportRow.Children.Remove(MediaButtons);
        previous.Children.Remove(MediaSeekRow);
        previous.Children.Remove(PracticeRow);
        previous.Children.Remove(PlanButtons);
        previous.Children.Remove(PracticeRestNotice);
        MediaPanel.Child = null;

        var transport = new Grid { ColumnSpacing = 12, MinHeight = 44 };
        foreach (var width in new[] { GridLength.Auto, new GridLength(160), new GridLength(1, GridUnitType.Star), GridLength.Auto })
            transport.ColumnDefinitions.Add(new() { Width = width });
        MediaButtons.DefaultLabelPosition = CommandBarDefaultLabelPosition.Collapsed;
        MediaButtons.ClearValue(Grid.ColumnProperty);
        transport.Children.Add(MediaButtons);
        desktopSource.FontSize = 12; desktopSource.VerticalAlignment = VerticalAlignment.Center;
        desktopSource.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(desktopSource, 1); transport.Children.Add(desktopSource);
        Grid.SetColumn(MediaSeekRow, 2); transport.Children.Add(MediaSeekRow);
        var practice = new StackPanel { Spacing = 10, Width = 400 };
        practice.Children.Add(Text("Cvičení", true));
        practice.Children.Add(PracticeRow); practice.Children.Add(PlanButtons); practice.Children.Add(PracticeRestNotice);
        PracticeRow.ColumnDefinitions[0].Width = new(1, GridUnitType.Star);
        PracticeRow.ColumnDefinitions[1].Width = new(0);
        PracticeRow.Children.Remove(PracticeStatus); practice.Children.Add(PracticeStatus);
        var practiceButton = new Button { Content = new SymbolIcon(Symbol.Clock), MinHeight = 32, Flyout = new Flyout { Content = practice } };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(practiceButton, "Cvičení, čas a denní plán");
        ToolTipService.SetToolTip(practiceButton, "Cvičení, čas a denní plán");
        Grid.SetColumn(practiceButton, 3); transport.Children.Add(practiceButton);
        MediaPanel.Padding = new(10, 0, 10, 0); MediaPanel.Child = transport;
        transport.SizeChanged += (_, e) => transport.ColumnDefinitions[1].Width = new(e.NewSize.Width < 760 ? 0 : 160);
    }
}
