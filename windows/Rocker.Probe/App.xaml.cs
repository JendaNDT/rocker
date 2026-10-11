// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Rocker.Video;

namespace Rocker.Probe;
public partial class App : Application
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    private Window? window;
    public App()
    {
        UnhandledException += (_, args) => LogStartupFailure(args.Exception, args.Message);
        try { InitializeComponent(); }
        catch (Exception error) { LogStartupFailure(error); throw; }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(VideoProtocol.AppId));
            window = new MainWindow();
            window.Activate();
        }
        catch (Exception error) { LogStartupFailure(error); throw; }
    }
    private static void LogStartupFailure(Exception error, string? context = null)
    {
        // Diagnostics must never replace or suppress the original startup exception.
        try
        {
            var directory = Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rocker", "logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "startup-error.txt"), $"{DateTime.UtcNow:O}\n{context}\n{error}\n");
        }
        catch (Exception) { }
    }
}
