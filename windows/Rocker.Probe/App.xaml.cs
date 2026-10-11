// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Rocker.Video;
using Rocker.Infrastructure;

namespace Rocker.Probe;
public partial class App : Application
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    private Window? window;private Mutex? instanceMutex;private bool ownsInstance;private CancellationTokenSource? instanceCancellation;private Task? instanceListener;
    public App()
    {
        UnhandledException += (_, args) => LogStartupFailure(args.Exception, args.Message);
        try { InitializeComponent(); }
        catch (Exception error) { LogStartupFailure(error); throw; }
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            string root=new UserFiles(Environment.GetEnvironmentVariable("ROCKER_USER_DATA_DIR")).Root;string identity=System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value+"|"+root.ToUpperInvariant();string name="Rocker-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)))[..32];
            instanceMutex=new Mutex(true,"Local\\"+name,out bool created);ownsInstance=created;if(!created){try{ownsInstance=instanceMutex.WaitOne(0);}catch(AbandonedMutexException){ownsInstance=true;}}
            string request=Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(v=>v!="--")??"";
            if(!ownsInstance){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));await InstanceChannel.SendAsync(name,request,timeout.Token);instanceMutex.Dispose();Exit();return;}
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(VideoProtocol.AppId));
            var main=new MainWindow();window=main;main.QueueFileActivation(request);main.Activate();instanceCancellation=new();instanceListener=InstanceChannel.ListenAsync(name,received=>main.DispatcherQueue.TryEnqueue(()=>main.QueueFileActivation(received)),instanceCancellation.Token);
            main.Closed+=(_,_)=>{instanceCancellation.Cancel();if(ownsInstance){instanceMutex.ReleaseMutex();ownsInstance=false;}instanceMutex.Dispose();};
        }
        catch (Exception error) { LogStartupFailure(error); throw; }
    }
    private static void LogStartupFailure(Exception error, string? context = null)
    {
        // Diagnostics must never replace or suppress the original startup exception.
        try
        {
            var directory = Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR")
                ?? new UserFiles().DirectoryFor("logs");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "startup-error.txt"), $"{DateTime.UtcNow:O}\n{context}\n{error}\n");
        }
        catch (Exception) { }
    }
}
