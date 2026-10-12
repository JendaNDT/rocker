# W08 — Společné komponenty Vintage

Základ tvoří systémové Segoe/WinUI texty, seznamy, formuláře, menu, dialogy
 a příkazy. Hudební přístroje mají teplou kontrastní plochu a měděný akcent;
 světlý/tmavý režim i vysoký kontrast mají vlastní theme resources. Akcentní
 text má samostatnou kontrastní barvu. Nejnovější transport je kompaktní
 nativní CommandBar, hlasitost a časová osa, aby nezabral polovinu malého okna.

Nástroje → Komponenty Vintage otevře skutečný katalog v hlavním WinUI okně:

- `ValueDial`: dekorativní ukazatel, systémový NumberBox a Slider, společná
  hodnota, přímé číselné zadání a klávesnice. Nečíselný vstup se bezpečně obnoví.
- `LevelMeter`: jednoznačné ticho/bez signálu/úroveň/přebuzení. Ukázkový signál
  je výslovně popsaný; bez vstupu se nezobrazuje náhodná živá hodnota.
- `ChordDiagram`: otevřené/ztlumené struny, prstoklad, barre a zrcadlení;
  čtečka má textový popis všech strun. Canvas je pouze vizuální zobrazení.
- `FretboardDiagram`: skutečné WinUI buňky s MIDI/tóny a stupnicí. Fokus drží
  jednu tabulátorovou zastávku, šipky přecházejí po sousedních buňkách,
  Enter/mezerník vybírají tón. Kořen má kromě barvy číslo 1 a dostupný popis.
- Karty, nativní seznam s více položkami a standardní InfoBar stavy.

Katalog používá demonstrační hodnoty a nepřehrává/neměří kytaru. Produktové
stránky nyní tyto stejné komponenty připojují ke skutečným datům a hostu; [aktuální úplná galerie a ověření](../w36/README.md).
Neoznačovat katalog za dokončený Aparát, Live, trenažér nebo zařízení.

Windows diagnostika vytvoří komponenty, ověří hranice NumberBox/Slider,
neplatný vstup měřáku a vykreslí všech 26 akordů pro obě ruce. Dva starty
publikovaného procesu ověřují přehled i katalog, noty, bridge a čisté zavření.
`RenderTargetBitmap` ukládá skutečný obsah WinUI jako PNG; případný desktopový
snímek je doplňkový a jeho absence neskrývá výsledek renderu. Ruční klávesnice,
čtečky, všechny vysoké DPI a systémový vysoký kontrast jsou odložené do W44,
nejsou vydávané za již provedené testy.

Windows sestavení [38153511933](https://github.com/JendaNDT/rocker/actions/runs/38153511933)
prošlo oběma starty, 31 kontrolami komponent v každém procesu, navigací všech
32 pohledů, notovým rendererem, místním video bridgem a čistým zavřením.
[Důkaz](windows-validation.json) a skutečné WinUI snímky
[přehledu](windows-overview.png) a [komponent](windows-components.png) jsou
uložené zde. Tento historický krok nepotvrzoval navazující produktové funkce; jejich aktuální implementaci a přejímku dokládá [W43](../w43/README.md).
