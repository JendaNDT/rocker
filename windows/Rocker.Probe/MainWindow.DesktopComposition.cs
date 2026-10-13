// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Probe.Desktop;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private FrameworkElement ComposeDesktopStudio(StackPanel source)
    {
        var elements=source.Children.ToArray();source.Children.Clear();
        var timelineHost=elements.OfType<ScrollViewer>().FirstOrDefault(s=>ReferenceEquals(s.Content,studioTimeline))??new ScrollViewer{Content=studioTimeline};timelineHost.MaxHeight=double.PositiveInfinity;
        var outline=Stack();outline.Children.Add(new TextBlock{Text="STOPY",FontSize=12,CharacterSpacing=80});if(studioTrackList is not null)outline.Children.Add(studioTrackList);
        var inspector=Stack();if(studioTrackEditor?.Parent is Border card)card.Child=null;if(studioTrackEditor is not null)inspector.Children.Add(studioTrackEditor);
        var surface=new DocumentSurface(timelineHost,outline,inspector);
        var fileActions=Stack();var recordActions=Stack();var exportActions=Stack();
        foreach(var element in elements)
        {
            if(element==timelineHost || element==studioTrackList || element==studioTrackEditor || element is Border{Child:null})continue;
            if(element is Button button)
            {
                string label=button.Content as string??"";button.MinHeight=32;
                if(label is "Uložit projekt" or "Připravit a přehrát projekt" or "Pozastavit přehrávání projektu" or "Vrátit poslední editaci" or "Nahrát novou stopu do projektu"){button.Content=label switch{"Uložit projekt"=>"Uložit","Připravit a přehrát projekt"=>"Přehrát","Pozastavit přehrávání projektu"=>"Pauza","Vrátit poslední editaci"=>"Zpět",_=>"Nahrát stopu"};ToolTipService.SetToolTip(button,label);surface.Toolbar.Children.Add(button);}
                else if(label.Contains("Export",StringComparison.OrdinalIgnoreCase) || label.Contains("přenos",StringComparison.OrdinalIgnoreCase))exportActions.Children.Add(button);
                else fileActions.Children.Add(button);
            }
            else if(element==studioProjects || element==studioRecordings)fileActions.Children.Add(element);
            else if(element is Slider)surface.Footer.Children.Add(element);
            else if(element is NumberBox box && box.Header is string label && label.StartsWith("Export",StringComparison.Ordinal))exportActions.Children.Add(element);
            else if(element is NumberBox || element is StackPanel)recordActions.Children.Add(element);
            else if(element is TextBlock reading && element==studioReading)surface.Footer.Children.Add(reading);
        }
        void AddMenu(string title,StackPanel content){if(content.Children.Count==0)return;var button=new Button{Content=title,MinHeight=32,Flyout=new Flyout{Content=new ScrollViewer{Content=content,MaxHeight=520,MaxWidth=380}}};surface.Toolbar.Children.Insert(0,button);}
        AddMenu("Nahrávání",recordActions);AddMenu("Export",exportActions);AddMenu("Projekt",fileActions);
        return surface;
    }
    private static UIElement DesktopDocumentContent(FrameworkElement page)
        =>page is DocumentSurface or Views.ScoreDocumentView ? page : new ScrollViewer{Content=page,Padding=new Thickness(16),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
}
