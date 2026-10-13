// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Rocker.Domain;
using Rocker.Infrastructure;
namespace Rocker.Probe;

public sealed partial class MainWindow
{
    private Canvas? recordingWaveform { get => documentContext.Current.Get<Canvas?>("recordingWaveform", () => default); set => documentContext.Current.Set("recordingWaveform",value); }
    private NumberBox? recordingRangeA { get => documentContext.Current.Get<NumberBox?>("recordingRangeA", () => default); set => documentContext.Current.Set("recordingRangeA",value); }
    private NumberBox? recordingRangeB { get => documentContext.Current.Get<NumberBox?>("recordingRangeB", () => default); set => documentContext.Current.Set("recordingRangeB",value); }
    private TextBlock? recordingWaveformStatus { get => documentContext.Current.Get<TextBlock?>("recordingWaveformStatus", () => default); set => documentContext.Current.Set("recordingWaveformStatus",value); }
    private List<Button> recordingWaveformActions { get => documentContext.Current.Get<List<Button>>("recordingWaveformActions", () => []); }
    private float[] recordingPeaks { get => documentContext.Current.Get<float[]>("recordingPeaks", () => []); set => documentContext.Current.Set("recordingPeaks",value); }
    private bool recordingRangeUpdating { get => documentContext.Current.Get<bool>("recordingRangeUpdating", () => default); set => documentContext.Current.Set("recordingRangeUpdating",value); }
    private double? recordingDragStart { get => documentContext.Current.Get<double?>("recordingDragStart", () => default); set => documentContext.Current.Set("recordingDragStart",value); }
    private Line? recordingPlayhead { get => documentContext.Current.Get<Line?>("recordingPlayhead", () => default); set => documentContext.Current.Set("recordingPlayhead",value); }private TextBlock? recordingWaveformCaption { get => documentContext.Current.Get<TextBlock?>("recordingWaveformCaption", () => default); set => documentContext.Current.Set("recordingWaveformCaption",value); }
    private FrameworkElement BuildRecordingWaveform()
    {var capturedDocument = documentContext.Current;
        var panel = Stack();recordingWaveformCaption=Text("PRŮBĚH NAHRÁVKY");recordingWaveformCaption.Style=(Style)Application.Current.Resources["VintageSectionTextStyle"];panel.Children.Add(recordingWaveformCaption);
        recordingWaveformStatus = Text("Vyber nahrávku a použij Zobrazit průběh nebo Přehrát. Přetažením ve zvukové vlně vybereš úsek; kliknutím změníš pozici.", true); panel.Children.Add(recordingWaveformStatus);
        recordingWaveform = new Canvas { Height = 160, HorizontalAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(recordingWaveform, "Průběh nahrávky; úsek lze vybrat také políčky A a B a pozici posuvníkem přehrávače.");
        recordingWaveform.SizeChanged += (_, _) => {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;DrawRecordingWaveform();};
        recordingWaveform.ActualThemeChanged += (_, _) => {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;DrawRecordingWaveform();};
        recordingWaveform.PointerPressed += (_, args) =>
        {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;
            if (!RecordingWaveformAvailable() || !args.GetCurrentPoint(recordingWaveform).Properties.IsLeftButtonPressed) return;
            recordingDragStart = args.GetCurrentPoint(recordingWaveform).Position.X;
            recordingWaveform.CapturePointer(args.Pointer); args.Handled = true;
        };
        recordingWaveform.PointerMoved += (_, args) =>
        {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;
            if (recordingDragStart is not double start || !RecordingWaveformAvailable()) return;
            var finish = args.GetCurrentPoint(recordingWaveform).Position.X;
            if (Math.Abs(start - finish) < 4) return;
            SetRecordingRange(WaveformSeconds(Math.Min(start, finish)), WaveformSeconds(Math.Max(start, finish)));
        };
        recordingWaveform.PointerReleased += async (_, args) =>
        {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;
            if (recordingDragStart is not double start) return;
            recordingDragStart = null; recordingWaveform.ReleasePointerCapture(args.Pointer); args.Handled = true;
            if (!RecordingWaveformAvailable()) return;
            double finish = args.GetCurrentPoint(recordingWaveform).Position.X;
            try
            {
                if (Math.Abs(start - finish) < 4) { if(desktopEnabled)await ClaimDesktopSourceAsync(capturedDocument);await audio!.SeekAsync(WaveformSeconds(finish) / currentSpeed); }
                else SetRecordingRange(WaveformSeconds(Math.Min(start, finish)), WaveformSeconds(Math.Max(start, finish)));
            }
            catch (Exception) { ShowPersistenceError("Pozici nahrávky se nepodařilo změnit."); }
        };
        recordingWaveform.PointerCaptureLost += (_, _) => {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return;recordingDragStart = null;};
        panel.Children.Add(recordingWaveform);
        recordingRangeA = new NumberBox { Header = "A · od (sekundy)", Minimum = 0, Maximum = 86400, Value = 0, SmallChange = .1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, IsEnabled = false };
        recordingRangeB = new NumberBox { Header = "B · do (sekundy)", Minimum = 0, Maximum = 86400, Value = 0, SmallChange = .1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, IsEnabled = false };
        recordingRangeA.ValueChanged += (_, _) => {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return; if (!recordingRangeUpdating) DrawRecordingWaveform(); };
        recordingRangeB.ValueChanged += (_, _) => {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return; if (!recordingRangeUpdating) DrawRecordingWaveform(); };
        panel.Children.Add(ResponsiveColumns(recordingRangeA, recordingRangeB));
        recordingWaveformActions.Clear();
        void Action(string label, Func<Task> action)
        {
            var button = new Button { Content = label, MinHeight = 40, IsEnabled = false };
            button.Click += async (_, _) => {using var documentScope = documentContext.Enter(capturedDocument);if(!capturedDocument.IsAlive)return; try { if (!RecordingWaveformAvailable())return;if(desktopEnabled)await ClaimDesktopSourceAsync(capturedDocument);if(RecordingWaveformReady())await action(); } catch (Exception error) { ShowPersistenceError(error.Message); } };
            recordingWaveformActions.Add(button); panel.Children.Add(button);
        }
        Action("Opakovat vybraný úsek A–B", async () =>
        {
            double a = recordingRangeA.Value, b = recordingRangeB.Value;
            if (!double.IsFinite(a) || !double.IsFinite(b) || b <= a || b > playingRecording!.DurationSec) throw new InvalidOperationException("Konec B musí následovat po A a ležet v nahrávce.");
            await audio!.SetLoopAsync(a / currentSpeed, b / currentSpeed, true);
            appliedSongLoopId = song?.Id; appliedSongLoopStart = a; appliedSongLoopEnd = b;
            if (songA is not null) songA.Value = a; if (songB is not null) songB.Value = b;
        });
        Action("Vypnout opakování úseku", async () => await audio!.SetLoopAsync(0, 0, false));
        Action("Přidat značku na aktuální pozici…", async () =>
        {
            var value = playingRecording!;
            double seconds = Math.Clamp(audio!.GetTransport().PositionSeconds * currentSpeed, 0, value.DurationSec);
            var label = await AskRecordingNameAsync("Značka v nahrávce", "Značka");
            if (label is null) return;
            if (value.Markers.Count >= 1000) throw new InvalidOperationException("Nahrávka už obsahuje 1000 značek.");
            await recordingLibrary!.SaveAsync(value with { Markers = Practice.SortMarkers([.. value.Markers, new(Guid.NewGuid().ToString("N"), seconds, label)]) });
            await ReloadRecordingsAsync(); DrawRecordingWaveform();
        });
        return Card(panel);
    }
    private bool RecordingWaveformAvailable() => !busy && !fileOperation && !workspaceOperation && !closingWindow && !powerTransition
        && playingRecording is not null && mediaPath == OwnedPaths.Resolve(userFiles.Root, playingRecording.RelativePath) && recordingPeaks.Length>0;
    private bool RecordingWaveformReady() => RecordingWaveformAvailable() && audio is not null && audio.GetTransport().Ready!=0 && (!desktopEnabled || services.Transport.Owner==documentContext.Current);
    private double WaveformSeconds(double x) => recordingWaveform!.ActualWidth <= 0 ? 0 : Math.Clamp(x / recordingWaveform.ActualWidth, 0, 1) * playingRecording!.DurationSec;
    private void SetRecordingRange(double a, double b)
    {
        if (recordingRangeA is null || recordingRangeB is null) return;
        recordingRangeUpdating = true;
        try { recordingRangeA.Value = a; recordingRangeB.Value = b; }
        finally { recordingRangeUpdating = false; }
        DrawRecordingWaveform();
    }
    private async Task<bool> PreviewRecordingAsync(RecordingData value)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        if(desktopEnabled)return await OpenDesktopRecordingAsync(value);
        if (audio is null || busy || workspaceOperation || captureDraft is not null || recordingOperation || fileOperation || closingWindow || backupOperationRunning || profileOperation || studioOperation || studioEditTask is{IsCompleted:false} || looperOperation || powerTransition || deviceOperationRunning)
            throw new InvalidOperationException("Nejdřív dokonči práci nebo zastav nahrávání.");
        if(audio.GetLoop().State is 1 or 2 or 4 or 5)throw new InvalidOperationException("Nejdřív dokonči nahrávání vrstvy v Looperu.");
        fileOperation=true;BeginPreparation("Ověřuji nahrávku a připravuji její průběh…");
        try
        {
        if (!await new OwnedMedia(userFiles).VerifyAsync(new(value.RelativePath, value.Sha256, value.Bytes),PreparationToken)) throw new InvalidDataException("Zvukový soubor chybí nebo je změněný.");
        PreparationToken.ThrowIfCancellationRequested();
        string path = OwnedPaths.Resolve(userFiles.Root, value.RelativePath);
        await OpenSongFileAsync(path, value.Name);
        if (songLoadCancelled || mediaPath != path || PreparationToken.IsCancellationRequested) return false;
        playingRecording = value; recordingPeaks = songPeaks.ToArray();
        if (recordingRangeA is not null && recordingRangeB is not null)
        { recordingRangeA.Maximum = recordingRangeB.Maximum = value.DurationSec; SetRecordingRange(0, value.DurationSec); }
        DrawRecordingWaveform(); RefreshRecordingWaveform();return true;
        }catch(OperationCanceledException){return false;}finally{fileOperation=false;EndPreparation();}
    }
    private void DrawRecordingWaveform()
    {
        if (recordingWaveform is null) return;
        recordingWaveform.Children.Clear(); recordingPlayhead = null;
        if (playingRecording is null || recordingPeaks.Length == 0 || recordingWaveform.ActualWidth <= 0) return;
        double width = recordingWaveform.ActualWidth, duration = Math.Max(.000001, playingRecording.DurationSec);
        var accent = recordingWaveformCaption!.Foreground;
        var secondary = recordingWaveformStatus!.Foreground;
        double a = recordingRangeA?.Value ?? 0, b = recordingRangeB?.Value ?? duration;
        if (double.IsFinite(a) && double.IsFinite(b) && b > a)
        {
            double left = Math.Clamp(a / duration, 0, 1) * width, right = Math.Clamp(b / duration, 0, 1) * width;
            var selection = new Rectangle { Width = Math.Max(0, right - left), Height = 160, Fill = accent, Opacity = .12, IsHitTestVisible = false };
            Canvas.SetLeft(selection, left); recordingWaveform.Children.Add(selection);
        }
        recordingWaveform.Children.Add(new Line { X1 = 0, X2 = width, Y1 = 80, Y2 = 80, Stroke = secondary, Opacity = .4, IsHitTestVisible = false });
        var wave = new Polyline { Stroke = accent, StrokeThickness = 1, IsHitTestVisible = false };
        for (int i = 0; i < recordingPeaks.Length; ++i)
        {
            double x = i * width / Math.Max(1, recordingPeaks.Length - 1), peak = Math.Clamp(recordingPeaks[i], 0, 1) * 58;
            wave.Points.Add(new(x, 80 - peak)); wave.Points.Add(new(x, 80 + peak));
        }
        recordingWaveform.Children.Add(wave);
        foreach (var marker in playingRecording.Markers)
        {
            double x = marker.TimeSec / duration * width;
            recordingWaveform.Children.Add(new Line { X1 = x, X2 = x, Y1 = 20, Y2 = 145, Stroke = secondary, StrokeThickness = 1, IsHitTestVisible = false });
            var label = Text(marker.Label); label.FontSize = 10; label.MaxWidth = Math.Min(120, width); label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis; label.IsHitTestVisible = false;
            Canvas.SetLeft(label, Math.Clamp(x + 3, 0, Math.Max(0, width - label.MaxWidth))); recordingWaveform.Children.Add(label);
        }
        recordingPlayhead = new Line { Y1 = 0, Y2 = 160, Stroke = recordingRangeA!.Foreground, StrokeThickness = 2, IsHitTestVisible = false };
        recordingWaveform.Children.Add(recordingPlayhead); RefreshRecordingWaveform();
    }
    private void RefreshRecordingWaveform()
    {
        bool ready = RecordingWaveformReady(),available=RecordingWaveformAvailable();
        foreach (var action in recordingWaveformActions) action.IsEnabled = available;
        if (recordingRangeA is not null){recordingRangeA.IsEnabled=available;if(playingRecording is{ } recording)recordingRangeA.Maximum=recording.DurationSec;}
        if (recordingRangeB is not null){recordingRangeB.IsEnabled=available;if(playingRecording is{ } recording)recordingRangeB.Maximum=recording.DurationSec;}
        if(recordingWaveformStatus is not null && playingRecording is null)recordingWaveformStatus.Text="Vyber nahrávku a použij Zobrazit průběh nebo Přehrát. Přetažením vybereš úsek; kliknutím změníš pozici.";
        if (recordingWaveformStatus is not null && playingRecording is { } value)
            recordingWaveformStatus.Text = ready ? value.Name + " · " + FormatTime(value.DurationSec) + $" · úsek {recordingRangeA?.Value:0.##}–{recordingRangeB?.Value:0.##} s · {value.Markers.Count} značek"
                : available ? value.Name+" · průběh připravený; přehrávání převezme zdroj až po tvém příkazu." : "Průběh nahrávky zatím není připravený.";
        if (recordingPlayhead is not null && recordingWaveform is not null && playingRecording is { DurationSec: > 0 })
        {
            recordingPlayhead.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
            if (ready) recordingPlayhead.X1 = recordingPlayhead.X2 = Math.Clamp(audio!.GetTransport().PositionSeconds * currentSpeed / playingRecording.DurationSec, 0, 1) * recordingWaveform.ActualWidth;
        }
    }
}
