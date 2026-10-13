# Pohodlí při hraní — pět rozšíření Windows aplikace

Zadání Jendy z 13. 10. 2026: implementovat režim hraní, společné sestavy cvičení, vyhledávání a oblíbené, průběh nahrávek, pohodlnější aktualizace a vizuální detaily.

## Implementace

Režim Jen hraju přizpůsobí stávající Live; nesestavuje druhé zvukové jádro. Nativní navigace, číselné ovladače, seznamy, klávesnice a menu zůstávají. F11 / Esc, velké hlavní akce, světlý i tmavý Vintage. Přepínání obsahu používá standardní Windows EntranceThemeTransition; dialogy přebírají zvolený vzhled.

`PracticeWorkspace` přidává validovaný typ dokumentu `practice-workspace` do SQLite. Sestava drží přesnou úlohu, kopii aparátu a metronomu, ladění a případně stabilní ID skladby, rychlost, pozici a A/B. Sestava z vlastní nahrávky uchovává i její ID a při přesunu/obnovení profilu znovu ověřuje soubor v místní knihovně. Přípravu lze zrušit samostatným nativním tlačítkem, i když je pracovní plocha dočasně zakázaná. Oblíbené používají stabilní klíče místo názvů. Limit 100 sestav, 500 oblíbených, 200 výsledků; hledání bez české diakritiky. Starý profil začíná prázdnou knihovnou. Úplné zálohy nový typ obsahují, média zůstávají v existujících knihovnách. Sestava nezapíná monitoring, metronom ani přehrávání a neukládá ovladač/zařízení jako součást zvukového presetu.

Zvuková vlna využívá existující nativní dekódovanou cache. Nahrávka se ověřuje proti svému hashi, předběžný poslech zůstává pozastavený, úsek lze zvolit myší i číselnými poli. Přehrávač a značky používají čas původního zdroje, včetně zpomalení. Výběr soubor nepoškozuje.

Aktualizace čtou pouze Windows vydání JendaNDT/rocker, včetně soukromých prerelease kandidátů. Úspěšný `candidate-validation.json`, SHA-256 a běh novější než nainstalovaný build tvoří nabídku. Stažení je omezené velikostí, nepřenáší přístupový token na server souborů, maže neúplné soubory a před spuštěním znovu ověřuje hash/formát. Installer se spouští až explicitním potvrzením a přes existující uložení/zastavení při zavírání. GitHub token je volitelný, oddělený DPAPI provider; pro soukromý repozitář funguje bez tokenu cesta přes přihlášený systémový prohlížeč. Aktualizace nestahují ovladače a nemění ochranu Windows. Cache není součást uživatelských záloh.

## Ověření

Místně: 60 C# souborů s reálnými WinUI referencemi bez chyb; doménové reference 69 + 184 hraničních kontrol a 372 dalších případů / 350 stavů; infrastruktura včetně 15 nových kontrol aktualizací a knihovny; TypeScript a 567 testů. Podklady: `local-validation.json`.

Windows: [běh 38361957245](https://github.com/JendaNDT/rocker/actions/runs/38361957245), revize `b550d64`, prošel včetně instalace, opravy, aktualizace a odinstalace. [Galerie](gallery.html), [uložené důkazy](windows-validation.json). Nový `workspace-enhancements.json` vyžaduje 18 kontrol skutečného okna, sdíleného audia, obnovení sestavy, vyhledávání, dekódované vlny a A/B při zpomalení. Snímky režimu hraní, dialogů, nahrávek a aktualizací vznikají z izolovaného syntetického profilu. Pro vlnu a skutečné zpomalení se vytváří dvousekundová vlastní PCM24 fráze se čtyřmi syntetickými tóny, nikdy přehraná na zařízení; krátká 10ms nahrávka nadále ověřuje původní ukládání. Jejich existence není ruční přejímka.

Online kontrola soukromých aktualizací s uživatelským tokenem a potvrzené spuštění instalátoru z aplikace zůstávají ručními scénáři. Automatické testy používají syntetické HTTP odpovědi a nespouštějí stažený kontrolní EXE. Fyzická hra na M-Track, skutečná odezva a ostatní dřívější W44 scénáře zůstávají neprovedené.

## Poznatky z Windows ověření

První nový snímek vzniká před původní standardní galerií; společný capture helper proto vytváří cílovou složku před zápisem. Krátká 10ms nahrávka stačí pro původní test ukládání, pro skutečný pitch-preserving stretch je nutný delší podklad; zachovaný test zpomalení nyní používá dva syntetické sekundy. Otevírání dialogu zahrnuje asynchronní načtení indexu: kontrola čeká na IsLoaded s limitem 10 sekund, ne pevně 200 ms. Windows čtení po crash testu používá stejné databázové bajty se sdílením ReadWrite kompatibilním s SQLite, beze změny aserce. Všechny kontrolní požadavky zůstaly zapnuté.

Nativní snímek aktualizací se pořizuje ještě před vytvořením build-info.json při balení, proto ukazuje vývojové sestavení. Skutečný instalační balík obsahuje tuto informaci a kontroluje příslušný Windows běh. Snímek zachycuje horní část karty; další volby jsou níže po posunutí.
