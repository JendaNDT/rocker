// SPDX-License-Identifier: AGPL-3.0-only
using System.Runtime.InteropServices;
using Rocker.Infrastructure;
namespace Rocker.Infrastructure.Checks;

internal static class CanonicalFileChecks
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CreateHardLinkW(string link,string source,nint reserved);
    internal static async Task<int> RunAsync(UserFiles files)
    {
        int checks=0;void Check(bool pass,string name){++checks;if(!pass)throw new InvalidOperationException(name);}
        string path=Path.Combine(files.DirectoryFor("settings"),"identity-žluťoučký.wav");await File.WriteAllTextAsync(path,"synthetic identity fixture");
        var file=CanonicalFiles.ResolveExisting(path);Check(File.Exists(file.Path) && file.Identity.Length>0,"Existing file receives a stable readable identity");
        Check(CanonicalFiles.ResolveExisting(path).Identity==file.Identity,"Repeated resolution preserves identity");
        string alias=Path.Combine(Path.GetDirectoryName(path)!,"identity-alias.wav");
        if(OperatingSystem.IsWindows())
        {
            if(!CreateHardLinkW(alias,path,0))throw new IOException("Hard-link fixture could not be created.");
            Check(CanonicalFiles.ResolveExisting(alias).Identity==file.Identity,"Windows hard-link aliases resolve to one file identity");
            Check(CanonicalFiles.ResolveExisting(path.ToUpperInvariant()).Identity==file.Identity,"Windows case aliases resolve to one file identity");
        }
        else{File.CreateSymbolicLink(alias,path);Check(CanonicalFiles.ResolveExisting(alias).Path==file.Path,"Symbolic-link target is resolved before document registration");}
        string copy=Path.Combine(Path.GetDirectoryName(path)!,"identity-copy.wav");File.Copy(path,copy);Check(CanonicalFiles.ResolveExisting(copy).Identity!=file.Identity,"Equal file bytes do not merge distinct document objects");
        File.Delete(path);try{CanonicalFiles.ResolveExisting(path);throw new Exception("Missing file accepted");}catch(FileNotFoundException){Check(true,"Missing files have an explicit failure path");}
        return checks;
    }
}
