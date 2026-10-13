# Desktopová implementace

Implementace D05–D32 je v samostatné větvi `windows-native-prototype`.
Finální předání D36 vyžaduje úspěšný Windows build včetně instalace,
aktivace souboru a odinstalace. RC6 zůstává posledním předaným balíkem,
dokud tento celý cyklus neprojde.

## Služby a životnost

`windows/Rocker.AppCore` vlastní ApplicationServices, jednu StorageSession,
AudioSession, TransportCoordinator, PracticeCoordinator, ExitCoordinator,
OperationRegistry, DocumentRegistry a CommandRouter. Přenosné kontroly
ověřují skutečné SQLite úložiště, cancellation a vlastnictví operací;
kontroly audia používají výslovně testovací native backend.

WinUI hostitelé používají skutečná `Window`, `AppWindow`, `TabView`,
`TreeView`, `ContentDialog` a systémové pickery. Pomocné okno nevytváří
NativeAudio, databázi ani druhý publish timer. Jeden UI cyklus obsluhuje
sdílené přístroje a všechny živé dokumentové relace.

Stav jednotlivých editorů je uložený v jejich DocumentSession. ID, historie,
výběr, renderovaný obraz a ovládací prvky následují dokument při přesunu
mezi skutečnými XamlRoot. MainWindow partial soubory zůstávají adaptéry
stávajících funkčních kontrolerů; nevzniká jejich kopie pro každé okno.
Legacy NavigationView slouží referenční přejímce původních kontraktů;
produkční vstup používá novou pracovní plochu.

## Pracovní plocha

- Knihovna obsahuje skutečné položky katalogu, skladby, projekty a nahrávky.
  Lekce a nahrávky mají kompaktní samostatný knihovní panel.
- Dokumenty mají záložky, pevné lišty, oddělené scroll oblasti a vlastnosti.
  Ve zmenšeném editoru jsou vlastnosti dostupné přes lištu.
- Ladička, metronom, aparát a looper mají jednu UI identitu. X skryje
  nástroj; nové vyvolání aktivuje stejný objekt. Live a nastavení jsou
  skutečná samostatná okna.
- Přesun funguje příkazem, kontextovou nabídkou a přes TabStrip drag/drop.
  Fyzický pointer drag zůstává samostatnou ruční přejímkou.
- Rozložení uchovává skupiny záložek, aktivní záložku, připojené/skryté
  nástroje, běžné rozměry oken a volby zobrazení partitur a projektů.
  Obnova přizpůsobí fyzické souřadnice dostupné pracovní ploše monitoru.

## Zvuk a operace

Otevření skladby nebo nahrávky čte průběh přes hostless MediaInspection.
Nevymění tím mediální slot a nezapíná zařízení. Výslovné přehrávání
připraví zdroj přes koordinátor a ověří skutečný native source ticket;
starý ready clip se stejnou rychlostí nestačí. Capture a looper chrání
vlastnictví zdroje. Mono vstup a stereo výstup zůstávají ve stejném jádru.

Dialogy a pickery rozlišují HWND a XamlRoot volajícího dokumentu. Příkazy
uchovávají původní relaci přes await. AI požadavek zachytí model a kontext
při odeslání; změna jiného okna je nepřesměruje. Ukládání a undo jsou
samostatné pro každý projekt. Centrum operací umožňuje cooperative cancel.

Při ukončení se nejprve shromáždí všechna rozhodnutí. Zrušení posledního
potvrzení nezahodí dřívější dokument; neúspěšné uložení zablokuje všechna
navržená zahození. Shutdown flushne data, zastaví zvuk a jednou uvolní
aplikační služby. Sleep zpráva se zpracuje jednou a monitoring se neobnoví.

## Finální automatická Windows evidence

Revize `69f6c8896593c48b6c43d011bf442f4c1d737a6b`, úspěšný běh
[38477297083](https://github.com/JendaNDT/rocker/actions/runs/38477297083):

- 20 produkčních window/session kontrol a 23 dalších interakcí;
- všech 32 funkcí vykreslených ve tmavém i světlém Vintage;
- stejné kontroly v obou diagnostických profilech i po instalaci;
- 22 native sad, 39 aplikačních service kontraktů, datová a původní funkční přejímka;
- úplný instalační cyklus, přesná oprava DLL, Unicode aktivace projektu,
  zachování asociace při selhání aktualizace a ponechání dat po odinstalaci.

[Galerie](gallery.html), [její revize](gallery-data.json),
[přejímka](candidate-validation.json), [matice scénářů](acceptance.md).
Starší neúspěšné běhy nejsou použité pro předání. Galerie používá izolovaný
profil a syntetické soubory; nepotvrzuje fyzický poslech, M-Track Solo ani
celkovou odezvu. Ruční část pokračuje v přejímce s bratrem.
