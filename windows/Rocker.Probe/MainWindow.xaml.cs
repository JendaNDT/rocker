// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Rocker.Interop;
using Rocker.Notation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace Rocker.Probe;
public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private NativeAudio? audio;
    private ScorePreview? preview;
    private bool closed;
    private bool initialNotationStarted;
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 900));
        Closed += (_, _) => { closed = true; timer.Stop(); audio?.Dispose(); audio = null; };
        Activated += async (_, _) =>
        {
            if (initialNotationStarted) return;
            initialNotationStarted = true;
            await LoadScoreAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "notation-phrases.gp"));
        };
        timer.Tick += (_, _) => RefreshState();
        try
        {
            audio = new NativeAudio();
            StatusText.Text = "Připraveno. Testovací tón je vypnutý.";
            timer.Start();
        }
        catch (Exception error) { StartButton.IsEnabled = false; ShowError(error); }
    }
    private void Start_Click(object sender, RoutedEventArgs e)
    {
        try { audio?.Start(); RefreshState(); }
        catch (Exception error) { ShowError(error); }
    }
    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        try { audio?.Stop(); RefreshState(); }
        catch (Exception error) { ShowError(error); }
    }
    private void RefreshState()
    {
        if (audio is null) return;
        try
        {
            var state = audio.GetSnapshot();
            StartButton.IsEnabled = state.State != AudioState.Running;
            StopButton.IsEnabled = state.State != AudioState.Stopped;
            StatusText.Text = state.State switch
            {
                AudioState.Running => "Hraje testovací tón.",
                AudioState.Faulted => "Zvuk se zastavil kvůli chybě zařízení. " + audio.GetLastError(),
                _ => "Zastaveno. Zařízení je uvolněné."
            };
            DeviceText.Text = state.State == AudioState.Running
                ? $"Vzorkování {state.SampleRate:N0} Hz · buffer {state.BufferFrames} vzorků · výstupy {state.OutputChannels}\nZpracováno {state.RenderedFrames:N0} vzorků."
                : "Zařízení zatím není otevřené.";
        }
        catch (Exception error) { timer.Stop(); ShowError(error); }
    }
    private void ShowError(Exception error)
    {
        StatusText.Text = "Zvuk není dostupný. " + error.Message;
        StopButton.IsEnabled = audio is not null;
    }

    private async void Xml_Click(object sender, RoutedEventArgs e) =>
        await LoadScoreAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "notation-scale.musicxml"));
    private async void Gp_Click(object sender, RoutedEventArgs e) =>
        await LoadScoreAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "notation-phrases.gp"));
    private async void OpenScore_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            foreach (var extension in new[] { ".gp", ".gp3", ".gp4", ".gp5", ".gpx", ".musicxml", ".xml", ".mxl" })
                picker.FileTypeFilter.Add(extension);
            var file = await picker.PickSingleFileAsync();
            if (file is not null && !closed) await LoadScoreAsync(file.Path);
        }
        catch (Exception error) { if (!closed) NotationStatus.Text = "Soubor nelze otevřít. " + error.Message; }
    }

    private async Task LoadScoreAsync(string path)
    {
        XmlButton.IsEnabled = GpButton.IsEnabled = OpenScoreButton.IsEnabled = ExportMidiButton.IsEnabled = false;
        preview = null;
        NotationStatus.Text = "Načítám a vykresluji noty…";
        try
        {
            var result = await Task.Run(() =>
            {
                using var file = File.OpenRead(path);
                if (file.Length > ScorePreview.MaximumFileBytes) throw new InvalidDataException("Soubor přesahuje limit 4 MB prototypu.");
                var bytes = new byte[(int)file.Length];
                file.ReadExactly(bytes);
                return ScorePreview.Load(bytes);
            });
            if (closed) return;
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(result.Png);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            if (closed) return;
            preview = result;
            ScoreImage.Source = bitmap;
            ScoreImage.Height = ScoreCanvas.Height = result.Height;
            SelectionBox.Visibility = Visibility.Collapsed;
            SelectionStatus.Text = "Takt zatím není vybraný. Klikni do notového řádku.";
            NotationStatus.Text = $"{Path.GetFileName(path)} · stopy {result.TrackCount} · takty {result.BarCount} · MIDI noty {result.NoteOnCount}";
            ExportMidiButton.IsEnabled = true;
            WriteNotationReport(new { result = "passed", bitmapDecoded = true, result.Width, result.Height,
                result.BarCount, result.NoteOnCount, audioPlaybackVerified = false });
        }
        catch (Exception error)
        {
            if (closed) return;
            preview = null;
            ScoreImage.Source = null;
            SelectionBox.Visibility = Visibility.Collapsed;
            NotationStatus.Text = "Noty nelze načíst. " + error.Message;
            SelectionStatus.Text = "Takt zatím není vybraný.";
            WriteNotationReport(new { result = "failed", error = error.ToString() });
        }
        finally
        {
            if (!closed) XmlButton.IsEnabled = GpButton.IsEnabled = OpenScoreButton.IsEnabled = true;
        }
    }

    private void Score_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(ScoreCanvas).Position;
        var bar = preview?.HitTest(point.X, point.Y);
        if (bar is null) { SelectionBox.Visibility = Visibility.Collapsed; SelectionStatus.Text = "Takt zatím není vybraný."; return; }
        Canvas.SetLeft(SelectionBox, bar.X); Canvas.SetTop(SelectionBox, bar.Y);
        SelectionBox.Width = bar.Width; SelectionBox.Height = bar.Height;
        SelectionBox.Visibility = Visibility.Visible;
        SelectionStatus.Text = $"Takt {bar.Number} · MIDI tick {bar.StartTick:N0} až {bar.EndTick:N0}. Zvukové přehrávání not zatím není propojené.";
    }

    private async void ExportMidi_Click(object sender, RoutedEventArgs e)
    {
        var current = preview;
        if (current is null) return;
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = "rocker-noty" };
            picker.FileTypeChoices.Add("MIDI", new[] { ".mid" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSaveFileAsync();
            if (file is not null && !closed)
            {
                await Windows.Storage.FileIO.WriteBytesAsync(file, current.Midi);
                if (!closed) NotationStatus.Text = "MIDI soubor byl uložen: " + file.Name;
            }
        }
        catch (Exception error) { if (!closed) NotationStatus.Text = "MIDI nelze uložit. " + error.Message; }
    }

    private static void WriteNotationReport(object report)
    {
        var directory = Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "notation-view.json");
        File.WriteAllText(destination + ".tmp", System.Text.Json.JsonSerializer.Serialize(report));
        File.Move(destination + ".tmp", destination, true);
    }
}
