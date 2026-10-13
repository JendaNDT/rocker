// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.AppCore;
using Rocker.Domain;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private ScrollViewer? desktopDeviceScroll;
    private FrameworkElement BuildDesktopSettingsContent(DocumentSession owner,NavigationTarget target)
    {
        var grid=new Grid{Padding=new Thickness(12),ColumnSpacing=16,Tag="desktop-settings"};grid.ColumnDefinitions.Add(new(){Width=new(180)});grid.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var sidebar=new Grid{RowSpacing=8};sidebar.RowDefinitions.Add(new(){Height=GridLength.Auto});sidebar.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var search=new TextBox{PlaceholderText="Hledat v nastavení",MaxLength=128};sidebar.Children.Add(search);
        var entries=new[]{new ListEntry("28","Obecné a vzhled",""),new("29","Zvuková zařízení",""),new("26","Moje kytara",""),new("30","Zálohy a soubory","")};
        var list=new ListView{ItemsSource=entries,DisplayMemberPath="Title",SelectionMode=ListViewSelectionMode.Single};Grid.SetRow(list,1);sidebar.Children.Add(list);grid.Children.Add(sidebar);
        list.SelectedItem=entries.First(e=>e.Id==target.ViewId);
        list.SelectionChanged+=(_,_)=>{using var scope=documentContext.Enter(owner);if(owner.IsAlive && list.SelectedItem is ListEntry selected && selected.Id!=owner.Target.ViewId)Navigate(new(selected.Id));};
        search.TextChanged+=(_,_)=>list.ItemsSource=entries.Where(e=>e.Title.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).ToArray();
        if(target.ViewId=="29" && desktopDeviceScroll is not null)desktopDeviceScroll.Content=null;
        FrameworkElement page=target.ViewId=="29"?TakeDevicePanel():BuildWorkspace(target);
        var content=new ScrollViewer{Content=page,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetColumn(content,1);grid.Children.Add(content);
        if(target.ViewId=="29")desktopDeviceScroll=content;
        grid.SizeChanged+=(_,e)=>grid.ColumnDefinitions[0].Width=new(e.NewSize.Width<650?140:180);
        LabelNativeControls(grid);return grid;
    }
}
