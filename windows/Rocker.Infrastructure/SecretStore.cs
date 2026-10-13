// SPDX-License-Identifier: AGPL-3.0-only
using System.Security.Cryptography;
using System.Text;
namespace Rocker.Infrastructure;
public enum SecretState { Missing, Ready, Unavailable, Unsupported }
public sealed class SecretAccess(SecretState state,string? value = null)
{
    public SecretState State { get; } = state;
    public string? Value { get; } = value;
    public override string ToString() => State.ToString();
}
public sealed class SecretStore(UserFiles files,string provider="gemini")
{
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes("JendaNDT.Rocker:AI-key:DPAPI:v1"));
    private byte[] ProviderEntropy=>provider=="gemini"?Entropy:SHA256.HashData(Encoding.UTF8.GetBytes("JendaNDT.Rocker:AI-key:DPAPI:"+provider+":v1"));
    private static ReadOnlySpan<byte> Magic => "RockerSecret1\0"u8;
    public string PathForKey => Path.Combine(files.DirectoryFor("secrets"),(provider switch{"gemini"=>"ai-key.dpapi","openai"=>"openai-key.dpapi","github-updates"=>"github-updates.dpapi",_=>throw new ArgumentException("Neplatný poskytovatel klíče.")}));
    public async Task<SecretAccess> ReadAsync(CancellationToken cancellation = default)
    {
        if (!OperatingSystem.IsWindows()) return new(SecretState.Unsupported);
        byte[]? encrypted=null,plain=null;
        try
        {
            string path=PathForKey;
            if (!File.Exists(path)) return new(SecretState.Missing);
            if (new FileInfo(path).Length is < 16 or > 65536) return new(SecretState.Unavailable);
            encrypted=await File.ReadAllBytesAsync(path,cancellation);
            if (!encrypted.AsSpan().StartsWith(Magic)) return new(SecretState.Unavailable);
            plain=ProtectedData.Unprotect(encrypted.AsSpan(Magic.Length).ToArray(),ProviderEntropy,DataProtectionScope.CurrentUser);
            string value=new UTF8Encoding(false,true).GetString(plain);
            return string.IsNullOrWhiteSpace(value) || value.Length>4096 ? new(SecretState.Unavailable) : new(SecretState.Ready,value);
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException or DecoderFallbackException)
        { return new(SecretState.Unavailable); }
        finally { if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted); if (plain is not null) CryptographicOperations.ZeroMemory(plain); }
    }
    public async Task SetAsync(string value,CancellationToken cancellation=default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Klíč lze chránit pouze ve Windows.");
        if (string.IsNullOrWhiteSpace(value) || value.Length>4096) throw new ArgumentException("Klíč je prázdný nebo příliš dlouhý.");
        byte[] plain=Encoding.UTF8.GetBytes(value.Trim()), encrypted=[];
        string path=PathForKey, temporary=Path.Combine(Path.GetDirectoryName(path)!,".rocker-secret-"+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            encrypted=ProtectedData.Protect(plain,ProviderEntropy,DataProtectionScope.CurrentUser);
            await using (var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.Asynchronous|FileOptions.WriteThrough))
            { await stream.WriteAsync(Magic.ToArray(),cancellation); await stream.WriteAsync(encrypted,cancellation); await stream.FlushAsync(cancellation); }
            cancellation.ThrowIfCancellationRequested(); File.Move(temporary,path,true);
        }
        finally { CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(encrypted); if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public Task RemoveAsync(CancellationToken cancellation=default) => Task.Run(()=> { cancellation.ThrowIfCancellationRequested(); if (File.Exists(PathForKey)) File.Delete(PathForKey); },cancellation);
}
