// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rocker.Video;

public static partial class VideoProtocol
{
    // Stable manifest identity, without version/architecture. The installer must
    // register this same identity; the wrapper never impersonates a website.
    public const string AppId = "JendaNDT.Rocker";
    public const string Origin = "https://jendandt.rocker";
    public const string Page = Origin + "/player.html";
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
    public static string? ExtractId(string input)
    {
        var value = input.Trim();
        if (IdPattern().IsMatch(value)) return value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0) return null;
        var host = uri.IdnHost.StartsWith("www.", StringComparison.Ordinal) ? uri.IdnHost[4..] : uri.IdnHost;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (host == "youtu.be") return segments.Length > 0 && IdPattern().IsMatch(segments[0]) ? segments[0] : null;
        if (host is not ("youtube.com" or "m.youtube.com" or "music.youtube.com")) return null;
        foreach (var pair in uri.Query.TrimStart('?').Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == "v")
            {
                var id = Uri.UnescapeDataString(parts[1]);
                if (IdPattern().IsMatch(id)) return id;
            }
        }
        return segments.Length >= 2 && segments[0] is "embed" or "shorts" or "live" && IdPattern().IsMatch(segments[1]) ? segments[1] : null;
    }
    public static VideoEvent? ReadEvent(string source, string json)
    {
        if (source != Page || json.Length > 8192) return null;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("event", out var kind) || kind.ValueKind != JsonValueKind.String) return null;
            long token=0;if(root.TryGetProperty("token",out var tokenField) && (!tokenField.TryGetInt64(out token) || token<0 || token>int.MaxValue))return null;
            var name = kind.GetString();
            if (name is "bridge-ready" or "ready") return new VideoEvent(name,Token:token);
            if (name is "error" && root.TryGetProperty("code", out var code) && code.TryGetInt32(out var error)) return new VideoEvent(name, Code: error,Token:token);
            if (name is "state" && root.TryGetProperty("state", out var state) && state.TryGetInt32(out var number) && number is -1 or 0 or 1 or 2 or 3 or 5)
                return new VideoEvent(name, Code: number,Token:token);
            if (name == "progress" && Number(root, "position", out var position) && Number(root, "duration", out var duration) && Number(root, "rate", out var rate) && rate > 0 && rate <= 16)
                return new VideoEvent(name, Position: position, Duration: duration, Rate: rate,Token:token);
            if (name == "rates" && root.TryGetProperty("rates", out var rates) && rates.ValueKind == JsonValueKind.Array && rates.GetArrayLength() <= 16)
            {
                var values = new List<double>();
                foreach (var item in rates.EnumerateArray())
                {
                    if (!item.TryGetDouble(out var value) || !double.IsFinite(value) || value <= 0 || value > 16) return null;
                    if (!values.Contains(value)) values.Add(value);
                }
                return new VideoEvent(name, Rates: values.ToArray(),Token:token);
            }
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException) { }
        return null;
    }
    private static bool Number(JsonElement root, string name, out double value)
    {
        value = 0;
        return root.TryGetProperty(name, out var field) && field.TryGetDouble(out value) && double.IsFinite(value) && value >= 0 && value <= 864000;
    }
    public static string DescribeError(int code) => code switch
    {
        2 => "Odkaz na video není platný.",
        5 => "YouTube nemůže přehrát video na tomto zařízení.",
        100 => "Video neexistuje nebo je soukromé.",
        101 or 150 => "Autor tohoto videa nepovolil vložené přehrávání.",
        153 => "YouTube nepřijal identifikaci aplikace. Video lze otevřít v prohlížeči.",
        -1 => "YouTube není dostupné. Zkontroluj připojení a zkus video načíst znovu.",
        _ => $"Přehrávání YouTube selhalo (stav {code})."
    };
}
public sealed record VideoEvent(string Kind, int Code = 0, double Position = 0, double Duration = 0, double Rate = 1, double[]? Rates = null,long Token=0);
