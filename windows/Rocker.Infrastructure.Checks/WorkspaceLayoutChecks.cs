// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Infrastructure.Checks;

internal static class WorkspaceLayoutChecks
{
    internal static async Task<int> RunAsync(UserFiles files)
    {
        int checks=0;void Check(bool value,string name){++checks;if(!value)throw new InvalidOperationException(name);}
        var key=DesktopDocumentKey.ForCanonicalFile("20",Path.GetFullPath(Path.Combine(files.Root,"české noty.musicxml")));
        var layout=new DesktopWorkspaceLayout(1,[new("workspace",DesktopWindowRole.Workspace,new(-1000,30,1280,900),[key]),new("tuner",DesktopWindowRole.Tool,new(-200,50,450,700),[],"03",AlwaysOnTop:true,Visible:false)],AttachedTools:["04"]);
        var store=new WorkspaceLayoutStore(files);await store.SaveAsync(layout);var loaded=store.Load()!;
        Check(loaded.Windows[0].Documents[0].Equals(key) && loaded.Windows[0].Bounds.X==-1000,"Unicode document identity and negative monitor coordinates roundtrip");
        Check(!loaded.Windows[1].Visible && loaded.Windows[1].AlwaysOnTop && loaded.AttachedTools!.SequenceEqual(["04"]),"Hidden floating and attached tool states remain distinct");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{await store.SaveAsync(layout with{LibraryWidthDip=400},cancellation.Token);}catch(OperationCanceledException){}Check(store.Load()!.LibraryWidthDip==220,"Canceled write preserves the previous layout");}
        string previous=await File.ReadAllTextAsync(store.FilePath);
        try{await store.SaveAsync(layout with{AttachedTools=["03"]});throw new Exception("Duplicate tool accepted");}catch(InvalidDataException){Check(await File.ReadAllTextAsync(store.FilePath)==previous,"Invalid layout is rejected before replacing any file");}
        await UserFiles.WriteTextAsync(store.FilePath,"{\"SchemaVersion\":99,\"Windows\":[]}");
        try{store.Load();throw new Exception("Future layout accepted");}catch(InvalidDataException){Check(true,"Future version rejected without altering music data");}
        await UserFiles.WriteTextAsync(store.FilePath,"malformed");try{store.Load();throw new Exception("Malformed layout accepted");}catch(InvalidDataException){Check(true,"Malformed JSON has an explicit recovery path");}
        await store.SaveAsync(layout);Check(store.Load()!.Validate().Windows.Count==2,"Valid layout can be saved after recovery");return checks;
    }
}
