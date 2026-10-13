// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Domain;
namespace Rocker.AppCore;

public sealed class PracticeCoordinator
{
    public PracticeSession Session { get; }=new();
    public ulong AudioFrame { get; set; }
    public uint AudioEpoch { get; set; }
    public double LastFlush { get; set; }
    public double PreviewUntil { get; set; }
    public double LastInteraction { get; set; }=double.NegativeInfinity;
}
