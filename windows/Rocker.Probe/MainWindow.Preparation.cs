// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private CancellationTokenSource? preparationCancellation;
    private Rocker.AppCore.OperationLease? preparationOperation;
    private CancellationToken PreparationToken => preparationCancellation?.Token ?? CancellationToken.None;
    private void BeginPreparation(string message)
    {
        preparationOperation = services.Operations.Begin(documentContext.Current.Id, "prepare", message);
        preparationCancellation = CancellationTokenSource.CreateLinkedTokenSource(preparationOperation.Cancellation);
        ShellInteractionRoot.IsEnabled = false;
        PreparationNotice.RequestedTheme=ShellRoot.RequestedTheme;PreparationNotice.Message = message;
        PreparationNotice.IsOpen = true;
        PreparationCancelButton.IsEnabled = true;
    }
    private void CancelPreparation_Click(object sender, RoutedEventArgs args)
    {
        preparationCancellation?.Cancel();
        songLoadCancelled = true; audio?.CancelMediaPreparation();
        PreparationCancelButton.IsEnabled = false;
        PreparationNotice.Message = "Ruším přípravu. Původní soubory zůstávají zachované…";
    }
    private void ShowPreparationCancelled()
    {
        ShellNotice.IsOpen=true;ShellNotice.Severity=Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
        ShellNotice.Message="Příprava byla zrušená. Zvukové soubory zůstávají zachované a přehrávání se samo nespustí.";
    }
    private void EndPreparation()
    {
        preparationCancellation?.Dispose(); preparationCancellation = null;
        preparationOperation?.Dispose(); preparationOperation = null;
        if (closed) return;
        PreparationNotice.IsOpen = false;
        ShellInteractionRoot.IsEnabled = true;
    }
}
