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
        var second=DesktopDocumentKey.ForObject("15","second-project");
        var layout=new DesktopWorkspaceLayout(1,[new("workspace",DesktopWindowRole.Workspace,new(-1000,30,1280,900),[key,second],SelectedDocumentIndex:1),new("tuner",DesktopWindowRole.Tool,new(-200,50,450,700),[],"03",AlwaysOnTop:true,Visible:false)],AttachedTools:["04"],ViewPreferences:[new(key,1.5,2,5,2,4,false,false)]);
        var store=new WorkspaceLayoutStore(files);await store.SaveAsync(layout);var loaded=store.Load()!;
        Check(loaded.Windows[0].Documents[0].Equals(key) && loaded.Windows[0].Bounds.X==-1000,"Unicode document identity and negative monitor coordinates roundtrip");
        Check(!loaded.Windows[1].Visible && loaded.Windows[1].AlwaysOnTop && loaded.AttachedTools!.SequenceEqual(["04"]),"Hidden floating and attached tool states remain distinct");
        Check(loaded.Windows[0].SelectedDocumentIndex==1 && loaded.ViewPreferences!.Single().Zoom==1.5 && loaded.ViewPreferences!.Single().Bar==5 && !loaded.ViewPreferences!.Single().Follow,"Selected tab and independent score preferences roundtrip without audio state");
        try{await store.SaveAsync(layout with{ViewPreferences=[new(key,double.NaN)]});throw new Exception("Invalid view preference accepted");}catch(InvalidDataException){Check(store.Load()!.ViewPreferences!.Single().Zoom==1.5,"Invalid preference does not replace the valid layout");}
        try{await store.SaveAsync(layout with{Windows=[layout.Windows[0] with{SelectedDocumentIndex=2}]});throw new Exception("Invalid selection accepted");}catch(InvalidDataException){Check(store.Load()!.Windows.Count==2,"Out-of-range selection is rejected before writing");}
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{await store.SaveAsync(layout with{LibraryWidthDip=400},cancellation.Token);}catch(OperationCanceledException){}Check(store.Load()!.LibraryWidthDip==220,"Canceled write preserves the previous layout");}
        string previous=await File.ReadAllTextAsync(store.FilePath);
        try{await store.SaveAsync(layout with{AttachedTools=["03"]});throw new Exception("Duplicate tool accepted");}catch(InvalidDataException){Check(await File.ReadAllTextAsync(store.FilePath)==previous,"Invalid layout is rejected before replacing any file");}
        await UserFiles.WriteTextAsync(store.FilePath,"{\"SchemaVersion\":99,\"Windows\":[]}");
        try{store.Load();throw new Exception("Future layout accepted");}catch(InvalidDataException){Check(true,"Future version rejected without altering music data");}
        await UserFiles.WriteTextAsync(store.FilePath,"malformed");try{store.Load();throw new Exception("Malformed layout accepted");}catch(InvalidDataException){Check(true,"Malformed JSON has an explicit recovery path");}
        await store.SaveAsync(layout);Check(store.Load()!.Validate().Windows.Count==2,"Valid layout can be saved after recovery");return checks;
    }
}
