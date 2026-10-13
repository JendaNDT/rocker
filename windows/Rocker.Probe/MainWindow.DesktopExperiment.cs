// SPDX-License-Identifier: AGPL-3.0-only
using Rocker.Probe.Desktop;

namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private async Task VerifyDesktopHostExperimentAsync()
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if (audio is null) throw new InvalidOperationException("Desktop experiment needs the existing audio session.");
        // This method only runs in the isolated Windows diagnostic profile.
        using var experiment = new DesktopHostExperiment(ScoreImage.Source);
        try
        {
            object evidence = await experiment.RunAsync(audio, (name, element) => CaptureNativeViewAsync(name, element));
            WriteShellDiagnostic("desktop-host-experiment.json", evidence);
        }
        catch (Exception error)
        {
            WriteShellDiagnostic("desktop-host-experiment.json", new { result = "failed", error = error.ToString(),
                productionShellReplaced = false, hardwareVerified = false });
            throw;
        }
        finally { Activate(); }
    }
}
