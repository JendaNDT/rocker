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
        if(outline is not null){Body.ColumnDefinitions.Add(new(){Width=new(190)});Body.Children.Add(new ScrollViewer{Content=outline,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});}
        int center=Body.ColumnDefinitions.Count;Body.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});Grid.SetColumn(main,center);Body.Children.Add(main);
        if(inspector is not null){Body.ColumnDefinitions.Add(new(){Width=new(240)});var scroll=new ScrollViewer{Content=inspector,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetColumn(scroll,center+1);Body.Children.Add(scroll);}
        Grid.SetRow(Footer,2);Children.Add(Footer);
        SizeChanged+=(_,e)=>
        {
            if(outline is not null)Body.ColumnDefinitions[0].Width=new(e.NewSize.Width<780?140:190);
            if(inspector is not null)Body.ColumnDefinitions[^1].Width=new(e.NewSize.Width<780?160:240);
        };
    }
}
