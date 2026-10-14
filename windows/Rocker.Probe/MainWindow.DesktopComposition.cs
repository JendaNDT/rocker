// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Probe.Desktop;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private FrameworkElement ComposeDesktopStudio(StackPanel source)
    {
        var elements=source.Children.ToArray();foreach(var element in elements)source.Children.Remove(element);
        var timelineHost=elements.OfType<ScrollViewer>().FirstOrDefault(s=>ReferenceEquals(s.Content,studioTimeline))??new ScrollViewer{Content=studioTimeline};timelineHost.MaxHeight=double.PositiveInfinity;
        var tracks=studioTrackList;var outline=Stack();outline.Children.Add(new TextBlock{Text="STOPY",FontSize=12,CharacterSpacing=80});
        WriteShellDiagnostic("desktop-composition-stage.json",new{stage="attach-track-list",parent=tracks?.Parent?.GetType().Name,session=documentContext.Current.Id,removedFromSource=tracks is null || !source.Children.Contains(tracks)});
        if(tracks is not null){tracks.Header=null;outline.Children.Add(tracks);}
        var inspector=Stack();var card=elements.OfType<Border>().FirstOrDefault(b=>ReferenceEquals(b.Child,studioTrackEditor));if(card is not null)card.Child=null;if(studioTrackEditor is not null)inspector.Children.Add(studioTrackEditor);
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
        var owner=documentContext.Current;
        var redo=new Button{Content="Znovu",MinHeight=32};ToolTipService.SetToolTip(redo,"Opakovat vrácenou editaci · Ctrl+Y");redo.Click+=async(_,_)=>await ExecuteDesktopCommandAsync("edit.redo",owner);surface.Toolbar.Children.Add(redo);
        return surface;
    }
    private FrameworkElement ComposeDesktopSong(StackPanel source)
    {
        var elements=source.Children.ToArray();foreach(var element in elements)source.Children.Remove(element);
        var body=new Grid{RowSpacing=8};body.RowDefinitions.Add(new(){Height=GridLength.Auto});body.RowDefinitions.Add(new(){Height=new(1,GridUnitType.Star)});
        var waveform=elements.OfType<Border>().FirstOrDefault(b=>ReferenceEquals(b.Child,songWaveform));if(waveform is not null)body.Children.Add(waveform);
        if(songSections is not null){Grid.SetRow(songSections,1);body.Children.Add(songSections);}
        var range=elements.OfType<Border>().FirstOrDefault(b=>!ReferenceEquals(b,waveform));var editor=Stack();if(range is not null)editor.Children.Add(range);
        var surface=new DocumentSurface(body,inspector:editor);var practice=Stack();
        foreach(var element in elements)
        {
            if(element==waveform || element==range || element==songSections)continue;
            if(element is Button button)
            {
                string label=button.Content as string??"";button.MinHeight=32;
                if(label is "Otevřít skladbu…" or "Spustit skladbu" or "Pozastavit skladbu" or "Zrušit načítání")
                {button.Content=label switch{"Otevřít skladbu…"=>"Otevřít","Spustit skladbu"=>"Přehrát","Pozastavit skladbu"=>"Pauza",_=>"Zrušit"};surface.Toolbar.Children.Add(button);}
                else editor.Children.Add(button);
            }
            else if(element==songReading)surface.Footer.Children.Add(element);
            else if(element is TextBlock)continue;
            else practice.Children.Add(element);
        }
        surface.Toolbar.Children.Add(new Button{Content="Cvičení a rychlost",MinHeight=32,Flyout=new Flyout{Content=new ScrollViewer{Content=practice,MaxHeight=500,Width=320}}});
        return surface;
    }
    private FrameworkElement BuildDesktopDocumentPage(Rocker.Domain.NavigationTarget target)
    {
        var page=BuildWorkspace(target);
        if(target.ViewId is not("09" or "10" or "11" or "12" or "17" or "18" or "19" or "21" or "22" or "23" or "24" or "25"))return page;
        StackPanel? source=page as StackPanel;
        if(page is Border{Child:StackPanel contents} border){border.Child=null;source=contents;}
        if(source is null)return page;
        var children=source.Children.ToArray();foreach(var child in children)source.Children.Remove(child);
        var main=Stack();var inspector=Stack();var additionalActions=Stack();
        var surface=new DocumentSurface(new ScrollViewer{Content=main,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},inspector:inspector);
        foreach(var child in children)
        {
            if(child is Button button)
            {
                string title=button.Content as string??"";
                bool primary=title.StartsWith("Spustit",StringComparison.Ordinal) || title.StartsWith("Pozastavit",StringComparison.Ordinal) || title.StartsWith("Zastavit",StringComparison.Ordinal) || title.StartsWith("Poslechnout",StringComparison.Ordinal) || title.StartsWith("Nová",StringComparison.Ordinal) || title.StartsWith("Odeslat",StringComparison.Ordinal) || title.StartsWith("Vygenerovat",StringComparison.Ordinal) || title.StartsWith("Zrušit",StringComparison.Ordinal);
                if(primary){button.MinHeight=32;surface.Toolbar.Children.Add(button);}
                else if(target.ViewId=="22" && earAnswers.Contains(button))main.Children.Add(button);
                else additionalActions.Children.Add(button);
            }
            else if(child is NumberBox or ComboBox or AutoSuggestBox or CheckBox or ToggleSwitch or Expander)inspector.Children.Add(child);
            else main.Children.Add(child);
        }
        if(additionalActions.Children.Count>0)surface.Toolbar.Children.Add(new Button{Content="Další akce",MinHeight=32,Flyout=new Flyout{Content=new ScrollViewer{Content=additionalActions,MaxHeight=500,Width=320}}});
        return surface;
    }
    private static UIElement DesktopDocumentContent(FrameworkElement page)
        =>page is DocumentSurface or Views.ScoreDocumentView or Views.VideoDocumentView || page.Tag is "desktop-settings" ? page : new ScrollViewer{Content=page,Padding=new Thickness(16),HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
}
