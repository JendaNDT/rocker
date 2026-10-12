# UI/UX — přehlednost a rozložení

Jendova priorita z 12. 10. 2026: celková přehlednost a rozložení při zachování Vintage a skutečných Windows ovládacích prvků. Navazuje na kompletního kandidáta W43; skutečné hraní a ruční přejímka zůstávají ve W44.

- Navigace na běžném desktopu zobrazuje názvy. Přehled, Live a Nastavení jsou dostupné přímo, bez stejnojmenné vnořené položky. Rozbalené jsou potřebné podsekce aktuální skupiny.
- Spodní panel ukazuje relevantní akce cvičení a skutečně dostupný přehrávač. Prázdný přehrávač ani neaktivní cvičení nezabírají místo v Nastavení. Otevřený soubor zůstává dostupný napříč pohledy.
- Přehrát / pozastavit je jeden označený příkaz. Hlasitost je ve vlastním rozbalovacím ovladači; rychlost, začátek souboru a podrobnosti jsou v nabídce přehrávače. Zastavení všech zvukových zdrojů zůstává dostupné. Podrobné chyby mají viditelnou hlášku a úplný text.
- Přehled upřednostňuje další konkrétní krok plánu. Ukazuje postup a bloky v adaptivní mřížce; původní ukládání, explicitní potvrzení bloku a měření aktivity jsou zachované.
- Live upřednostňuje nastavení/spuštění, poslech kytary, hlasitost a vstupní měřák. Ladička má čitelnou Vintage plochu. Nahrávání má hlavní akci hned, zdroj/název/předtakt se rozbalují. Diagnostika a zbývající měřáky se rozbalují zvlášť. Monitoring se nezapíná automaticky.
- Společné karty mají jednotné zaoblení, stránky srozumitelné popisy a menší duplicitní texty.

`ux-layout.json` kontroluje skutečný Windows layout, skrytý prázdný panel v Nastavení, relevantní akce Live, velikost jeho prázdného spodního panelu, úzké okno a dostupné přehrávání načteného souboru. Kontrola všech 32 pohledů, UI Automation a instalační cyklus jsou dál součástí kandidátního buildu. Linux kontrola C# používá náhradní XAML pole; není důkaz spuštění Windows.

Snímky budou po úspěšném Windows běhu aktualizované v [galerii](../w36/native-gallery/index.html). Výsledky a přesná testovaná revize patří do `windows-validation.json`; do jeho uložení není nové rozložení označené jako ověřené na Windows. Ruční ergonomie, Narrator/DPI a skutečná odezva zůstávají neprovedené.
