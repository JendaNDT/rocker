// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using Rocker.Notation;

if (args.Length != 2) throw new ArgumentException("Supply fixture and report directories.");
var checks = 0;
void Check(bool valid, string description)
{
    if (!valid) throw new Exception(description);
    checks++;
}
var evidence = new List<object>();
foreach (var sample in new[] { (Name: "notation-scale.musicxml", Bars: 1, Notes: 4),
                              (Name: "notation-phrases.gp", Bars: 2, Notes: 8) })
{
    var preview = ScorePreview.Load(File.ReadAllBytes(Path.Combine(args[0], sample.Name)));
    Check(preview.TrackCount == 1 && preview.BarCount == sample.Bars, "Imported structure");
    Check(preview.NoteOnCount == sample.Notes, "MIDI note events");
    Check(preview.Midi.AsSpan(0, 4).SequenceEqual("MThd"u8), "Standard MIDI header");
    Check(preview.Png.AsSpan(0, 8).SequenceEqual(new byte[] {137, 80, 78, 71, 13, 10, 26, 10}), "PNG header");
    using var image = AlphaSkia.AlphaSkiaImage.Decode(preview.Png);
    Check(image is not null && image.Width == preview.Width && image.Height == preview.Height, "Native PNG decode and dimensions");
    var pixels = image!.ReadPixels();
    Check(pixels is not null && pixels.Distinct().Count() > 16, "Rendered image contains ink, not a blank canvas");
    var found = new HashSet<int>();
    for (var y = 0; y < preview.Height; y += 5)
        for (var x = 0; x < preview.Width; x += 5)
            if (preview.HitTest(x, y) is { } bar)
            {
                if (found.Add(bar.Number))
                    Check(bar.EndTick > bar.StartTick && bar.Width > 0 && bar.Height > 0, "Selected bar has geometry and timing");
            }
    Check(found.Count == sample.Bars, "Every rendered bar can be selected by position");
    Check(preview.HitTest(-1, 0) is null && preview.HitTest(double.NaN, 0) is null
        && preview.HitTest(preview.Width, preview.Height) is null, "Invalid positions have no selection");
    Directory.CreateDirectory(args[1]);
    File.WriteAllBytes(Path.Combine(args[1], sample.Name + ".png"), preview.Png);
    File.WriteAllBytes(Path.Combine(args[1], sample.Name + ".mid"), preview.Midi);
    evidence.Add(new { sample.Name, preview.TrackCount, preview.BarCount, preview.NoteOnCount,
        preview.Width, preview.Height, selectedBars = found.Order().ToArray() });
}
foreach (var bytes in new[] { Array.Empty<byte>(), new byte[] { 1, 2, 3, 4 }, new byte[ScorePreview.MaximumFileBytes + 1] })
{
    bool rejected = false;
    try { _ = ScorePreview.Load(bytes); }
    catch (Exception e) when (e is InvalidDataException or AlphaTab.AlphaTabError) { rejected = true; }
    Check(rejected, "Invalid or oversized file rejected");
}
File.WriteAllText(Path.Combine(args[1], "notation-checks.json"), JsonSerializer.Serialize(new {
    result = "passed", checks, os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    alphaTab = "1.8.3", alphaSkia = "3.4.135", samples = evidence,
    audioPlaybackVerified = false
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS: {checks} assertions; MusicXML/GP import, native rendering, bar hit testing and MIDI events. Audio synthesis not tested.");
