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
stránky budou tyto stejné komponenty připojovat ke skutečným datům a hostu.
Neoznačovat katalog za dokončený Aparát, Live, trenažér nebo zařízení.

Windows diagnostika vytvoří komponenty, ověří hranice NumberBox/Slider,
neplatný vstup měřáku a vykreslí všech 26 akordů pro obě ruce. Dva starty
publikovaného procesu ověřují přehled i katalog, noty, bridge a čisté zavření.
`RenderTargetBitmap` ukládá skutečný obsah WinUI jako PNG; případný desktopový
snímek je doplňkový a jeho absence neskrývá výsledek renderu. Ruční klávesnice,
čtečky, všechny vysoké DPI a systémový vysoký kontrast jsou další W37/W42,
nejsou vydávané za již provedené testy.

Aktuální Windows sestavení tohoto řezu se ověřuje. Základ shellu před tímto
řezem prošel v běhu 38152583162; to samo nepotvrzuje nové vlastní komponenty.
