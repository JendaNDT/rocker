// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Rocker.Probe.Desktop;

public sealed class DesktopWindow : Window
{
    public Grid Root { get; } = new() { RowSpacing = 0 };
    public TabView Tabs { get; } = new() { IsAddTabButtonVisible = false, CanDragTabs = true, CanReorderTabs = true };
    public bool ExitApproved { get; set; }
    public DesktopWindow(string title)
    {
        Title=title;
        Root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        Root.Children.Add(Tabs); Content=Root;
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","Rocker.ico"));
        AppWindow.Resize(new(920,720));
    }
}
