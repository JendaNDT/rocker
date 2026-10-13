// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.AppCore;

// All source mutations are serialized and retain the initiating document over awaits.
public sealed class TransportCoordinator
{
    private readonly SemaphoreSlim transition = new(1, 1);
    public DocumentSession? Owner { get; private set; }
    public DocumentSession? Grader { get; private set; }
    public bool Capturing { get; set; }
    public bool LooperActive { get; set; }
    public long Revision { get; private set; }
    public async Task<bool> TakeAsync(DocumentSession requested, Func<Task> prepare, CancellationToken cancellation = default)
    {
        await transition.WaitAsync(cancellation);
        try
        {
            if (!requested.IsAlive) return false;
            if (Owner != requested && (Capturing || LooperActive))
                throw new InvalidOperationException("Před změnou zdroje zastav a ulož nahrávání nebo looper.");
            cancellation.ThrowIfCancellationRequested();
            await prepare();
            cancellation.ThrowIfCancellationRequested();
            if (!requested.IsAlive) return false;
            Owner = requested; ++Revision; return true;
        }
        finally { transition.Release(); }
    }
    public async Task UpdateOwnerAsync(DocumentSession requested,Func<Task> update)
    {
        await transition.WaitAsync();
        try{if(Owner==requested && requested.IsAlive)await update();}
        finally{transition.Release();}
    }
    public void SetGrader(DocumentSession? requested)
    {
        if (requested is not null && !requested.IsAlive) throw new InvalidOperationException("Tréninková relace už je zavřená.");
        Grader = requested;
    }
    public void Release(DocumentSession owner) { if (Owner == owner) { Owner = null; ++Revision; } if (Grader == owner) Grader = null; }
}
