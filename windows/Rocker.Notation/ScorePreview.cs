// SPDX-License-Identifier: AGPL-3.0-only
using AlphaTab;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using AlphaTab.Midi;
using AlphaTab.Rendering;
using AlphaTab.Rendering.Utils;
using SkiaImage = AlphaTab.Platform.Skia.AlphaSkiaBridge.AlphaSkiaImage;

namespace Rocker.Notation;

public sealed record BarSelection(int Number, double X, double Y, double Width,
    double Height, double StartTick, double EndTick);

// Native Skia is used only while rendering. The preview retains managed hit/timing data.
public sealed class ScorePreview
{
    public const int MaximumFileBytes = 4 * 1024 * 1024;
    private static readonly Lazy<IList<string>> portableFont = new(() =>
    {
        AlphaSkia.AlphaSkiaCanvas.SwitchToFreeTypeFonts();
        using var input = typeof(ScorePreview).Assembly.GetManifestResourceStream("Rocker.Notation.fonts.NotoSans-Regular.otf")!;
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        return AlphaTab.Environment.RegisterAlphaSkiaCustomFont(new Uint8Array(buffer.ToArray())).Families;
    });
    private readonly BoundsLookup bounds;
    private readonly MidiTickLookup ticks;
    public byte[] Png { get; }
    public byte[] Midi { get; }
    public string Title { get; }
    public int TrackCount { get; }
    public int BarCount { get; }
    public int NoteOnCount { get; }
    public int Width { get; }
    public int Height { get; }

    private ScorePreview(byte[] png, byte[] midi, string title, int tracks, int bars,
        int notes, int width, int height, BoundsLookup bounds, MidiTickLookup ticks)
    {
        Png = png; Midi = midi; Title = title; TrackCount = tracks; BarCount = bars;
        NoteOnCount = notes; Width = width; Height = height;
        this.bounds = bounds; this.ticks = ticks;
    }

    public BarSelection? HitTest(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || y < 0 || x >= Width || y >= Height)
            return null;
        var beat = bounds.GetBeatAtPos(x, y);
        if (beat is null) return null;
        var bar = beat.Voice.Bar.MasterBar;
        var box = bounds.FindMasterBar(bar)?.RealBounds;
        if (box is null || x < box.X || y < box.Y || x > box.X + box.W || y > box.Y + box.H)
            return null;
        var time = ticks.GetMasterBar(bar);
        return new BarSelection((int)bar.Index + 1, box.X, box.Y, box.W, box.H, time.Start, time.End);
    }

    public static ScorePreview Load(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumFileBytes)
            throw new InvalidDataException("Notový soubor je prázdný nebo přesahuje limit 4 MB prototypu.");
        ValidateZipSize(bytes);
        // AlphaSkia OS font support is Windows/macOS only; Linux checks use FreeType.
        var settings = new Settings();
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            settings.Display.Resources.TablatureFont.Families = portableFont.Value;
            foreach (var font in settings.Display.Resources.ElementFonts.Values())
                font.Families = portableFont.Value;
        }
        settings.Core.Engine = "skia";
        settings.Core.UseWorkers = false;
        settings.Core.EnableLazyLoading = false;
        settings.Display.BarCount = 12;
        var score = ScoreLoader.LoadScoreFromBytes(new Uint8Array(bytes), settings);
        if (score.Tracks.Count is < 1 or > 16 || score.MasterBars.Count is < 1 or > 128)
            throw new InvalidDataException("Prototyp podporuje 1–16 stop a nejvýše 128 taktů.");
        var midi = new MidiFile();
        var generator = new MidiFileGenerator(score, settings, new AlphaSynthMidiFileHandler(midi, true));
        generator.Generate();
        var chunks = new List<RenderFinishedEventArgs>();
        var renderer = new ScoreRenderer(settings) { Width = 700 };
        Exception? failure = null;
        RenderFinishedEventArgs? finished = null;
        renderer.Error.On(error => failure = error);
        renderer.PartialRenderFinished.On(chunk => chunks.Add(chunk));
        renderer.RenderFinished.On(result => finished = result);
        try
        {
            renderer.RenderScore(score, new double[] { 0 }, null);
            if (failure is not null) throw new InvalidDataException("Noty nelze vykreslit.", failure);
            if (finished is null || renderer.BoundsLookup is not { IsFinished: true } lookup)
                throw new InvalidDataException("Vykreslení not nebylo dokončeno.");
            int width = checked((int)Math.Ceiling(finished.TotalWidth));
            int height = checked((int)Math.Ceiling(finished.TotalHeight));
            if (width < 1 || height < 1 || (long)width * height > 12_000_000)
                throw new InvalidDataException("Notový náhled přesahuje limit velikosti prototypu.");
            using var canvas = new AlphaSkia.AlphaSkiaCanvas();
            canvas.BeginRender(width, height, 1);
            canvas.Color = AlphaSkia.AlphaSkiaCanvas.RgbaToColor(255, 255, 255, 255);
            canvas.FillRect(0, 0, width, height);
            foreach (var chunk in chunks)
                if (chunk.RenderResult is SkiaImage image)
                    canvas.DrawImage(image.Image, (float)chunk.X, (float)chunk.Y, (float)chunk.Width, (float)chunk.Height);
            using var rendered = canvas.EndRender() ?? throw new InvalidDataException("Prázdný výsledek vykreslení.");
            var png = rendered.ToPng() ?? throw new InvalidDataException("Notový obraz nelze převést do PNG.");
            return new ScorePreview(png, midi.ToBinary().Buffer.Raw,
                score.Title, score.Tracks.Count, score.MasterBars.Count,
                midi.Events.Count(e => e is NoteOnEvent), width, height, lookup, generator.TickLookup);
        }
        finally
        {
            foreach (var chunk in chunks)
                if (chunk.RenderResult is SkiaImage image) image.Dispose();
            if (finished?.RenderResult is SkiaImage finalImage) finalImage.Dispose();
            renderer.Destroy();
        }
    }

    private static void ValidateZipSize(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 'P' || bytes[1] != 'K') return;
        using var stream = new MemoryStream(bytes, false);
        using var zip = new System.IO.Compression.ZipArchive(stream);
        if (zip.Entries.Count > 256 || zip.Entries.Sum(e => e.Length) > 20 * 1024 * 1024)
            throw new InvalidDataException("Zabalený notový soubor přesahuje limit prototypu.");
    }
}
