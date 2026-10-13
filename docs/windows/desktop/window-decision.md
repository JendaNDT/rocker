# D03 — skutečné Windows hostování dokumentů a nástrojů

Stav: technický řez implementovaný, skutečné Windows ověření probíhá.
Zdrojová revize 11242c965c9074f1cb6a22933482121661733f33,
[Windows běh 38437376867](https://github.com/JendaNDT/rocker/actions/runs/38437376867).
Linux semantic check ověřil 65 C# souborů bez chyb s reálnými WinUI
referencemi a stubovanými XAML poli; není to Windows runtime přejímka.

## Výchozí technické rozhodnutí

- Zůstat u WinUI 3 a existujících nativních prvků; bez nové docking knihovny.
- Omezené hostování: TabView pro dokumenty, zvláštní Window pro odpojený dokument a nástroj.
- Jediný UI DispatcherQueue; jeden XAML strom má v každou chvíli právě jednoho hostitele.
- Přesun nejdřív odpojí Content starého hostitele, pak připojí stejné zobrazení novému.
- Dokument má stabilní identitu, UI výběr a zoom. Další fáze oddělí model relace od lifetime tohoto pohledu.
- Plovoucí nástroj má explicitního nativního vlastníka; dokumentové okno je samostatné.
- ContentDialog dostane XamlRoot skutečného volajícího okna.
- NativeAudio se nevytváří v hostiteli; experiment dostává již existující host a pouze čte snapshoty.

Tento experiment běží pouze v izolovaném Windows diagnostickém profilu.
Nenahrazuje produkční shell a není nový kandidát k bratrovu testování.
Vybraná revize se nepředává jako hotová desktopová přestavba.

## Co se skutečně provádí

[DesktopHostExperiment.cs](../../../windows/Rocker.Probe/Desktop/DesktopHostExperiment.cs)
otevře pracovní host, odpojené okno a singleton nástroje. Jeden dokument
obsahuje TextBox s výběrem, NumberBox se zoomem, skutečný notový bitmapový
zdroj aplikace a lokální WebView2 bez sítě/zvuku.

Postup:

1. Připojit dokument do nativního pracovního hostu.
2. Inicializovat lokální WebView2, uložit kontrolní JavaScript stav.
3. Odpojit dokument do druhého skutečného HWND.
4. Ověřit jednoho rodiče, text/zoom/výběr, nový XamlRoot a původní CoreWebView2/stav.
5. Otevřít plovoucí nástroj dvakrát a ověřit stejnou instanci i nativního vlastníka.
6. Zobrazit a zrušit ContentDialog nad odpojeným dokumentem.
7. Připojit stejný dokument zpět a ověřit jeho stav.
8. Zavřít nástroj i prázdné odpojené okno; hlavní host zůstává živý.
9. Porovnat stav audio hostu, frame/epochu a monitoring před/po operacích.
10. Uvolnit experimentální okna a WebView2; původní aplikace pokračuje ve svých existujících kontrolách.

Implementováno je 19 tvrdých runtime assertionů. Jsou instalované i
TabDragStarting / TabStripDragOver / TabStripDrop pro přenos identity
a příkazové/klávesnicové alternativy. Runtime scénář provádí stejný
transfer přes společnou metodu; skutečný fyzický pointer drag se zatím
nevydává za otestovaný.

## Hranice důkazu

Lokální WebView2 kontrola ověřuje XAML hostování a zachování core/state.
Není to online YouTube přejímka, synchronizace videa ani důkaz nulového
přerušení při přesunu skutečného embed. Bitmapový notový zdroj ověřuje
přenos náhledu, nikoli všechny renderer/zoom/selection scénáře nové
ScoreDocumentSession. Ty patří D21/D22.

RenderTargetBitmap snímá XAML obsah; GPU video vrstva může být na snímku
prázdná. Stav WebView2 se proto ověřuje přímo přes skutečný core API.
Snímky používají RasterizationScale svého vlastního kořene.

Fyzický druhý monitor, změny DPI, Alt+Tab, Narrator a ruční drag/drop jsou
odložené scénáře, nikoli automaticky úspěšné kontroly. Produkční D09/D10
musí doplnit registry, správné role/limity, fokus, příkazy, zavírání a
session ownership; D03 je ověření technické hranice.

## Evidence a další postup

Windows runtime zapisuje desktop-host-experiment.json a tři skutečné
snímky do build reportů. Kandidátní ověřovač tento report vyžaduje.
Po úspěšném běhu sem uložit vybrané důkazy, ověřit cleanup a navázat D04.
Pokud přesun WebView2 nebo jiná hranice selže, nejdřív změnit technické
rozhodnutí podle zjištěné příčiny a ověřit opravený scénář; neodstraňovat
assertion jen pro dosažení úspěšného buildu.
