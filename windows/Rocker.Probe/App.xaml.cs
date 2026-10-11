// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;

namespace Rocker.Probe;
public partial class App : Application
{
    private Window? window;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow();
        window.Activate();
    }
}
