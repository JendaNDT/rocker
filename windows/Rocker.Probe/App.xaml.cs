// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;

namespace Rocker.Probe;
public partial class App : Application
{
    private Window? window;
    public App()
    {
        UnhandledException += (_, args) => LogStartupFailure(args.Exception);
        try { InitializeComponent(); }
        catch (Exception error) { LogStartupFailure(error); throw; }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            window = new MainWindow();
            window.Activate();
        }
        catch (Exception error) { LogStartupFailure(error); throw; }
    }
    private static void LogStartupFailure(Exception error)
    {
        // Diagnostics must never replace or suppress the original startup exception.
        try
        {
            var directory = Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rocker", "logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "startup-error.txt"), $"{DateTime.UtcNow:O}\n{error}\n");
        }
        catch (Exception) { }
    }
}
