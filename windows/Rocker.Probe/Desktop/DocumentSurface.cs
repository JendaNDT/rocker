// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Rocker.Probe.Desktop;

// A bounded document layout: its toolbar remains visible while each pane scrolls independently.
public sealed class DocumentSurface : Grid
{
    public StackPanel Toolbar { get; }=new(){Orientation=Orientation.Horizontal,Spacing=6};
    public Grid Body { get; }=new(){ColumnSpacing=8};
    public StackPanel Details { get; }=new(){Spacing=8};
    public StackPanel Footer { get; }=new(){Spacing=6};
    public DocumentSurface(FrameworkElement main,FrameworkElement? outline=null,FrameworkElement? inspector=null)
    {
        RowSpacing=8;Padding=new(12);
        RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});RowDefinitions.Add(new(){Height=GridLength.Auto});
        Children.Add(new ScrollViewer{Content=Toolbar,HorizontalScrollMode=ScrollMode.Enabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});
        Grid.SetRow(Body,1);Children.Add(Body);
        ScrollViewer? outlineScroll=null,inspectorScroll=null;
        if(outline is not null){Body.ColumnDefinitions.Add(new(){Width=new(190)});outlineScroll=new(){Content=outline,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Body.Children.Add(outlineScroll);}
        int center=Body.ColumnDefinitions.Count;Body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});Grid.SetColumn(main,center);Body.Children.Add(main);
        if(inspector is not null){Body.ColumnDefinitions.Add(new(){Width=new(240)});inspectorScroll=new(){Content=inspector,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetColumn(inspectorScroll,center+1);Body.Children.Add(inspectorScroll);}
        Button? PaneButton(string title,ScrollViewer? source,FrameworkElement? content)
        {
            if(source is null || content is null)return null;
            var popup=new ScrollViewer{MaxHeight=480,Width=280,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};var flyout=new Flyout{Content=popup};
            flyout.Opening+=(_,_)=>{source.Content=null;popup.Content=content;};flyout.Closed+=(_,_)=>{popup.Content=null;source.Content=content;};
            var button=new Button{Content=title,Flyout=flyout,MinHeight=32,Visibility=Visibility.Collapsed};Toolbar.Children.Add(button);return button;
        }
        var outlineButton=PaneButton("Seznam",outlineScroll,outline);var inspectorButton=PaneButton("Vlastnosti",inspectorScroll,inspector);
        var footer=new ScrollViewer{Content=Footer,MaxHeight=120,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(footer,2);Children.Add(footer);
        SizeChanged+=(_,e)=>
        {
            bool compact=e.NewSize.Width<640;
            if(outline is not null){Body.ColumnDefinitions[0].Width=new(compact?0:e.NewSize.Width<900?150:190);outlineButton!.Visibility=compact?Visibility.Visible:Visibility.Collapsed;}
            if(inspector is not null){Body.ColumnDefinitions[^1].Width=new(compact?0:e.NewSize.Width<900?180:240);inspectorButton!.Visibility=compact?Visibility.Visible:Visibility.Collapsed;}
        };
    }
}
