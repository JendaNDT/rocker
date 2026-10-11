// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Rocker.Interop;

namespace Rocker.Probe;
public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private NativeAudio? audio;
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(820, 620));
        Closed += (_, _) => { timer.Stop(); audio?.Dispose(); audio = null; };
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
}
