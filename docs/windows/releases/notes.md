Rocker 0.3.0 RC1 přináší pracovní plochu se záložkami, skutečnými samostatnými okny Windows a připojitelnými nástroji ve Vintage vzhledu.

- Studio, noty, skladby, nahrávky, lekce a tréninky mají vlastní dokumentové relace. Záložky lze odpojit, seskupit a vrátit do hlavního okna.
- Ladička, metronom, aparát a looper mají jedinou instanci; Live a nastavení jsou vlastní okna. Příkazy, klávesnice, dialogy a souborové operace respektují správný dokument.
- Zvuk používá jedno společné C++/JUCE jádro. Prohlížení další skladby nebo nahrávky nepřepíná přehrávání; zdroj přebírá výslovný příkaz. Stav monitoringu je viditelný u výsledného mixu.
- Rozložení zachovává skupiny záložek, polohy oken a volby editorů. Hudební data zůstávají kompatibilní; rozložení lze předat samostatně jako .rockerlayout.

Stáhni **Rocker-Windows-Setup-0.3.0-x64.exe**, zavři původní Rocker a spusť instalátor. Vyžaduje Windows 11 x64 od 24H2. Vývojové nástroje nepotřebuješ. Zdrojové kódy jsou samostatný volitelný balík.

**Rocker-Windows-desktop-gallery.zip** obsahuje interaktivní galerii všech 32 funkcí ve tmavém i světlém vzhledu. Rozbal ZIP a otevři gallery.html; snímky lze zvětšit a filtrovat. Jde o skutečný obsah WinUI oken z izolovaného kontrolního profilu. Ukázkové nahrávky jsou syntetické.

Windows build ověřil 43 kontrol oken a dokumentových interakcí, všech 32 funkcí ve dvou vzhledech, 22 native sad, původní funkční/datové kontroly a celý cyklus instalace, opravy, Unicode aktivace projektu a odinstalace se zachováním dat. Kontrolní součty balíků jsou v SHA256SUMS.txt.

Skutečný poslech, celkovou odezvu M-Track Solo, online služby a fyzické zkoušky monitorů či přístupnosti musí potvrdit závěrečná ruční přejímka. Toto je funkčně ucelený testovací kandidát, ne tvrzení o dokončeném měření na bratrově kartě.
