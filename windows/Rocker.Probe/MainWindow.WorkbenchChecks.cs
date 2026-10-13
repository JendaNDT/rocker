// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
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
