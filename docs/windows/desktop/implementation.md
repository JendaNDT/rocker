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

## Aktuální automatická Windows evidence

Revize `3ca755016f54f2fc36ef5e2f5d4147afa35018e7`, úspěšný běh [38491131359](https://github.com/JendaNDT/rocker/actions/runs/38491131359), kandidát 0.3.1 RC1:

- 21 kontrol oken/relací a 28 dalších interakcí; stejné kontroly v obou profilech i po instalaci;
- všech 32 funkcí ve tmavém i světlém Vintage; vlastní vizuální kontrola nahrávky, aparátu, Studia, ladičky a metronomu;
- přehrávač 49 DIP při třech různých skutečných šířkách okna;
- skutečný kontrast křivky 7,66 v tmavém a 5,51 ve světlém motivu;
- 22 native sad, přenosné aplikační/datové kontrakty a úplná instalační přejímka.

Záložka má trvalý ContentControl se stretch zarovnáním; při přestavbě editoru se mění jeho dítě. TabView kopíruje Content vybrané položky do vlastního presenteru při změně výběru, proto přímá výměna TabViewItem.Content nechávala viditelný starý placeholder. Kontroly načtené nahrávky a přepnutí nastavení ověřují skutečný IsLoaded a rozměry aktuálního obsahu. Události zaměření jsou na trvalém hostiteli a přežívají přestavbu stránky.

Nástroje se otevírají jako samostatná okna, běžné dokumenty zůstávají v pracovním prostoru. Vlastnosti a jednotlivé oblasti editorů se posouvají samostatně; nástroje lze dobrovolně připojit. Waveform používá ThemeResource styly místo kopie již vyhodnoceného brush z jiného prvku. [Podrobná revize](workbench-revision-2.md).

[Galerie](gallery.html), [revize](gallery-data.json), [kandidát](candidate-validation.json), [matice a omezení](acceptance.md), [předané soubory](../releases/delivery-validation.json). Starší běhy slouží historii. Syntetický profil nepotvrzuje fyzický poslech, latenci M-Track Solo, Narrator ani skutečné online služby.
