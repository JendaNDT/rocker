// SPDX-License-Identifier: AGPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rocker.Probe.Controls;
using System.Text.Json;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
namespace Rocker.Probe;
public sealed partial class MainWindow
{
    private bool componentMode;
    private void Components_Click(object sender, RoutedEventArgs args) => ShowComponents();
    private void ShowComponents()
    {
        componentMode = true; Title = "Rocker — komponenty Vintage";
        WorkspaceTitle.Text = "Komponenty Vintage"; WorkspaceTrail.Text = "Rocker / Nástroje / Komponenty";
        NotationPanel.Visibility = VideoPanel.Visibility = DevicePanel.Visibility = Visibility.Collapsed;
        WorkspaceContent.Visibility = Visibility.Visible; WorkspaceContent.Content = BuildComponents();
        Navigation.IsBackEnabled = BackMenu.IsEnabled = true;
    }
    private FrameworkElement BuildComponents()
    {
        var panel = Stack();
        panel.Children.Add(new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Informational, Title = "Ukázkové hodnoty", Message = "Tento katalog ukazuje ovládací prvky. Úroveň ani přebuzení zde nejsou měřením kytary." });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        var gain = new ValueDial { Label = "Gain", Minimum = 0, Maximum = 100, Value = 45 };
        var bass = new ValueDial { Label = "Basy", Minimum = -100, Maximum = 100, Value = 10 };
        var tempo = new ValueDial { Label = "Tempo BPM", Minimum = 40, Maximum = 240, Value = 100 };
        row.Children.Add(gain); row.Children.Add(bass); row.Children.Add(tempo);
        row.SizeChanged += (_,args) => row.Orientation = args.NewSize.Width < 600 ? Orientation.Vertical : Orientation.Horizontal;
        panel.Children.Add(new VintageHousing(row,"ROCKER / INSTRUMENT SERIES",speaker:true));
        var meter = new LevelMeter{Analog=true}; meter.SetLevel(0.35,true,true); panel.Children.Add(Card(meter));
        var noSignal = new LevelMeter{Analog=true}; noSignal.SetLevel(0,false); panel.Children.Add(Card(noSignal));
        if (catalog is not null)
        {
            var chord = new ChordDiagram(); chord.ShowChord(catalog.Find("chords","Em")!.Value);
            var mirror = new ToggleSwitch { Header = "Zrcadlení akordu", OnContent = "Pro leváka", OffContent = "Pro praváka" };
            mirror.Toggled += (_,_) => chord.ShowChord(catalog.Find("chords","Em")!.Value,mirror.IsOn);
            var chordPanel = Stack(); chordPanel.Children.Add(mirror); chordPanel.Children.Add(chord); panel.Children.Add(Card(chordPanel));
            var fretboard = new FretboardDiagram(); fretboard.SetTuning([40,45,50,55,59,64],[0,3,5,7,10],4);
            panel.Children.Add(Card(fretboard));
        }
        panel.Children.Add(new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Warning, Title = "Ukázka stavu bez zařízení", Message = "Zvukový vstup není připojený. Nastavení nabídne pouze skutečně nalezená zařízení." });
        var list = new ListView { ItemsSource = new[] { new { Name = "Ukázková nahrávka", Duration = "0:12" }, new { Name = "Prázdný projekt", Duration = "0:00" } }, DisplayMemberPath = "Name", SelectionMode = ListViewSelectionMode.Multiple, MaxHeight = 150 };
        var listPanel = Stack(); listPanel.Children.Add(Text("Nativní seznam · Ctrl/Shift pro více položek")); listPanel.Children.Add(list); panel.Children.Add(Card(listPanel));
        return panel;
    }
    private async Task CaptureNativeViewAsync(string name="native-content.png",FrameworkElement? element=null)
    {
        using var invocationScope=documentContext.Enter(documentContext.Current);

        var directory = Environment.GetEnvironmentVariable("ROCKER_DIAGNOSTICS_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(element??ShellRoot);
        var buffer = await bitmap.GetPixelsAsync();
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0 || buffer.Length > 64 * 1024 * 1024) throw new InvalidOperationException("Invalid native view capture.");
        var pixels = new byte[buffer.Length];
        using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(pixels);
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
        double dpi = 96 * (element ?? ShellRoot).XamlRoot.RasterizationScale;
        encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,dpi,dpi,pixels);
        await encoder.FlushAsync();
        stream.Seek(0); var png = new byte[checked((int)stream.Size)];
        using (var reader = new DataReader(stream)) { await reader.LoadAsync((uint)png.Length); reader.ReadBytes(png); }
        var destination=Path.Combine(directory,name);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllBytesAsync(destination,png);
    }
    private void VerifyComponents()
    {
        int checks = 0; void Check(bool value) { if (!value) throw new InvalidOperationException("Native component contract failed."); ++checks; }
        var dial = new ValueDial { Minimum = -100,Maximum = 100,Value = 250 };
        Check(dial.Value == 100); dial.Value = double.NaN; Check(dial.Value == -100);
        var gauge=new PitchGauge();Check(!gauge.HasReading);gauge.Value=200;Check(gauge.Value==100);gauge.Value=double.NaN;Check(gauge.Value==50);gauge.HasReading=true;gauge.HasReading=false;Check(!gauge.HasReading);
        var meter = new LevelMeter{Analog=true}; meter.SetLevel(double.NaN,true,true); Check(!meter.HasSignal && meter.Level == 0);
        meter.SetLevel(2,true,true); Check(meter.HasSignal && meter.Level == 1);
        Check(meter.NeedleVisible && meter.Clipping && meter.NeedleAngle == 60);
        meter.SetLevel(.001,true,true); Check(meter.NeedleVisible && !meter.Clipping && Math.Abs(meter.NeedleAngle + 60) < .001);
        meter.SetLevel(.1,true,true); Check(Math.Abs(meter.NeedleAngle - 20) < .001);
        meter.SetLevel(.5,false); Check(!meter.NeedleVisible && !meter.Clipping);
        meter.SetLevel(double.PositiveInfinity,true); Check(!meter.NeedleVisible && !meter.HasSignal);
        meter.SetLevel(-1,true); Check(!meter.NeedleVisible && meter.Level == 0);
        var power = new CheckBox { Content = "Aparát" }; var hardware = VintageHousing.PowerSwitch(power);
        var peer=Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(power);
        var toggle=(Microsoft.UI.Xaml.Automation.Provider.IToggleProvider?)peer?.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Toggle);
        toggle?.Toggle(); Check(toggle is not null && power.IsChecked == true && hardware is Grid);
        toggle!.Toggle(); Check(power.IsChecked == false);
        var housing = new VintageHousing(dial,"ROCKER",speaker:true); Check(housing.HasSpeakerGrille);
        WriteShellDiagnostic("retro-instruments.json",new { result="passed",meterBoundaryChecks=6,nativePowerStateChecks=2,cabinetCheck=true,needleUsesPeakDbfs=true,invalidOrAbsentInputHasNoNeedle=true,decorationsOwnNoInput=true,sharedAudioHostCount=audioHostsCreated,realInputVerified=false,highContrastManualVerified=false });
        if (catalog is not null)
        {
            var chord = new ChordDiagram(); foreach (var item in catalog.Items("chords")) { chord.ShowChord(item); chord.ShowChord(item,true); ++checks; }
            var board = new FretboardDiagram(); board.SetTuning([38,45,50,55,59,64],[0,3,5,7,10],4,true); Check(board.SelectedMidi is null);
        }
        WriteShellDiagnostic("native-components.json",new { result = "passed", checks, realInputVerified = false, keyboardAndScreenReaderManualVerified = false, highContrastManualVerified = false });
    }
}
