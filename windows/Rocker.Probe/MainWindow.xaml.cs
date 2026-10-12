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
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private NativeAudio? audio;
    private ScorePreview? preview;
    private bool closed;
    private bool busy, updatingPosition, positionDragging;
    private int playbackIntent;
    private string? mediaPath;
    private byte[]? mediaMidi;
    private double currentSpeed = 1;
    private bool initialNotationStarted;
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 940));
        InitializeShell();
        InitializeNativeEvents();InitializePracticeTracking();InitializeFileCommands();
        PositionSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, args) => { if (args.GetCurrentPoint(PositionSlider).Properties.IsLeftButtonPressed) positionDragging = true; }), true);
        PositionSlider.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(Position_Released), true);
        VideoSeek.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler((_,args)=>{if(args.GetCurrentPoint(VideoSeek).Properties.IsLeftButtonPressed)videoSeeking=true;}),true);
        VideoSeek.AddHandler(UIElement.PointerCaptureLostEvent,new PointerEventHandler(VideoSeek_Released),true);
        Closed += (_, _) => { closed = true;DetachNativeEvents(); timer.Stop(); audio?.Dispose(); audio = null; VideoView.Close(); };
        Activated += async (_, _) =>
        {
            if (initialNotationStarted) return;
            initialNotationStarted = true;
            await InitializePersistenceAsync();
            if (closed) return;
            if (!closed && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR"))) { await LoadScoreAsync(Path.Combine(AppContext.BaseDirectory, "fixtures", "notation-phrases.gp"));await VerifyShellAsync(); }
            else NotationStatus.Text="Otevři Guitar Pro nebo MusicXML. Noty fungují bez internetu.";
        };
        timer.Tick += (_, _) => RefreshState();
        try
        {
            audio = new NativeAudio();
            ++audioHostsCreated;
            StatusText.Text = "Připraveno. Testovací tón je vypnutý.";
            timer.Start();
        }
        catch (Exception error) { StartButton.IsEnabled = false; ShowError(error);timer.Start(); }
    }
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if(powerTransition)return;
        try { ++playbackIntent; SetBusy(true); if(audio is not null)await audio.StartAsync(); if(!closingWindow && !closed)RefreshState(); }
        catch (Exception error) { ShowError(error); }
        finally { if(!closed)SetBusy(false); }
    }
    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        try { SendVideo(new{op="pause"});++playbackIntent; metroEnabled=false;++metroRevision;metroApplyTimer.Stop();if(audio is not null){await audio.StopAsync();await StopCaptureAsync();await ApplyMetronomeAsync();} if(!closingWindow && !closed)RefreshState(); }
        catch (Exception error) { ShowError(error); }
    }
    private void RefreshState()
    {
        ProcessFileActivation();
        if(audio is null){RefreshPracticeWithoutAudio();return;}
        try
        {
            var state = audio.GetSnapshot();
            var transport = audio.GetTransport();
            StartButton.IsEnabled = !deviceSelectionUnavailable && !deviceOperationRunning && !powerTransition && !busy && state.State != AudioState.Running;
            PlayMediaButton.IsEnabled = !deviceSelectionUnavailable && !deviceOperationRunning && !powerTransition && !busy && transport.Ready != 0;
            PlayMenu.IsEnabled = PlayMediaButton.IsEnabled && transport.Playing == 0;
            string playLabel = transport.Playing != 0 ? "Pozastavit" : "Přehrát";
            if (PlayMediaButton.Label != playLabel) { PlayMediaButton.Label = playLabel; PlayMediaButton.Icon = new SymbolIcon(transport.Playing != 0 ? Symbol.Pause : Symbol.Play); }
            RewindButton.IsEnabled = PositionSlider.IsEnabled = !busy && transport.Ready != 0;
            SpeedBox.IsEnabled = !studioOwnsPlayback && !busy && transport.Ready != 0;
            PlayScoreButton.IsEnabled = !deviceSelectionUnavailable && !deviceOperationRunning && !powerTransition && !busy && preview is not null;
            if (!positionDragging)
            {
                updatingPosition = true;
                PositionSlider.Maximum = Math.Max(1e-6, transport.DurationSeconds);
                PositionSlider.Value = Math.Min(transport.PositionSeconds, PositionSlider.Maximum);
                updatingPosition = false;
            }
            PositionText.Text = $"{FormatTime(transport.PositionSeconds)} / {FormatTime(transport.DurationSeconds)}";
            StopButton.IsEnabled = state.State != AudioState.Stopped;
            string status = state.State switch
            {
                AudioState.Running => state.Reserved==8 ? "Zvuková ukázka." : state.Reserved==6 ? "Looper." : state.Reserved==5 ? "Nahrávání; monitoring se ovládá samostatně." : state.Reserved==3 ? "Metronom." : state.Reserved==2 ? "Živý vstup. Monitoring se ovládá samostatně." : transport.Playing != 0 ? "Přehrávání." : state.Reserved==1 ? "Skladba pozastavena; zvukové zařízení běží." : "Hraje testovací tón.",
                AudioState.Starting => "Spouštím zvukové zařízení…",
                AudioState.Reconfiguring => "Měním nastavení zvuku…",
                AudioState.Stopping or AudioState.Closing => "Uvolňuji zvukové zařízení…",
                AudioState.Faulted => "Zvuk se zastavil kvůli chybě zařízení. " + audio.GetLastError(),
                _ => "Zastaveno. Zařízení je uvolněné."
            };
            if (StatusText.Text != status) { StatusText.Text = status; ToolTipService.SetToolTip(StatusText, status); if (state.State == AudioState.Faulted) { ShellNotice.IsOpen = true; ShellNotice.Severity = InfoBarSeverity.Error; ShellNotice.Message = status; } }
            RefreshLive(state);
            RefreshMetronome(state);
            var analysis=audio.GetAnalysis();RefreshTuner(analysis);RefreshLesson(analysis);RefreshSurgical(analysis);RefreshPalmMute(analysis);RefreshRhythm();RefreshNotation(transport);
            RefreshPractice(audio.GetClock(),transport,analysis);
            RefreshAudioDiagnostics();
            RefreshSong(transport);
            RefreshRecording();
            RefreshLooper();
            RefreshWorkspaceChrome();
            DeviceText.Text = state.State == AudioState.Running
                ? $"Vzorkování {state.SampleRate:N0} Hz · buffer {state.BufferFrames} vzorků · výstupy {state.OutputChannels}\nZpracováno {state.RenderedFrames:N0} vzorků."
                : "Zařízení zatím není otevřené.";
        }
        catch (Exception error) { timer.Stop(); ShowError(error); }
    }
    private void ShowError(Exception error)
    {
        StatusText.Text = "Zvuk není dostupný. " + error.Message;
        ToolTipService.SetToolTip(StatusText, StatusText.Text);
        ShellNotice.IsOpen = true; ShellNotice.Severity = InfoBarSeverity.Error; ShellNotice.Message = StatusText.Text;
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
            if (file is not null && !closed) { await OpenWorkspaceFileAsync(file.Path); }
        }
        catch (Exception error) { if (!closed) NotationStatus.Text = "Soubor nelze otevřít. " + error.Message; }
    }

    private async Task LoadScoreAsync(string path)
    {
        if(busy || closed)return;SetBusy(true);var previous=preview;NotationStatus.Text="Načítám a vykresluji noty…";
        try
        {
            var result=await Task.Run(()=>{using var file=File.OpenRead(path);if(file.Length>ScorePreview.MaximumFileBytes)throw new InvalidDataException("Soubor přesahuje limit 4 MB.");var bytes=new byte[(int)file.Length];file.ReadExactly(bytes);return ScoreDocument.Load(bytes).Render();});if(closed)return;
            await SetNotationImageAsync(result);notationSourcePath=path;RememberFile(path);SetNotationDocument(result.Document,Path.GetFileName(path));SelectionStatus.Text="Vyber takt kliknutím nebo jeho číslem.";NotationStatus.Text=$"{notationName} · stopy {result.TrackCount} · takty {result.BarCount} · zobrazená stopa {result.Options.Track+1}";ExportMidiButton.IsEnabled=ExportMidiMenu.IsEnabled=true;bool prepared=false;
            if(audio is not null)try{if(await StopCaptureAsync()){await audio.PauseAsync();await PrepareNotationAsync(result);MediaStatus.Text="Noty jsou připravené. Přehrávání spustíš tlačítkem.";prepared=audio.GetTransport().Ready!=0;}}catch(Exception error){if(!closed)MediaStatus.Text="Noty se zobrazily; zvuk nelze připravit. "+error.Message;}
            if(!closed)WriteNotationReport(new{result="passed",bitmapDecoded=true,result.Width,result.Height,result.BarCount,result.NoteOnCount,midiPreparedInCommonEngine=prepared,audioPlaybackVerified=false});
        }
        catch(Exception error){if(closed)return;preview=previous;NotationStatus.Text="Noty nelze načíst; předchozí partitura je zachovaná. "+error.Message;}
        finally{if(!closed)SetBusy(false);}
    }
    private void Score_PointerPressed(object sender,PointerRoutedEventArgs args)
    {
        var point=args.GetCurrentPoint(ScoreCanvas).Position;var bar=preview?.HitTest(point.X,point.Y);if(bar is null)return;notationUpdating=true;try{NotationBar.Value=bar.Number;}finally{notationUpdating=false;}MarkNotationBar(bar.Number);
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

    private void SetBusy(bool value)
    {
        busy = value;
        foreach(var control in NotationControls.Children.OfType<Control>())control.IsEnabled=!value && notationDocument is not null;
        XmlButton.IsEnabled = GpButton.IsEnabled = OpenScoreButton.IsEnabled = OpenScoreMenu.IsEnabled =
            OpenMediaButton.IsEnabled = OpenMediaMenu.IsEnabled = !value;
        if (value)
        {
            PlayMediaButton.IsEnabled = PlayMenu.IsEnabled = PlayScoreButton.IsEnabled =
                StartButton.IsEnabled = SpeedBox.IsEnabled = RewindButton.IsEnabled = PositionSlider.IsEnabled = false;
        }
        if (!closed) RefreshState();
    }
    private async void OpenMedia_Click(object sender, RoutedEventArgs e)
    {
        if (busy || audio is null) return;
        try
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            foreach (var extension in new[] { ".wav", ".mp3", ".ogg", ".opus", ".webm", ".m4a", ".mp4" }) picker.FileTypeFilter.Add(extension);
            var file = await picker.PickSingleFileAsync();
            if (file is null || closed || busy) return;
            Navigate(new("07"));
            await OpenSongFileAsync(file.Path,file.Name);
        }
        catch (Exception error) { if (!closed) MediaStatus.Text = "Nahrávku nelze otevřít. " + error.Message; }
        finally { if (!closed) SetBusy(false); }
    }
    private async void PlayMedia_Click(object sender, RoutedEventArgs e)
    {
        if (busy || audio is null) return;
        try { ++playbackIntent; SetBusy(true); await audio.PlayAsync(); if(!closingWindow && !closed)RefreshState(); }
        catch (Exception error) { ShowError(error); }
        finally { if(!closed)SetBusy(false); }
    }
    private async void PlayScore_Click(object sender, RoutedEventArgs e)
    {
        if (busy || audio is null || preview is null) return;
        var intent = ++playbackIntent;
        var current = preview;
        try
        {
            SetBusy(true);
            MediaStatus.Text = "Připravuji přehrávání not…";
            if(!await StopCaptureAsync())return;
            await PrepareNotationAsync(current);
            if(closed)return;
            if (intent == playbackIntent) await audio.PlayAsync();
            MediaStatus.Text = "Noty jsou připravené ve společném přehrávači.";
        }
        catch (Exception error) { if (!closed) MediaStatus.Text = "Noty nelze přehrát. " + error.Message; }
        finally { if (!closed) SetBusy(false); }
    }
    private async void Rewind_Click(object sender, RoutedEventArgs e)
    {
        try { ++playbackIntent; if(audio is not null)await audio.SeekAsync(0); RefreshState(); }
        catch (Exception error) { ShowError(error); }
    }
    private async void Speed_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (audio is null || busy || SpeedBox.SelectedItem is not ComboBoxItem item) return;
        var speed = double.Parse((string)item.Tag, System.Globalization.CultureInfo.InvariantCulture);
        if (speed == currentSpeed || (mediaPath is null && mediaMidi is null)) return;
        try { await ChangeSongSpeedAsync(speed); }
        catch(Exception error){ShowError(error);}
    }

    private void Volume_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (audio is null) return;
        try { audio.SetVolume(e.NewValue / 100); }
        catch (Exception error) { ShowError(error); }
    }
    private void Position_Released(object sender, PointerRoutedEventArgs e)
    {
        if (!positionDragging) return;
        positionDragging = false;
        SeekPosition();
    }
    private static bool IsSeekKey(Windows.System.VirtualKey key) => key is
        Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Up or
        Windows.System.VirtualKey.Down or Windows.System.VirtualKey.Home or Windows.System.VirtualKey.End or
        Windows.System.VirtualKey.PageUp or Windows.System.VirtualKey.PageDown;
    private void Position_KeyDown(object sender, KeyRoutedEventArgs e) { if (IsSeekKey(e.Key)) positionDragging = true; }
    private void Position_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (!IsSeekKey(e.Key)) return;
        positionDragging = false; SeekPosition();
    }
    private async void SeekPosition()
    {
        if (updatingPosition || busy || audio is null || !PositionSlider.IsEnabled) return;
        try { ++playbackIntent; await audio.SeekAsync(PositionSlider.Value); RefreshState(); }
        catch (Exception error) { ShowError(error); }
    }
    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

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
