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
                              (Name: "notation-phrases.gp", Bars: 2, Notes: 8),
                              (Name: "notation-original.gp3", Bars: 1, Notes: 4),
                              (Name: "notation-original.gp4", Bars: 1, Notes: 4),
                              (Name: "notation-original.gp5", Bars: 1, Notes: 4),
                              (Name: "notation-original.gpx", Bars: 2, Notes: 8),
                              (Name: "notation-original.mxl", Bars: 1, Notes: 4) })
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
var multi=ScoreDocument.Load(File.ReadAllBytes(Path.Combine(args[0],"notation-multitrack.musicxml")));Check(multi.Tracks.Count==3 && multi.BarCount==4,"authored multitrack score imports every instrument");Check(multi.Playback.Select(b=>b.Number).SequenceEqual(new[]{1,2,1,2,3,4}),"repeat expansion follows the score's actual playback order");Check(Math.Abs(multi.Playback[0].EndSeconds-2)<1e-6 && Math.Abs(multi.Playback[1].EndSeconds-14.0/3)<1e-6,"tempo map converts MIDI ticks into exact source seconds");
Check(multi.LoopSeconds(1,2,1)==(multi.Playback[2].StartSeconds,multi.Playback[3].EndSeconds),"loop occurrence selects the second traversal of repeated bars");
try{multi.LoopSeconds(1,3);throw new Exception("Repeat jump accepted as a contiguous range");}catch(InvalidOperationException){Check(true,"range crossing a repeat jump is explicitly rejected");}
for(int track=0;track<3;++track){var view=multi.Render(new(Track:track,FirstBar:3,PageBars:2,Scale:1.25,Width:900));Check(view.TrackCount==3 && view.Bar(3) is not null && view.Bar(4) is not null && view.Bar(1) is null,"track selection, zoom and a later page retain only shown bar geometry");Check(view.NoteOnCount==24,"selected track MIDI retains its own expanded notes");File.WriteAllBytes(Path.Combine(args[1],$"notation-multitrack-track{track}.mid"),view.Midi);File.WriteAllBytes(Path.Combine(args[1],$"notation-multitrack-track{track}.png"),view.Png);}
Check(multi.DescribeBar(2,3).Contains("bicí",StringComparison.Ordinal),"percussion bar has a native readable text alternative");
var allTracks=multi.Render(new(AllTracksAudio:true));Check(allTracks.NoteOnCount==72,"all-track playback retains the complete score");File.WriteAllBytes(Path.Combine(args[1],"notation-multitrack-all.mid"),allTracks.Midi);evidence.Add(new{ name="notation-multitrack.musicxml", tracks=multi.Tracks, expandedPlayback=multi.Playback, allTrackNotes=allTracks.NoteOnCount });Check(multi.AtSeconds(multi.Playback[2].StartSeconds+.01)?.Number==1 && multi.AtSeconds(-1) is null,"source position locates repeated bars without UI clock estimates");
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
