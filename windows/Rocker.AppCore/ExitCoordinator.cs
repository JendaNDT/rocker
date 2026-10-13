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
            var choices=new List<(string Key,PendingWork Guard,CloseChoice Choice)>();
            foreach(var entry in Guards.Where(p=>documentId is null || p.Key.StartsWith(documentId+"/",StringComparison.Ordinal)).ToArray())
            {
                var guard=entry.Value;if(!guard.State.RequiresConfirmation)continue;
                var choice=await choose(guard);
                if(choice==CloseChoice.Cancel)return false;
                if(choice==CloseChoice.Discard && guard.State.Recording)return false;
                choices.Add((entry.Key,guard,choice));
            }
            // A later cancellation must never discard an earlier document's work.
            // Saving may fail, so finish all saves before applying any discard.
            foreach(var item in choices)if(item.Choice==CloseChoice.Save && !await item.Guard.Save())return false;
            foreach(var item in choices){if(item.Choice==CloseChoice.Discard)item.Guard.Discard();Guards.Remove(item.Key);}
            return true;
        }
        finally{Resolving=false;}
    }
}
