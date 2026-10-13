// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Rocker.Domain;
namespace Rocker.Probe.Desktop;

public sealed class DesktopWindow : Window
{
    public string Id { get; } = "window-"+Guid.NewGuid().ToString("N");
    public Grid Root { get; } = new() { RowSpacing = 0 };
    public MenuBar Menu { get; }=new();
    public TextBlock Status { get; }=new(){FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new(12,4,12,4)};
    public TabView Tabs { get; } = new() { IsAddTabButtonVisible = false, CanDragTabs = true, CanReorderTabs = true };
    public bool ExitApproved { get; set; }
    public DesktopPixelBounds NormalBounds { get; private set; }=new(40,40,920,720);
    public void RememberBounds(DesktopPixelBounds bounds)=>NormalBounds=bounds;
    public DesktopWindow(string title)
    {
        Title=title;
        Root.Style=(Style)Application.Current.Resources["VintageDesktopHostStyle"];
        Root.RowDefinitions.Add(new(){Height=GridLength.Auto});Root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});Root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        Root.Children.Add(Menu);Grid.SetRow(Tabs,1);Root.Children.Add(Tabs);Grid.SetRow(Status,2);Root.Children.Add(Status); Content=Root;
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","Rocker.ico"));
        AppWindow.Resize(new(920,720));
        AppWindow.Changed+=(_,_)=>{if(AppWindow.Presenter is OverlappedPresenter{State:OverlappedPresenterState.Restored})NormalBounds=new(AppWindow.Position.X,AppWindow.Position.Y,AppWindow.Size.Width,AppWindow.Size.Height);};
    }
}
