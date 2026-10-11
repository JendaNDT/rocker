// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Infrastructure;
using System.Text;
internal static class SecretChecks
{
    public static async Task<int> RunAsync(UserFiles files)
    {
        int checks=0;void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
        var store=new SecretStore(files);
        if (!OperatingSystem.IsWindows())
        {
            Check((await store.ReadAsync()).State==SecretState.Unsupported,"DPAPI unsupported is explicit on Linux");
            try { await store.SetAsync("not-a-key");throw new Exception("Plaintext fallback accepted"); }
            catch (PlatformNotSupportedException) { Check(!File.Exists(store.PathForKey),"no plaintext fallback or secret file on Linux"); }
            Console.WriteLine($"PASS: {checks} non-Windows secret contracts; DPAPI checks require Windows.");return checks;
        }
        const string canary="ROCKER_DPAPI_TEST_CANARY_4FA3F109";
        Check((await store.ReadAsync()).State==SecretState.Missing,"missing key state");
        await store.SetAsync(canary);
        byte[] encrypted=await File.ReadAllBytesAsync(store.PathForKey);
        Check(!Encoding.UTF8.GetString(encrypted).Contains(canary,StringComparison.Ordinal),"secret absent in plaintext file");
        var read=await new SecretStore(files).ReadAsync();
        Check(read.State==SecretState.Ready && read.Value==canary && read.ToString()=="Ready","current user DPAPI decrypts after reopening; diagnostic text has no key");
        using (var cancellation=new CancellationTokenSource())
        {
            cancellation.Cancel();try { await store.SetAsync("replacement",cancellation.Token);throw new Exception("Cancelled secret changed"); }
            catch (OperationCanceledException) { Check((await store.ReadAsync()).Value==canary,"cancelled change preserves key"); }
        }
        await store.SetAsync("changed-test-key"); Check((await store.ReadAsync()).Value=="changed-test-key","replacement key");
        await store.RemoveAsync();Check((await store.ReadAsync()).State==SecretState.Missing,"key removed");
        await File.WriteAllBytesAsync(store.PathForKey,[1,2,3]);
        Check((await store.ReadAsync()).State==SecretState.Unavailable,"corrupt or inaccessible DPAPI is an explicit state");
        await store.RemoveAsync();
        Check(!Directory.EnumerateFiles(Path.GetDirectoryName(store.PathForKey)!).Any(),"no abandoned key or temporary file");
        Console.WriteLine($"PASS: {checks} Windows DPAPI secret contracts.");return checks;
    }
}
