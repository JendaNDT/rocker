// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private async Task VerifyRecordingContrastAsync()
    {
        string previous=selectedTheme;var samples=new List<object>();
        static double Luminance(Windows.UI.Color color)
        {
            static double Channel(byte value){double v=value/255d;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}
            return .2126*Channel(color.R)+.7152*Channel(color.G)+.0722*Channel(color.B);
        }
        try
        {
            foreach(string theme in new[]{"dark","light"})
            {
                ApplyTheme(theme);await Task.Delay(150);ShellRoot.UpdateLayout();
                var wave=recordingWaveform!.Children.OfType<Polyline>().Single();
                var stroke=((SolidColorBrush)wave.Stroke).Color;
                var background=((SolidColorBrush)((Border)recordingWaveform.Parent).Background).Color;
                double a=Luminance(stroke),b=Luminance(background),contrast=(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
                samples.Add(new{theme,contrast,stroke=$"#{stroke.R:X2}{stroke.G:X2}{stroke.B:X2}",background=$"#{background.R:X2}{background.G:X2}{background.B:X2}"});
                if(contrast<3)throw new InvalidOperationException("Recording waveform has insufficient contrast in "+theme+" theme.");
            }
        }
        finally{ApplyTheme(previous);}
        WriteShellDiagnostic("desktop-recording-contrast.json",new{result="passed",samples,sourceCommit=Environment.GetEnvironmentVariable("GITHUB_SHA")});
    }
    private async Task VerifyWorkbenchLayoutAsync()
    {
        var samples = new List<object>();var clientWidths=new HashSet<int>();
        var original = AppWindow.Size;
        try
        {
            foreach (int width in new[] { 800, 1000, 1200 })
            {
                AppWindow.Resize(new(width, 940)); await Task.Delay(150); ShellRoot.UpdateLayout();
                var transport = MediaPanel.TransformToVisual(ShellRoot).TransformPoint(new Point(0, 0));
                if (MediaPanel.ActualHeight > 62 || MediaPanel.ActualHeight < 32 || desktopRoot.ActualHeight < 300)
                    throw new InvalidOperationException("Workbench transport consumed document space.");
                if (Grid.GetRow(desktopRoot) != 2 || desktopLibrarySection.XamlRoot is null || desktopRoot.ActualWidth > ShellRoot.ActualWidth + 1)
                    throw new InvalidOperationException("Workbench chrome did not fit the native window.");
                clientWidths.Add((int)Math.Round(ShellRoot.ActualWidth));
                samples.Add(new { windowWidth = width, clientWidthDip = ShellRoot.ActualWidth, documentHeightDip = desktopRoot.ActualHeight, transportHeightDip = MediaPanel.ActualHeight, transportTopDip = transport.Y });
            }
        }
        finally { AppWindow.Resize(original); }
        if(clientWidths.Count!=3)throw new InvalidOperationException("Workbench checks require three distinct actual client widths.");
        WriteShellDiagnostic("desktop-workbench.json", new { result = "passed", interfaceRevision = 2, samples, toolsOpenInNativeWindows = true, physicalPointerVerified = false, sourceCommit = Environment.GetEnvironmentVariable("GITHUB_SHA") });
    }
}
