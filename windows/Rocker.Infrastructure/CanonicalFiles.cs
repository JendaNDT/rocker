// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;
namespace Rocker.Infrastructure;

public sealed record CanonicalFile(string Path,string Identity);
public static class CanonicalFiles
{
    [StructLayout(LayoutKind.Sequential)] private struct FileInformation
    {
        public uint Attributes,CreatedLow,CreatedHigh,AccessedLow,AccessedHigh,WrittenLow,WrittenHigh,Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle,StringBuilder path,uint length,uint flags);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileInformation information);
    public static CanonicalFile ResolveExisting(string path)
    {
        string full=RecentFiles.ValidatePath(path);if(!File.Exists(full))throw new FileNotFoundException("Soubor není dostupný.");
        if(!OperatingSystem.IsWindows())
        {
            var target=new FileInfo(full).ResolveLinkTarget(true);string resolved=target?.FullName??full;return new(resolved,resolved);
        }
        using var handle=File.OpenHandle(full,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var buffer=new StringBuilder(32768);uint count=GetFinalPathNameByHandleW(handle,buffer,(uint)buffer.Capacity,0);
        if(count==0 || count>=buffer.Capacity || !GetFileInformationByHandle(handle,out var info))throw new IOException("Identitu souboru nelze ověřit.");
        string canonical=buffer.ToString();
        if(canonical.StartsWith(@"\\?\UNC\",StringComparison.Ordinal))canonical=@"\\"+canonical[8..];
        else if(canonical.StartsWith(@"\\?\",StringComparison.Ordinal))canonical=canonical[4..];
        return new(canonical,$"{info.Volume:x8}:{info.IndexHigh:x8}{info.IndexLow:x8}:{info.CreatedHigh:x8}{info.CreatedLow:x8}");
    }
}
