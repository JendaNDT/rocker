# W16 — Hodiny zařízení, hudební mřížka a hranice smyčky

Společný callback počítá vzorky bez ohledu na zdroj: tón, MIDI/soubor i Live.
Pozice zařízení, epocha a hudební fáze se publikují mimo UI časovač; UI jen
zobrazuje poslední snímek. Nové otevření zařízení začne novou epochu a nulu,
nezamíchá tak 44,1/48/96 kHz časové základny. Analytické bloky používají
stejnou pozici prvního vzorku a epochu. Metadata nahrávky se připojí ve W22.

Hudební mřížka zachovává zbytek fáze přes bloky; tempo se mění na hranici
zpracovávaného bloku se zachováním fáze. Nezaokrouhluje trvání každé doby
na velikost bufferu. Pětiminutový test 137 BPM porovnává stejné přesné
pozice při blocích 512 a proměnlivých 1/127/256/511/1024/63/7 vzorků,
kontroluje každý onset vůči teoretické pozici s tolerancí jednoho vzorku,
změnu 120 → 240 BPM v půlce doby a změnu epochy. Zvukové metronomové
události, subdivize, předtakt a všechny režimy se připojí ve W18.

Přehrávač má A/B hranice v celých zdrojových vzorcích. Přechod zachovává
frakční přesah resamplingu, takže jiná vzorkovací frekvence nezaokrouhluje
každé opakování. Krátké fade na hranicích omezují skok; seek mimo aktivní
úsek vrátí jeho začátek. End-of-file se nepoužije jako zkrácení frakčního
přesahu u smyčky celé skladby. Nastavení úseku zastaví přehrávání, nikdy ho
samo nespustí. Formulář úseků a jejich persistence navazují ve W21.

C ABI 6 má 72b clock snapshot, tempo a nastavení smyčky. C# kontrakty
ověřují skutečné počty vzorků, tempo a konfiguraci úseku přes P/Invoke.
Linux 6/6 CTest kontroluje také tisíce opakování při 48 → 44,1 kHz,
různé bloky, izolaci stereo výstupu a zachování rozsahu. Vlastní RT audit
pokrývá hodiny jako součást input grafu.

Počet vzorků a délka bufferu nejsou celková odezva kytara → sluchátka.
Odhad ovladače zůstává zvlášť v zařízení. Nynější unity graf nemá lookahead;
konstantní DSP prodleva aparátu se doplní ve W17 a skutečné fyzické měření
zůstává W44. Časování looperu a diskový záznam jsou další integrační kroky,
ne hotové funkce tohoto řezu. Windows ověření navazuje samostatným buildem.

Windows [38161618474](https://github.com/JendaNDT/rocker/actions/runs/38161618474)
prošel všemi 6 CTest, 46 transportními kontrolami, skutečným C# voláním
a dvěma WinUI starty. [Důkaz](windows-validation.json) vymezuje přesnou revizi.
