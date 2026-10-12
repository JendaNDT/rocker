// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private static FrameworkElement ResponsiveColumns(params FrameworkElement[] panels)
    {
        var grid=new Grid{ColumnSpacing=20,RowSpacing=16};foreach(var panel in panels)grid.Children.Add(panel);int currentColumns=0;
        void Layout(double width){int columns=width>=720?2:1;if(columns==currentColumns)return;currentColumns=columns;grid.ColumnDefinitions.Clear();grid.RowDefinitions.Clear();for(int i=0;i<columns;++i)grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});for(int i=0;i<(panels.Length+columns-1)/columns;++i)grid.RowDefinitions.Add(new(){Height=GridLength.Auto});for(int i=0;i<panels.Length;++i){Grid.SetRow(panels[i],i/columns);Grid.SetColumn(panels[i],i%columns);}}
        Layout(0);grid.SizeChanged+=(_,e)=>Layout(e.NewSize.Width);return grid;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);++i)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private async Task VerifyAllDesignViewsAsync()
    {
        string? directory=Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");if(string.IsNullOrEmpty(directory))return;string previousTheme=selectedTheme;var screenshots=new List<string>();var captures=new List<object>();ShellNotice.IsOpen=false;PracticeRestNotice.IsOpen=false;ApplyTheme("dark");Directory.CreateDirectory(System.IO.Path.Combine(directory,"screens"));
        foreach(var view in Rocker.Domain.WorkspaceViews.All){Navigate(new(view.Id));await Task.Delay(35);string name="screens/view-"+view.Id+".png";await CaptureNativeViewAsync(name);screenshots.Add(name);captures.Add(new{file=name,clientWidthDip=ShellRoot.ActualWidth,clientHeightDip=ShellRoot.ActualHeight,windowWidthPixels=AppWindow.Size.Width,windowHeightPixels=AppWindow.Size.Height});}
        foreach(var (width,height) in new[]{(640,720),(1680,1000)}){AppWindow.Resize(new Windows.Graphics.SizeInt32(width,height));Navigate(new("02"));await Task.Delay(50);string name=$"screens/live-{width}.png";await CaptureNativeViewAsync(name);screenshots.Add(name);captures.Add(new{file=name,clientWidthDip=ShellRoot.ActualWidth,clientHeightDip=ShellRoot.ActualHeight,windowWidthPixels=AppWindow.Size.Width,windowHeightPixels=AppWindow.Size.Height});}
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280,940));ApplyTheme("light");Navigate(new("01"));await Task.Delay(50);await CaptureNativeViewAsync("screens/overview-light.png");screenshots.Add("screens/overview-light.png");captures.Add(new{file="screens/overview-light.png",clientWidthDip=ShellRoot.ActualWidth,clientHeightDip=ShellRoot.ActualHeight,windowWidthPixels=AppWindow.Size.Width,windowHeightPixels=AppWindow.Size.Height});ApplyTheme(previousTheme);Navigate(new("01"));
        string links=string.Join("",Rocker.Domain.WorkspaceViews.All.Select(v=>$"<article><h2>{System.Net.WebUtility.HtmlEncode(v.Title)}</h2><a href='screens/view-{v.Id}.png'><img src='screens/view-{v.Id}.png' alt='{System.Net.WebUtility.HtmlEncode(v.Title)}'></a></article>"));await File.WriteAllTextAsync(System.IO.Path.Combine(directory,"native-gallery.html"),"<!doctype html><html lang='cs'><meta charset='utf-8'><title>Rocker — skutečné nativní obrazovky</title><style>body{background:#211e1a;color:#f1dfc1;font:16px system-ui;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:24px}img{width:100%}h2{font-size:18px}</style><h1>32 skutečných Windows pohledů</h1><p>Syntetický kontrolní profil. Obrázky nepotvrzují poslech, online služby ani ruční přístupnost.</p><main>"+links+"</main></html>");
        WriteShellDiagnostic("design-views.json",new{result=screenshots.Count==35?"passed":"failed",nativeViews=32,screenshots,captures,requestedAdaptiveWidthsPixels=new[]{640,1280,1680},themes=new[]{"dark","light"},keyboardManualVerified=false,narratorManualVerified=false,highContrastManualVerified=false});
    }
}
