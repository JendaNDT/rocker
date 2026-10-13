// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
namespace Rocker.AppCore;

public sealed record PendingWork(WorkState State,Func<Task<bool>> Save,Action Discard);
public sealed class ExitCoordinator
{
    public Dictionary<string,PendingWork> Guards { get; }=[];
    public bool Resolving { get; private set; }
    public async Task<bool> ResolveAsync(Func<PendingWork,Task<CloseChoice>> choose,string? documentId=null)
    {
        if(Resolving)return false;Resolving=true;
        try
        {
            foreach(var entry in Guards.Where(p=>documentId is null || p.Key.StartsWith(documentId+"/",StringComparison.Ordinal)).ToArray())
            {
                var guard=entry.Value;if(!guard.State.RequiresConfirmation)continue;
                var choice=await choose(guard);
                if(choice==CloseChoice.Cancel)return false;
                if(choice==CloseChoice.Save && !await guard.Save())return false;
                if(choice==CloseChoice.Discard){if(guard.State.Recording)return false;guard.Discard();}
                Guards.Remove(entry.Key);
            }
            return true;
        }
        finally{Resolving=false;}
    }
}
