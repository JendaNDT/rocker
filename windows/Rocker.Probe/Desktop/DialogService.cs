// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.AppCore;
namespace Rocker.Probe.Desktop;

public sealed record WindowContext(Window Window,FrameworkElement Root,DocumentSession Session);
public sealed class DialogService(OperationRegistry operations)
{
    private readonly Dictionary<XamlRoot,SemaphoreSlim> gates=[];
    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog,WindowContext owner)
    {
        var root=owner.Root.XamlRoot;if(root is null || !owner.Session.IsAlive)return ContentDialogResult.None;
        if(!gates.TryGetValue(root,out var gate))gates.Add(root,gate=new(1,1));
        await gate.WaitAsync();
        try
        {
            if(!owner.Session.IsAlive || owner.Root.XamlRoot!=root)return ContentDialogResult.None;
            using var operation=operations.Begin(owner.Session.Id,"dialog","Otevřený dialog",cancellable:false);
            dialog.XamlRoot=root;return await dialog.ShowAsync();
        }
        finally{gate.Release();}
    }
    public void Forget(XamlRoot? root){if(root is not null)gates.Remove(root);}
}
