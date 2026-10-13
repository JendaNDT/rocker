// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Rocker.Video;
using Rocker.Infrastructure;

namespace Rocker.Probe;
public partial class App : Application
{
    internal Rocker.AppCore.ApplicationServices? Services { get; private set; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    private Mutex? setupGuard;private bool ownsSetupGuard;private Window? window;private Mutex? instanceMutex;private bool ownsInstance;private CancellationTokenSource? instanceCancellation;private Task? instanceListener;
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
            if(!ownsInstance && request=="--prepare-update"){Environment.ExitCode=2;Exit();return;}
            if(!ownsInstance){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));await InstanceChannel.SendAsync(name,request,timeout.Token);instanceMutex.Dispose();Exit();return;}
            await ColdProfileRecovery.RecoverInterruptedAsync(new UserFiles(root));
            if(request=="--prepare-update"){try{if(System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName).Any(process=>{using(process)return process.Id!=Environment.ProcessId;}))throw new IOException("Před aktualizací zavři všechny běžící verze Rockeru.");await UpdatePreparation.CreateAsync(new UserFiles(root),typeof(App).Assembly.GetName().Version!.ToString());Environment.ExitCode=0;}catch(Exception error){LogStartupFailure(error,"Update backup failed; existing data retained.");Environment.ExitCode=1;}Exit();return;}
            setupGuard=new Mutex(true,"Local\\JendaNDT.Rocker.SetupGuard",out ownsSetupGuard);if(!ownsSetupGuard){try{ownsSetupGuard=setupGuard.WaitOne(0);}catch(AbandonedMutexException){ownsSetupGuard=true;}}
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(VideoProtocol.AppId));
            Services=new(new UserFiles(root));var main=new MainWindow();window=main;main.QueueFileActivation(request);main.Activate();instanceCancellation=new();instanceListener=ObserveInstanceListenerAsync(name,main,instanceCancellation.Token);
            main.Closed+=async (_,_)=>{instanceCancellation.Cancel();try{await Services.DisposeAsync();}catch(Exception error){LogStartupFailure(error,"Application service shutdown failed.");}if(ownsSetupGuard){setupGuard!.ReleaseMutex();ownsSetupGuard=false;}setupGuard?.Dispose();if(ownsInstance){instanceMutex.ReleaseMutex();ownsInstance=false;}instanceMutex.Dispose();};
        }
        catch (Exception error) { LogStartupFailure(error); throw; }
    }
    private static async Task ObserveInstanceListenerAsync(string name,MainWindow main,CancellationToken cancellation)
    {
        try{await InstanceChannel.ListenAsync(name,received=>main.DispatcherQueue.TryEnqueue(()=>main.QueueFileActivation(received)),cancellation);}
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){}
        catch(Exception error){LogStartupFailure(error,"Instance channel failed; the existing workspace remains available.");}
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
