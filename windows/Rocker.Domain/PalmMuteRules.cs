// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;
public sealed record PalmMuteCalibration(double OpenMean, double MutedMean, double Threshold);
public sealed record PalmMuteDrill(string Id, string Name, string Description, IReadOnlyList<string> Pattern);
public sealed record PalmMuteState(PalmMuteDrill Drill, IReadOnlyList<string> Hits);
public static class PalmMuteRules
{
    public const int MinimumCalibrationSamples = 3;
    // Consumes centroid measurements from the native analyzer. FFT and onset
    // capture remain in C++; these are calibration and exercise rules only.
    public static PalmMuteCalibration? Calibrate(IReadOnlyList<double> open, IReadOnlyList<double> muted)
    {
        if (open.Count < MinimumCalibrationSamples || muted.Count < MinimumCalibrationSamples || open.Concat(muted).Any(value => !double.IsFinite(value) || value < 0)) return null;
        double openMean = open.Average(), mutedMean = muted.Average();
        return openMean > mutedMean ? new(openMean, mutedMean, (openMean + mutedMean) / 2) : null;
    }
    public static string ClassifyHit(double centroid, PalmMuteCalibration calibration)
    {
        if (!double.IsFinite(centroid) || centroid < 0 || !double.IsFinite(calibration.Threshold)) throw new ArgumentOutOfRangeException(nameof(centroid));
        return centroid < calibration.Threshold ? "muted" : "open";
    }
    public static PalmMuteState Start(PalmMuteDrill drill)
    {
        if (drill.Pattern.Count is 0 or > 10000 || drill.Pattern.Any(value => value is not ("muted" or "open"))) throw new ArgumentException("Neplatná sekvence technik.", nameof(drill));
        return new(drill with { Pattern = Array.AsReadOnly(drill.Pattern.ToArray()) }, Array.Empty<string>());
    }
    public static bool IsComplete(PalmMuteState state) => state.Hits.Count >= state.Drill.Pattern.Count;
    public static string? ExpectedTechnique(PalmMuteState state) => IsComplete(state) ? null : state.Drill.Pattern[state.Hits.Count];
    public static PalmMuteState LogHit(PalmMuteState state, string technique)
    {
        if (technique is not ("muted" or "open")) throw new ArgumentException("Neznámá technika.", nameof(technique));
        return IsComplete(state) ? state : state with { Hits = Array.AsReadOnly(state.Hits.Append(technique).ToArray()) };
    }
    public static int Score(PalmMuteState state) => state.Hits.Where((hit, index) => hit == state.Drill.Pattern[index]).Count();
}
