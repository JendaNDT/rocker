// SPDX-License-Identifier: AGPL-3.0-only
using System.Text.Json;
namespace Rocker.Infrastructure;
public sealed record WindowRectangle(int X, int Y, int Width, int Height)
{
    public WindowRectangle FitTo(WindowRectangle workArea)
    {
        int width = Math.Clamp(Width, Math.Min(640, workArea.Width), workArea.Width);
        int height = Math.Clamp(Height, Math.Min(480, workArea.Height), workArea.Height);
        int x = (int)Math.Clamp((long)X, workArea.X, (long)workArea.X + workArea.Width - width);
        int y = (int)Math.Clamp((long)Y, workArea.Y, (long)workArea.Y + workArea.Height - height);
        return new(x,y,width,height);
    }
}
public sealed record WindowPreferences(int SchemaVersion, WindowRectangle Bounds, bool Maximized, string Theme = "system");
public sealed class WindowPreferencesStore(UserFiles files)
{
    private string PathName => Path.Combine(files.DirectoryFor("settings"), "window.json");
    public WindowPreferences? Load()
    {
        if (!File.Exists(PathName)) return null;
        if (new FileInfo(PathName).Length > 16384) throw new InvalidDataException("Uložená poloha okna je poškozená.");
        var state = JsonSerializer.Deserialize<WindowPreferences>(File.ReadAllText(PathName));
        return state is not null && state.SchemaVersion == 1 && state.Bounds is not null && state.Bounds.Width > 0 && state.Bounds.Height > 0 && state.Theme is "system" or "dark" or "light" ? state : throw new InvalidDataException("Uložená poloha okna není platná.");
    }
    public Task SaveAsync(WindowPreferences preferences) => UserFiles.WriteTextAsync(PathName, JsonSerializer.Serialize(preferences));
}
