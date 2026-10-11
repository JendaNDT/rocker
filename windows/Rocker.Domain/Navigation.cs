// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;
public sealed record WorkspaceView(string Id, string Title, string Group);
public sealed record NavigationTarget(string ViewId, string? ItemId = null);
public static class WorkspaceViews
{
    public static IReadOnlyList<WorkspaceView> All { get; } = Array.AsReadOnly(new WorkspaceView[] {
        new("01","Přehled","Přehled"), new("02","Live","Live"), new("03","Ladička","Live"), new("04","Metronom","Live"),
        new("05","Výběr lekce","Cvičení"), new("06","Průběh lekce","Cvičení"), new("07","Vlastní skladba","Cvičení"), new("08","YouTube cvičení","Cvičení"),
        new("09","Přesný trénink","Cvičení"), new("10","Power chordy","Cvičení"), new("11","Gallop","Cvičení"), new("12","Palm mute","Cvičení"),
        new("13","Aparát","Zvuk"), new("14","Nahrávky","Zvuk"), new("15","Studio","Zvuk"), new("16","Looper","Zvuk"),
        new("17","Akordy","Knihovna"), new("18","Stupnice","Knihovna"), new("19","Riffy","Knihovna"), new("20","Noty a tabulatury","Knihovna"),
        new("21","Hmatník","Knihovna"), new("22","Sluch","Knihovna"), new("23","AI — rozhovor","AI tutor"), new("24","AI — plán","AI tutor"), new("25","AI — zpětná vazba","AI tutor"),
        new("26","Moje kytara","Nastavení"), new("27","Pokrok","Přehled"), new("28","Nastavení","Nastavení"), new("29","Zvuková zařízení","Nastavení"),
        new("30","Zálohy a soubory","Nastavení"), new("31","První spuštění","Nastavení"), new("32","Export nahrávky","Zvuk") });
    public static WorkspaceView Get(string id) => All.FirstOrDefault(view => view.Id == id) ?? throw new ArgumentException("Neznámý pracovní pohled.", nameof(id));
    public static void Validate(NavigationTarget target)
    {
        _ = Get(target.ViewId);
        if (target.ItemId is not null && (target.ItemId.Length is 0 or > 128 || target.ItemId.Any(char.IsControl))) throw new ArgumentException("Neplatná úloha navigace.");
    }
}
public sealed class NavigationSession
{
    private readonly List<NavigationTarget> back = [], forward = [];
    public NavigationTarget Current { get; private set; } = new("01");
    public bool CanGoBack => back.Count > 0;
    public bool CanGoForward => forward.Count > 0;
    public void Navigate(NavigationTarget target)
    {
        WorkspaceViews.Validate(target);
        if (Current == target) return;
        back.Add(Current); if (back.Count > 128) back.RemoveAt(0);
        forward.Clear(); Current = target;
    }
    public void Back()
    {
        if (!CanGoBack) return;
        forward.Add(Current); Current = back[^1]; back.RemoveAt(back.Count - 1);
    }
    public void Forward()
    {
        if (!CanGoForward) return;
        back.Add(Current); Current = forward[^1]; forward.RemoveAt(forward.Count - 1);
    }
}
public enum CloseChoice { Save, Discard, Cancel }
public sealed record WorkState(bool Recording, bool UnsavedChanges)
{
    public bool RequiresConfirmation => Recording || UnsavedChanges;
    public string Prompt => Recording ? "Probíhá nahrávání. Zastavit a uložit záznam před zavřením?" : "Práce obsahuje neuložené změny. Uložit je před zavřením?";
}
