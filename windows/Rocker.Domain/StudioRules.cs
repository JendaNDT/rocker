// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;
public sealed record TrackAlignment(double FirstProjectFrame,int? InputDelayFrames,int? OutputDelayFrames,uint DspDelayFrames,double ManualCorrectionMs,bool Continuous,bool DelayMeasured=false);
public readonly record struct TrackPlacement(long OffsetFrames,long TrimStartFrames);
public static class StudioRules
{
    // Compensation is applied once to placement. The original capture remains
    // unchanged and source trimming is non-destructive.
    public static TrackPlacement Place(TrackAlignment alignment,int projectRate,int captureRate,long captureFrames)
    {
        if(projectRate is <8000 or >192000 || captureRate is <8000 or >192000 || captureFrames<1 || !Valid(alignment))throw new ArgumentException("Neplatné zarovnání stopy.");
        double delay=alignment.InputDelayFrames is int input && alignment.OutputDelayFrames is int output?(input+(double)output+alignment.DspDelayFrames)/captureRate:0;
        double frame=alignment.FirstProjectFrame-(delay+alignment.ManualCorrectionMs/1000)*projectRate;
        return frame>=0?new(checked((long)Math.Round(frame)),0):new(0,Math.Min(captureFrames-1,checked((long)Math.Round(-frame*captureRate/projectRate))));
    }
    public static bool Valid(TrackAlignment value)=>double.IsFinite(value.FirstProjectFrame) && value.FirstProjectFrame>=0 && value.InputDelayFrames is null or >=0 && value.OutputDelayFrames is null or >=0 && double.IsFinite(value.ManualCorrectionMs) && value.ManualCorrectionMs is >=-2000 and <=2000;
}
