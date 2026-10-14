# Rocker — nativní pracovní prostředí Windows

Aktualizováno 14. 10. 2026. Implementovaná pracovní plocha 2 a testovací kandidát **0.3.1 RC1** jsou sestavené a automaticky ověřené na Windows.

- [Hotový instalátor a galerie](https://github.com/JendaNDT/rocker/releases/tag/untagged-b2ab831b9bb79eaacd0c) — neveřejný koncept vydání; instalátor se jmenuje Rocker-Windows-Setup-0.3.1-x64.exe. Galerie je samostatný ZIP.
- [Skutečná galerie všech 32 funkcí](gallery.html), [její zdrojová revize](gallery-data.json), [prohlížečová kontrola](gallery-validation.json).
- [Úspěšný Windows běh 38491131359](https://github.com/JendaNDT/rocker/actions/runs/38491131359), zdroj `3ca755016f54f2fc36ef5e2f5d4147afa35018e7`.
- [Přejímka kandidáta](candidate-validation.json), [kontroly oken](desktop-runtime.json), [interakce dokumentů](desktop-interactions.json), [rozměry pracovní plochy](desktop-workbench.json), [kontrast obou motivů](desktop-recording-contrast.json), [instalátor](installer-validation.json).
- [Ověření všech předaných souborů](../releases/delivery-validation.json), [návod](../w43/user-guide.md), [implementace](implementation.md), [převod dat a návrat](migration.md), [hranice přejímky](acceptance.md).

Pracovní lišta a průzkumník doplňují skutečné dokumenty v záložkách. Ladička, metronom, aparát a Looper se otevírají v samostatných Windows oknech; připojení zpět je volitelné. Nahrávka má velký editor a pevné A/B, Studio časovou osu a vlastnosti stop. Stabilní hostitel obsahu aktualizuje právě otevřenou záložku bez přepínání jinam. [Revize 2](workbench-revision-2.md) uvádí konkrétní opravy po připomínkách k 0.3.0.

Automaticky prošlo 49 kontrol oken/interakcí v obou diagnostických profilech i nainstalované aplikaci, 64 snímků, 22 native sad, funkční/datové kontroly a instalační cyklus. Galerie obsahuje obsah skutečných WinUI oken ze syntetického profilu; systémové rámy nejsou součástí snímků. Fyzický poslech, celková odezva M-Track Solo, další monitory, Narrator a výsledný dojem z UI vyžadují vlastní přejímku.

## Zadání a historie

[Hlavní plán D01–D36](../../../WINDOWS_DESKTOP_REBUILD_PLAN.md), [výchozí inventura](baseline.md), [návrhy D02](design-gallery.html) a [specifikace](design-spec.md) zůstávají podklady. [windows-validation.json](windows-validation.json) a [rozhodnutí o hostování](window-decision.md) popisují historický experiment D03; nejsou důkazem aktuálního kandidáta. Současný důkaz je candidate-validation.json výše. [Modelové kontroly](models-validation.json) ověřují přenosné kontrakty; samy neověřují okna nebo fyzický zvuk.
