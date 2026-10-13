# Rocker — implementační plán přestavby desktopového prostředí

**Znovuotevřená vizuální přejímka:** verze 0.3.0 byla funkčně sestavená, ale Jenda její rozložení oprávněně vyhodnotil jako příliš podobné původní aplikaci. [Revize pracovní plochy 2](docs/windows/desktop/workbench-revision-2.md) opravuje výchozí okna, ovládání a vlastní editory. Historická označení dokončení níže nejsou schválením finálního UI.

Datum: 13. 10. 2026. Stav: **realizace D01–D36 schválená; D01–D35 dokončené a automaticky ověřené; D36 dokončené jako soukromé předání testovacího kandidáta**.

Jenda chce změnit způsob práce s Rockerem na profesionální Windows nástroj:
pracovní plochu s dokumenty, panely a více skutečnými okny. Vintage zůstává
vizuálním charakterem hudebních přístrojů. Tato přestavba navazuje na hotovou
nativní verzi; mění organizaci práce i vlastnictví služeb.

Výchozí checkout: windows-native-prototype, revize
175edf5f3e0fec50c9c48895aa5daf7a5888752e. Poslední sestavený kandidát je
[RC6](https://github.com/JendaNDT/rocker/releases/tag/windows-0.2.0-rc6-20261013),
zdrojová revize 5eb2eb97b2ce4f2340aa8b143714a93840c45fc0,
[Windows běh 38416440273](https://github.com/JendaNDT/rocker/actions/runs/38416440273).
Tento plán ani jeho kontrola nejsou důkazem implementovaného víceokenního UI.

## Orientace

- [Výchozí stav a rozsah](#scope)
- [Cílové rozložení a okna](#experience)
- [Pravidla práce, zvuku a zavírání](#behavior)
- [Architektura a soubory](#architecture)
- [Převod všech 32 pohledů](#coverage)
- [Postup D01–D36](#implementation)
- [Milníky a pořadí](#milestones)
- [Ověřování a předání](#validation)
- [Rizika a návrat k předchozí verzi](#risks)

<a id="scope"></a>
## 1. Výchozí stav a rozsah

### Co je zjištěné v kódu

| Současná část | Zjištění | Důsledek pro přestavbu |
| --- | --- | --- |
| [MainWindow.xaml](windows/Rocker.Probe/MainWindow.xaml) | NavigationView, společný ScrollViewer, stránkový ContentControl a spodní transport | Nahradit pracovní plochou s nezávislými oblastmi a dokumenty |
| [MainWindow.Shell.cs](windows/Rocker.Probe/MainWindow.Shell.cs) | Cache pohledů, navigace, dialogy a ochrana zavření jsou v hlavním okně | Oddělit identitu dokumentu, jeho data a hostující okno |
| [MainWindow.xaml.cs](windows/Rocker.Probe/MainWindow.xaml.cs) | Okno vytváří NativeAudio, řídí aktualizaci UI a při zavření jej uvolňuje | Host musí vlastnit aplikace, ne konkrétní okno |
| [App.xaml.cs](windows/Rocker.Probe/App.xaml.cs) | Jedna reference na MainWindow; instance listener a mutexy končí s jeho zavřením | Zavést registr oken a samostatný životní cyklus aplikace |
| [MainWindow.Shell.cs](windows/Rocker.Probe/MainWindow.Shell.cs) | Navigace pozastavuje video a ukončuje některé trenažéry | Tyto účinky přesunout do explicitních pravidel relací; přepnutí záložky není ukončení relace |
| [MainWindow.Files.cs](windows/Rocker.Probe/MainWindow.Files.cs) | Aktivace, soubory, zkratky a drag/drop pracují s hlavním oknem | Příkazy musí znát aktivní dokument a skutečné cílové okno |
| [WindowPreferences.cs](windows/Rocker.Infrastructure/WindowPreferences.cs) | Ukládá se jediné okno; minimální rozměry jsou společné | Zavést verzi rozložení, druhy oken a jejich vlastní minima |
| [MainWindow.Live.cs](windows/Rocker.Probe/MainWindow.Live.cs) | Monitoring má vlastní přepínač; processed bus je před jeho ztlumením | Měřák mixu musí vysvětlit vypnutý poslech, nikoli jen zobrazit „Ticho“ |

Bratr při dosavadním testu potvrdil, že hlášené ticho v mixu způsoboval
nezaškrtnutý monitoring; po zapnutí zvuk funguje. Jde o potvrzení této
konkrétní příčiny, nikoli všech hardwarových scénářů nebo celkové latence.

### Povinný rozsah

1. Nový shell, knihovna, záložky dokumentů, inspektor a společný transport.
2. Samostatné Live, plovoucí nástroje, odpojitelné dokumenty a jejich opětovné připojení.
3. Přestavba všech 32 existujících pohledů podle matice níže, včetně nastavení, importů, záloh a exportů.
4. Sdílený zvuk, pracovní relace a ukládání; správné zavírání, obnova oken a správa operací.
5. Srozumitelné stavy monitoringu, skutečné měřáky a synchronní metronom.
6. Konzistentní Vintage grafika, klávesnice, vysoké DPI, více monitorů a přístupnost.
7. Automatické kontroly, skutečné Windows sestavení, galerie a instalátor; uživatel nemusí nic kompilovat.

Použít existující WinUI 3 / C#, C++ / JUCE, SQLite, notový a video modul.
Neměnit audio ABI nebo algoritmy jen kvůli novému rozložení. Zachovat
uživatelská data, profile root, identitu instalace, asociace souborů a
dojednané soukromé předání se samostatnými zdroji.

VST hosting, nový DAW engine, více nezávislých současně hrajících skladeb,
cloudová synchronizace, plugin systém, nové AI hodnocení hry a nové hudební
funkce jsou mimo tuto přestavbu. Více oken samo o sobě nesnižuje audio latenci.

[WINDOWS_ROADMAP.md](WINDOWS_ROADMAP.md) zůstává záznamem původní realizace
W01–W43 a navazující hardwarové přejímky W44–W48. Kroky D se vedou odděleně;
původní přejímky se nesmějí automaticky označit za dokončené.

<a id="experience"></a>
## 2. Cílové rozložení a okna

### Hlavní pracovní plocha

Schematické rozložení je specifikace oblastí, nikoli hotový grafický návrh:

    ┌──────────────────────────────────────────────────────────────┐
    │ Rocker / název aktivního dokumentu             ovládání okna │
    ├──────────────────────────────────────────────────────────────┤
    │ Soubor  Úpravy  Zobrazení  Nástroje  Okno  Nápověda          │
    │ Otevřít  Uložit  Zpět/Znovu  Hledat  Live  volba rozložení   │
    ├──────────────┬───────────────────────────────┬───────────────┤
    │ KNIHOVNA     │ Lekce | Skladba | Studio *    │ VLASTNOSTI    │
    │              ├───────────────────────────────┤               │
    │ Lekce        │                               │ Vybraná stopa │
    │ Skladby      │   AKTIVNÍ DOKUMENT             │ nebo úloha    │
    │ Nahrávky     │   noty / časová osa / cvičení  │               │
    │ Projekty     │                               │ Nastavení    │
    │ Hudební obsah│                               │ výběru       │
    ├──────────────┴───────────────────────────────┴───────────────┤
    │ NÁSTROJE: Ladička | Metronom | Aparát | Looper              │
    ├──────────────────────────────────────────────────────────────┤
    │ Zdroj transportu · přehrát/pauza · nahrávání · zastavit vše  │
    │ Zvuková karta · vstup · Poslech vypnutý/zapnutý · stav práce │
    └──────────────────────────────────────────────────────────────┘

Knihovna slouží k hledání objektů a otevírání práce. Není to seznam všech
32 stránek. Inspektor se řídí výběrem uvnitř aktivního dokumentu. Zvukové
nástroje mají vlastní identitu a globální stav nezávislý na inspektoru.
Přehled a Pokrok jsou dostupné dokumenty, nikoli povinná zastávka před hraním.

Panelům lze změnit šířku nebo výšku; knihovnu, inspektor a nástroje lze skrýt.
Hlavní okno nemá jediný svislý scroll přes celý obsah. Seznamy, editor a
inspektor posouvají svůj obsah samostatně. Kompaktní transport zůstává viditelný.

### Druhy oken

| Druh | Účel a výchozí chování | Pravidlo počtu a zavření |
| --- | --- | --- |
| Pracovní okno | Knihovna, dokumenty, inspektor a transport; systémový rám, změna velikosti, Snap | Jedno hlavní; X znamená ukončit aplikaci včetně všech oken přes společnou přejímku |
| Live | Samostatná plocha pro hraní, vhodná pro druhý monitor; běžně v Alt+Tab, vlastní titulek | Nejvýše jedno; X zavře pohled, globální poslech a nahrávání pokračují |
| Nástroj | Ladička, metronom, aparát nebo looper; připojený panel nebo plovoucí okno | Jedna plná instance každého nástroje; X jej skryje, službu nevypne |
| Dokumentové okno | Odpojené noty, skladba, nahrávka, projekt nebo cvičení | Více různých dokumentů; X řeší jeho práci podle pravidel níže |
| Nastavení / zařízení / zálohy | Jedno samostatné okno s kategoriemi; běžné formuláře | Opětovné vyvolání aktivuje existující; neotevírat duplicitní změnu karty nebo obnovu dat |
| Průvodce / potvrzení | První spuštění, konflikt zdrojů, přepsání, neuložené změny | Krátký dialog svázaný s vlastníkem; ne modeless formulář pro destruktivní rozhodnutí |
| Centrum operací | Import, export, záloha, AI příprava a aktualizace; průběh a zrušení | Dostupné ze stavové lišty; operace nepatří životností libovolnému panelu |

Otevřít nástroj přes nabídku vždy zviditelní jeho stávající instanci.
Stejná relace dokumentu se přemisťuje mezi hosty; nevytvoří se druhý editor
týchž dat. Malý informační přehled metronomu v Live může současně zrcadlit
globální stav plného nástroje, nikoli založit nový metronom.

### Připojování a odpojování

Výchozí realizace je omezený, předvídatelný model: nástroje v oblasti
Nástroje, knihovna vlevo, inspektor vpravo; dokumenty v jedné skupině záložek.
Příkazy „Otevřít v samostatném okně“ a „Připojit do hlavního okna“ musí
fungovat i klávesnicí. Implementovat také přetahování záložek pro změnu pořadí,
odpojení do okna a připojení zpět s viditelným cílem.

Libovolné vnořené dokování, nekonečné skupiny záložek a přesouvání všech
panelů na všechna místa nejsou podmínkou dokončení. WinUI není automaticky
plnohodnotný docking systém. D03 ověří přesun, rám, fokus a DPI na Windows;
výchozí cesta používá vlastní omezené hostitele a existující nativní prvky,
bez nové externí docking knihovny. Případná knihovna musí projít kontrolou
licence, dostupnosti, klávesnice a životního cyklu před jejím zavedením.

### Výchozí pracovní rozložení

| Rozložení | Otevřený obsah | Smysl |
| --- | --- | --- |
| Cvičení | Knihovna lekcí, aktivní úloha, metronom/ladička, inspektor podle úlohy | Začátečník se dostane k hraní bez organizace oken |
| Hraní | Live, kompaktní aparát/metronom/ladička; knihovna může být skrytá | Pohodlné ovládání s kytarou |
| Studio | Projektová časová osa, seznam stop, inspektor stopy, knihovna médií | Úpravy a nahrávání bez dlouhých formulářových stránek |

Rozložení otevírá nebo přesouvá pohledy, ale samo nespouští zvuk,
monitoring, video, nahrávání ani trenažér a nemění zvukový preset.
Rozložení a „sestava cvičení“ jsou dvě různé věci. Sestavy dál uchovávají
hudební nastavení a zdroj; D29 propojí otevření sestavy s dokumentem.

### Grafický systém

- Běžný rám: čisté nabídky, přehledné seznamy, jasný výběr, záložky a jemné oddělení oblastí.
- Vintage: ořech, koženka, měď, bakelit a krémové přístroje na konkrétních hudebních modulech.
- Přístroj je dekorován jednou; nevkládat do sebe několik velkých ozdobných karet.
- Zachovat skutečná kyvadla, peak dBFS měřáky, knoflíky a jejich dostupné číselné ovládání.
- Tmavé, světlé a systémové téma se uplatní ve všech oknech; vysoký kontrast používá systémové barvy.
- Neoznačovat peak dBFS jako kalibrované VU. Obnova rozložení nesmí zobrazit falešný vstup, aktivní beat nebo hlasitost.

D02 vytvoří skutečné vizuální návrhy celého nového prostředí a interaktivní
galerii: hlavní pracovní plocha, Live, čtyři nástroje, dokumenty, Studio,
cvičení, video a systémová okna. Staré
[návrhy](docs/windows/design/vsechny-navrhy.md) jsou referencí obsahu a
materiálů; nejsou schválenou kompozicí nové pracovní plochy.

<a id="behavior"></a>
## 3. Pravidla práce, zvuku a zavírání

### Dokumenty a relace

Identita dokumentu obsahuje druh a stabilní ID objektu; u vnějšího souboru
ověřenou kanonickou cestu, u dočasného dokumentu vlastní ID. Životnost
relace není životnost XAML ovládacího prvku. Změna hostitele uchová výběr,
pozici, zoom, smyčku, rozpracovaný krok, historii úprav, probíhající operaci
a příslušnost ke zvukovému zdroji.

Umožnit otevřít více různých skladeb, not nebo projektů, ale dosavadní engine
nadále obsluhuje jen jednu aktivní hlavní mediální časovou osu.
Přepnutí záložky nemění slyšitelný zdroj; aktivní transport jmenuje vlastníka.
Příkaz Přehrát z jiného dokumentu získá zdroj výslovně. Při aktivním záznamu,
looperu nebo jiném konfliktu se před přípravou média nabídne bezpečné
zastavení/uložení nebo zrušení. Import jiného souboru nesmí tiše přepsat
aktuálně hrající native clip.

Otevřený neaktivní dokument nemá právo automaticky nastavovat tempo,
připojovat své média, vyhodnocovat hru ani ovládat globální aparát.
Pro trenažéry existuje nejvýše jedna aktivní hodnoticí relace. Přepnutí
mezi zobrazeními ji může ponechat běžet; spuštění jiné relace vyžaduje
výslovný přechod. Aktivní trénink a účtování času zůstávají viditelné.

### Globální zvuk a monitoring

Jeden AudioSession vlastní jeden NativeAudio / JUCE host, jednu zvukovku
a společné sample hodiny. UI čte snapshoty a posílá příkazy přes tuto službu.
Nepřidávat druhý host kvůli plovoucímu metronomu nebo odpojenému Studiu.
Callback nezná okna, nevolá UI, nealokuje kvůli rozložení a nečeká na něj.

| Stav | Text a chování v Live / stavové liště |
| --- | --- |
| Zařízení zastavené | „Zvuk je zastavený“; viditelný příkaz Spustit |
| Chybí karta nebo vstup | Konkrétní důvod a příkaz Nastavit zvuk; žádný předstíraný signál |
| Vstup běží, monitoring vypnutý | „Poslech kytary je vypnutý“ a příkaz Zapnout poslech; vstup i signál za aparátem mohou být živé |
| Monitoring zapnutý, gain 0 | „Hlasitost poslechu je na nule“; dostupný skutečný ovladač |
| Monitoring zapnutý, žádný vstupní signál | „Na vstupu není signál“; oddělit od vypnutého poslechu a prahu detekce ladičky |
| Monitoring zapnutý, vstup má signál | Zobrazit skutečné úrovně; ticho v mixu nepopsat jako vypnutý monitoring |
| Aparát bypass | „Aparát je vypnutý · čistý zvuk“; bypass není vypnutý poslech |
| Změna zařízení / uspání / odpojení | Zastavit podle stávajícího bezpečnostního kontraktu a ukázat důvod; neobnovit monitoring automaticky |

Mix může hrát skladbu nebo metronom při vypnutém monitoringu kytary. Text
u mixu musí vysvětlit stav kytary, ale nesmí tvrdit, že je celý mix vypnutý.
Úroveň „Za aparátem“ není důkazem průchodu do sluchátek. Direct Monitor
M-Track Solo aplikace neovládá ani z obrázku nezjišťuje; návod odliší tuto cestu.
Zapnutí monitoringu je výslovný příkaz, s nízkou výchozí hlasitostí; uložené
rozložení ani dřívější stav jej při spuštění neaktivují.

### Příkazy a klávesnice

Menu, toolbar, kontextové nabídky a zkratky sdílejí jeden registr příkazů.
Každý příkaz má cílový dokument nebo globální rozsah a skutečnou dostupnost.
Aktivní dokument se neodvozuje jen z posledního kliknutí na knihovnu.

| Příkaz | Návrh zkratky | Rozsah |
| --- | --- | --- |
| Otevřít | Ctrl+O | Aktivní pracovní/dokumentové okno; vytvoří nebo aktivuje relaci |
| Uložit | Ctrl+S | Aktivní editovatelný dokument; nástroje mají vlastní průběžné nastavení |
| Zavřít dokument | Ctrl+W | Aktivní dokument, nikoli celá aplikace |
| Zpět / znovu | Ctrl+Z / Ctrl+Y | Historie konkrétního editoru; mimo podporovaný editor vypnuto |
| Další / předchozí dokument | Ctrl+Tab / Ctrl+Shift+Tab | Dokumenty aktuálního hostitele |
| Hledat a příkazy | Ctrl+K | Sdílené hledání, výsledek otevře skutečný dokument nebo nástroj |
| Přechod mezi oblastmi | F6 / Shift+F6 | Knihovna, dokument, inspektor, nástroje, transport |
| Přehrát / pauza | Mezerník | Jasně označený zdroj; nezasahuje do textového pole nebo nativního ovladače |
| Režim „Jen hraju“ | F11 / Esc | Zjednoduší existující Live; neotevře druhý host |
| Ukončit | Alt+F4 | Podle typu okna; globální ukončení z menu má vlastní společnou přejímku |

Zkratky ověřit proti současným příkazům a přístupnosti. Nezavádět globální
systémové hotkeys. Příkaz „Zastavit vše“ je viditelný ve všech zvukových
oknech a zastaví skutečné zdroje včetně videa podle společného kontraktu.
Zastavení jednotlivého metronomu nebo přehrávače nezastaví jiné zdroje.

### Zavření a životní cyklus

| Událost | Povinné chování |
| --- | --- |
| Skrýt nástroj / zavřít Live | Odpojit UI odběry; zvuk a aktivní záznam pokračují. Stav zůstává viditelný v hlavním okně |
| Odpojit nebo připojit dokument | Přesun hostitele; žádné ukončení relace, nový host ani restart přehrávání |
| Zavřít neaktivní dokument | Uložit/zahodit/zrušit pouze jeho neuložené změny; neovlivnit ostatní |
| Zavřít vlastníka přehrávání nebo tréninku | Oznámit ukončení příslušného zdroje/relace; při záznamu vyřešit uložený výsledek, pak zavřít |
| Uložit selže | Dokument zůstává otevřený a dirty; nic nedisponovat |
| Zavřít hlavní okno / Ukončit Rocker | Jedna celková přejímka dokumentů, záznamu a operací; po souhlasu zastavit služby a zavřít všechna okna |
| Zrušit celkové ukončení | Zachovat registry, host a rozpracovaná data; neuzavřít část oken ani neuvolnit mutex |
| Aktualizovat / obnovit zálohu | Globální blokace konfliktních příkazů; jedna příprava, flush a bezpečný shutdown všech oken |
| Uspání / vytažení karty | Jediný handler zastaví host a relace podle bezpečnostního kontraktu; všechna okna dostanou stejný stav |
| Pád aplikace | Recovery zachová média a skutečně ukládaná data; při restartu pouze otevřít pohledy v zastaveném stavu |

Dočasné plovoucí nástroje mají vlastní systémový rám a správného vlastníka;
Live a dokumenty musí být samostatně přepínatelné. Chování taskbaru,
Alt+Tab, minimalizace a modálních dialogů ověří D03 na skutečných HWND.
Vždy navrátit fokus po zavření dialogu. Nezavírat pracovní relaci v XAML
Unloaded; ten nastává i při přemístění ovládacího prvku.

### Obnova rozložení a operace

Uchovat role oken, pořadí dokumentů, aktivní dokument, šířky panelů,
viditelnost nástrojů, velikost/polohu a explicitní volbu Vždy navrchu.
Souřadnice mají definované jednotky; návrh rozlišuje DIPs obsahu a
souřadnice AppWindow. Přepočítat podle DPI a omezit na dostupnou work area.
Po odpojení monitoru nesmí zmizet titulek ani příkaz obnovy.

Obnova otevřených souborů validuje cestu a existenci; chybějící soubor
nabídne najít nebo zavřít položku. Obnova projektu neznamená jeho přehrání.
Rozložení nemůže obnovit nahrávání, aktivní trénink, monitoring nebo
nedokončený destruktivní příkaz. „Obnovit výchozí rozložení“ mění pouze UI.

Operace mají ID, vlastníka dat, typ, průběh, možnost zrušení a stav chyby.
Přemístění pohledu je neruší; zavření vlastníka je řeší výslovně.
Obnova dat nebo aktualizace je globální operace. Export a import mají
bezpečné dočasné soubory a cleanup; jejich zrušení nepoškodí původní práci.
Citlivé klíče, přístupové tokeny a celé AI konverzace nepatří do diagnostiky UI.

<a id="architecture"></a>
## 4. Architektura a soubory

Názvy níže jsou cílový návrh, nikoli tvrzení, že tyto nové třídy existují.
Ověřený současný základ je uvedený v oddílu 1.

~~~mermaid
flowchart TD
    App["App: instance a životní cyklus"] --> Services["ApplicationServices"]
    App --> Windows["WindowManager a ExitCoordinator"]
    Services --> Audio["AudioSession: jediný NativeAudio"]
    Services --> Docs["DocumentManager a pracovní relace"]
    Services --> Store["Úložiště, zálohy, klíče, operace"]
    Services --> Commands["CommandRouter a DialogService"]
    Windows --> Main["Pracovní okno"]
    Windows --> Live["LiveWindow"]
    Windows --> Tools["ToolWindow"]
    Windows --> Detached["DocumentWindow"]
    Main --> Views["Pohledy / view models"]
    Live --> Views
    Tools --> Views
    Detached --> Views
    Views --> Commands
    Commands --> Audio
    Commands --> Docs
    Audio --> CPP["C++ / JUCE callback a společné hodiny"]
~~~

| Vrstva / umístění | Odpovědnost |
| --- | --- |
| Rocker.Domain | Identita dokumentů, čisté workspace modely, přechody relací a pravidla vlastnictví; bez WinUI, HWND a zvukového zařízení |
| Rocker.Infrastructure | Verze a atomické uložení rozložení, knihovna, soubory, databáze, zálohy, recovery; žádné odkazy na okna |
| Rocker.Interop + windows/native | Stávající audio kontrakt; UI se přes něj nepřipojuje přímo z každého okna |
| Rocker.Probe/Application | ApplicationServices, AudioSession, TransportCoordinator, PracticeCoordinator, OperationRegistry, ExitCoordinator; řízení práce nad doménou a úložištěm |
| Rocker.Probe/Desktop | WindowManager, WindowContext, WorkspaceController, CommandRouter, DialogService, DocumentManager a registr pohledů |
| Rocker.Probe/ViewModels | Stav konkrétní relace a nástroje; informování více UI bez singletonů XAML |
| Rocker.Probe/Views | Documents, Tools, Shell a Settings; nové samostatné kompozice místo dlouhých Build… metod |
| Rocker.Probe/Windows | LiveWindow, ToolWindow, DocumentWindow, SettingsWindow; minimální hostitelé |
| Rocker.Probe/Controls a Themes | Přebírané i upravené Vintage přístroje; témata podle každého XamlRoot |
| Rocker.Probe/Diagnostics | Funkční scénáře, výpis stavů a screenshoty; testovací logika se postupně oddělí od produkčního MainWindow |

ApplicationServices se vytvoří jednou při startu aplikace. Obsahuje stejný
UserFiles, DataRepository, autosave, služby médií, zvuk a koordinátory.
Neobalit pouze původní MainWindow singletonem; služby nemají držet konkrétní
UI prvky nebo XamlRoot. Migraci provést po blocích přes dočasné adaptéry.

AudioSession publikuje jeden neměnný balík pozorování ze společného
obnovovacího taktu. Okna neposílají při každém renderu změny do jádra.
Jediný UI DispatcherQueue pro první implementaci zjednoduší hostitele;
zpracování audia zůstává na stávajícím native callbacku. UI odběry mají
explicitní lifetime a odpojení. Skryté nástroje neprovádějí drahý rendering,
ale aplikační relace a sledování času pokračují podle skutečných důkazů.

Při přesunu dokumentu se nejprve odpojí starý hostitel a odběry, pak vytvoří
nebo připojí nový pohled nad stejnou relací. Jeden XAML element nemůže mít
dva rodiče. Notový Canvas/renderer a WebView2 potřebují vlastní ověřenou
strategii: preferovat přesun jediného pohledu, kde je bezpečný; pokud je
nutné vytvořit nový, uložit stav a převést vlastnictví bez duplicitního
zvuku. Technický limit nebo krátké přerušení videa musí být uvedené v D03/D22,
nikoli zatajené jako bezešvý přesun.

DialogService zná HWND a XamlRoot volajícího okna, serializuje konfliktní
dialogy a inicializuje systémové pickery u správného vlastníka. Asynchronní
výsledek se doručuje relaci, ne již zavřenému UI. Registr příkazů vyhodnocuje
CanExecute z modelu; změna aktivního okna mění cílení, nikoli audio transport.

Hranice typu „jedna aktivní mediální časová osa“ je součástí modelu
TransportCoordinator. Přidat testované rezervace/generace pro závody
Open/Play/Stop/Close; zachovat stávající native intent/ticket ochrany.
PracticeCoordinator účtuje čas jen jednou pro společnou aktivní práci,
bez násobení počtem otevřených oken nebo zrcadlených měřáků.

<a id="coverage"></a>
## 5. Matice převodu všech 32 pohledů

Každé původní ID zůstane známé pro staré sestavy, ResumeTask, hledání a
testovací fixture. Cílový host může být jiný; neznámá identita se bezpečně
odmítne. „Dokument“ může být hlavní záložka nebo odpojené okno.

| ID | Současná funkce | Cílová práce / rozložení | Kroky |
| --- | --- | --- | --- |
| 01 | Přehled | Úvodní dokument s pokračováním v přesné relaci, denní plán; z knihovny rovnou otevřít úlohu | D27, D29 |
| 02 | Live | Samostatné LiveWindow, kompaktní nástroje, viditelný poslech a záznam, F11 | D14 |
| 03 | Ladička | Připojitelný/plovoucí nástroj; detail ladění, kalibrace a referenčních tónů v nástroji | D11 |
| 04 | Metronom | Nástroj s kyvadlem nahoře, BPM/transportem a rozbalitelnými pokročilými volbami | D12 |
| 05 | Výběr lekce | Knihovna s kategoriemi, hledáním a detailem; otevře lekci jako dokument | D23 |
| 06 | Průběh lekce | Dokument: kroky vlevo, úloha uprostřed, kontext v inspektoru; relace uchová postup | D23 |
| 07 | Vlastní skladba | Dokument: waveform, smyčka a transport; vlastnosti zdroje v inspektoru | D17 |
| 08 | YouTube cvičení | Video dokument, A/B a rychlost u videa; existující oficiální embed, online stavy | D22 |
| 09 | Přesný trénink | Dokument hodnoticí relace, cílové tóny a skutečné výsledky; nastavení v inspektoru | D24 |
| 10 | Power chordy | Tréninkový dokument; rytmus, vizualizace a výsledky pohromadě | D24 |
| 11 | Gallop | Tréninkový dokument sdílí časování, samostatný druh úlohy a nastavení | D24 |
| 12 | Palm mute | Tréninkový dokument s kalibrací; nezaměnit proxy měření za nové rozpoznávání hry | D24 |
| 13 | Aparát | Nástroj: viditelný řetězec, preset, bypass a základní knoflíky; detaily efektu v nástroji | D13 |
| 14 | Nahrávky | Seznam v knihovně; vybraná nahrávka otevře editor waveform/A/B/značek | D18 |
| 15 | Studio | Projektový dokument: časová osa a stopy, mixer a inspektor; historie patří projektu | D20 |
| 16 | Looper | Připojitelný/plovoucí nástroj se skutečnou jednou looper relací, vrstvami a uložením | D19 |
| 17 | Akordy | Knihovna a referenční dokument s diagramem, prstoklady a cvičením | D25 |
| 18 | Stupnice | Knihovna a referenční dokument; diagram a nastavení podle výběru | D25 |
| 19 | Riffy | Knihovna; konkrétní riff otevírá dokument nebo navázaný trénink | D25 |
| 20 | Noty a tabulatury | Dokument: noty využijí plochu, lišta stop/zoomu/taktů, inspektor a společný transport | D21 |
| 21 | Hmatník | Herní/cvičební dokument; zacílená klávesnice a oddělená relace hry | D25 |
| 22 | Sluch | Herní/cvičební dokument; jasně označené ukázky a pravidla vlastnictví přehrávání | D25 |
| 23 | AI — rozhovor | AI dokument; historie, psaní, průběh a zrušení požadavku ve správné relaci | D26 |
| 24 | AI — plán | AI dokument; návrh plánu s výslovným použitím a vazbou na skutečné úlohy | D26 |
| 25 | AI — zpětná vazba | AI dokument se zdrojovými statistikami; nevydávat rady za měření audio hry | D26 |
| 26 | Moje kytara | Kategorie samostatného nastavení/profilu; související nástroje otevře příkazem | D27, D28 |
| 27 | Pokrok | Dokument statistik; skutečný čas, výsledky a režim bez odměn | D27 |
| 28 | Nastavení | Jedno SettingsWindow s kategoriemi, hledáním a vlastním správným HWND | D28 |
| 29 | Zvuková zařízení | Kategorie nastavení; ověření karty a mono vstupu, ASIO/WASAPI, buffer a latence ovladače | D28 |
| 30 | Zálohy a soubory | Kategorie správy dat; stav globálních operací v centru operací | D28, D30 |
| 31 | První spuštění | Průvodce ukotvený k pracovnímu oknu; krok poslechu výslovně vysvětlí monitoring | D28 |
| 32 | Export nahrávky | Vlastněný exportní dialog nad konkrétní nahrávkou; dlouhá operace v centru operací | D18, D30 |

Existující PWA import a kompletní záloha zachovají všechny dosavadní datové
skupiny, média a projektové vazby. Zkratka do knihovny nebo nová kompozice
není náhrada za funkční dokončení daného řádku.

<a id="implementation"></a>
## 6. Postup implementace D01–D36

Dosud nedokončené kroky mají stav „plánováno“. Při realizaci doplnit ke každému
revizi, důkaz a rozlišení implementováno / automaticky ověřeno / ručně ověřeno.
Uvedené nové soubory jsou zamýšlené cíle. Průběžné kroky nevyžadují ruční
klikání Jendy; Windows automatizace probíhá průběžně a ucelený kandidát
se předá až po D35/D36.

### Fáze A — Zadání a ověřitelný návrh

#### D01 — Zafixovat desktopový výchozí stav

- Stav: dokončeno. [Inventura a zachované kontrakty](docs/windows/desktop/baseline.md), [čerstvé lokální výsledky](docs/windows/desktop/baseline-validation.json). Zafixováno 388 zdrojů, 32 funkcí, 16 datových druhů a 22 hranic služeb. TypeScript, lint, 567/567 Vitest testů, doménové/datové kontroly a 21/21 Linux native sad prošly; skutečný Windows důkaz RC6 je vedený jako historický, nový multi-window runtime ještě neověřený.
- Závislosti: žádné.
- Změna: vytvořit inventář globálních služeb, polí MainWindow, všech operací a vedlejších účinků navigace; zafixovat data a scénáře RC6.
- Soubory: docs/windows/desktop/baseline.md; referenční podklady v docs/windows/baseline, w43 a metronome se použijí bez přepisování historie.
- Výsledek: každý řádek matice má zdrojové metody, data, cílovou relaci a zachované funkce; potvrzené vypnutí monitoringu je samostatný UX případ.
- Hotovo, když: inventář rozlišuje 32 funkcí, jeden audio host, vlastnictví média a existující versus nové chování.
- Ověření: aktuální doménové/datové/native kontroly; výchozí Windows důkaz nebo nový běh, pokud se podklad změnil. Nezaměnit kontrolu dokumentu za tento krok.

#### D02 — Navrhnout celý desktopový UX a grafiku

- Stav: dokončen návrh. [Galerie všech 32 pohledů](docs/windows/desktop/design-gallery.html), [specifikace](docs/windows/desktop/design-spec.md), [kontrola galerie](docs/windows/desktop/design-validation.json). Pracovní plocha, Live a plovoucí nástroje, oba vzhledy, stavy poslechu, 64 modal/theme scénářů a mobilní prohlížení ověřené v Chromium. Jde o návrhovou galerii s ukázkovými údaji; skutečné HWND a WinUI chování ověří D03.
- Závislosti: D01.
- Změna: rozpracovat pracovní plochu, Live, nástroje, dokumenty, knihovnu, Studio, inspektory a systémová okna včetně prázdných/chybových stavů.
- Soubory: docs/windows/desktop/design-spec.md, design-gallery.html a podklady; následné úpravy Themes/Vintage.xaml a Controls se teprve plánují.
- Výsledek: jeden konzistentní návrh s rozměry oblastí, hustotou, hierarchií akcí, chováním a mapováním všech 32 funkcí; návrhy ukázat společně.
- Hotovo, když: návrh pokrývá široké i malé pracovní okno, více oken, tmu/světlo/kontrast, vypnutý poslech a nedokončenou práci.
- Ověření: průchod úlohami „otevři lekci a metronom“, „hraj a nahraj“, „edituj projekt“, „noty na druhém monitoru“. Vizualizace jasně označit jako návrhy.

#### D03 — Ověřit více oken, docking a systémové chování

- Stav: dokončeno. [Rozhodnutí a scénáře](docs/windows/desktop/window-decision.md), [Windows důkazy](docs/windows/desktop/windows-validation.json). Běh 38437376867 nad revizí 11242c9 uspěl: 19 nových kontrol v obou diagnostických profilech, 22 native sad a instalační cyklus. Experiment zachoval text/zoom/výběr, WebView2 core/state a jediný audio host. Fyzický pointer drag, další monitor a produkční shell zůstávají dalšími scénáři.
- Závislosti: D02.
- Změna: malý skutečný WinUI řez s dvěma okny, jedním přesouvatelným dokumentem a nástrojem; vyzkoušet XamlRoot, HWND, tab drag/drop, WebView2 a notový pohled.
- Soubory: Desktop/WindowManager.cs, Windows/DocumentWindow.xaml, Windows/ToolWindow.xaml; docs/windows/desktop/window-decision.md.
- Výsledek: potvrzená strategie omezeného docking modelu, rámů, vlastníků, taskbaru, fokusů a UI threadu; případné limity přesunu videa zapsané.
- Hotovo, když: na Windows fungují přesuny, klávesnicová alternativa, dialog na správném okně a zavření pomocného okna bez ukončení procesu.
- Ověření: skutečný Windows build/smoke. Prototyp bez kytary; simulace Linuxem není důkaz Windows oken. Nepřidávat knihovnu bez ověření licence a potřeby.

### Fáze B — Oddělit služby od hlavního okna

#### D04 — Definovat dokumentové a workspace modely

- Stav: implementováno a lokálně ověřeno. [Modely a hranice](docs/windows/desktop/document-models.md), [72 kontrol](docs/windows/desktop/models-validation.json). Překlad starých ID, stabilní identity, layout a pravidla zavření/vlastnictví jsou čistá doména; dosud neřídí produkční shell. D03 Windows důkaz patří jeho vlastní zdrojové revizi, nikoli novým D04 modelům.
- Závislosti: D01, D03.
- Změna: stabilní DocumentKey, WindowRole, ToolId, DocumentState, WorkspaceLayout, transport owner a pravidla přechodů; zachovat překlad NavigationTarget.
- Soubory: Rocker.Domain/DesktopWorkspace.cs, DocumentSession.cs; Rocker.Domain/Navigation.cs pouze pro kompatibilitu starých ID.
- Výsledek: model neobsahuje HWND/XAML; odlišuje relaci, pohled, vlastníka zvuku a uložené rozložení.
- Hotovo, když: duplicate open aktivuje stávající relaci, přesun nemění její ID a neznámé/poškozené vstupy se odmítnou.
- Ověření: doménové scénáře open/move/close/cancel, limitů rozložení a převodu všech starých ID.

Aktuální realizační poznámky: [implementace](docs/windows/desktop/implementation.md), [migrace a návrat](docs/windows/desktop/migration.md). Finální Windows přejímka `69f6c88` v běhu 38477297083 uspěla: 43 desktopových kontrol, všech 32 funkcí ve dvou vzhledech, 22 native sad a celý instalační cyklus. [Skutečná galerie](docs/windows/desktop/gallery.html), [přejímací důkaz](docs/windows/desktop/candidate-validation.json). D36 rozlišuje automaticky dokončeného kandidáta od ruční hardwarové přejímky.

#### D05 — Zavést aplikační služby a koordinaci operací

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D04.
- Změna: přesunout UserFiles, DataRepository, autosave, nastavení, sdílená média, klíče a registry operací mimo MainWindow; zachovat jednoho databázového zapisovatele.
- Soubory: App.xaml.cs, MainWindow.Persistence.cs, Application/ApplicationServices.cs, OperationRegistry.cs; existující Infrastructure služby.
- Výsledek: okna dostávají služby a své WindowContext; operace přežijí přemístění UI a umějí doručit výsledek živé relaci.
- Hotovo, když: nový hostitel nepotřebuje skrytý MainWindow ani další databázi; každá dlouhá operace má vlastníka a cancellation.
- Ověření: ukládání a flush z více pohledů, závod zavření/výsledek, selhání DB a zachování původních médií.

#### D06 — Převést audio a transport na jednu aplikační relaci

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D05.
- Změna: AudioSession a TransportCoordinator převezmou NativeAudio, snapshoty, nastavení aparátu/metronomu a native intent/tickets.
- Soubory: MainWindow.xaml.cs, MainWindow.Amp.cs, MainWindow.Metronome.cs, Application/AudioSession.cs, TransportCoordinator.cs; Interop se mění jen při prokázané nutnosti.
- Výsledek: jeden host a jeden publish cyklus pro všechna okna; mediální příprava a spuštění respektují vlastníka a konflikt se záznamem/looperem.
- Hotovo, když: otevření/odpojení/zavření pohledu nemění host count, device starts, epochu ani zdroj; Stop all má jednu autoritu.
- Ověření: současné native routing/transport/recording kontrakty, závody start/stop a příprava druhého dokumentu při hraní prvního.

#### D07 — Centralizovat trénink, napájení a shutdown

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D05, D06.
- Změna: PracticeCoordinator a ExitCoordinator převezmou hodnocení, čas cvičení, wake lock, sleep/hotplug, close guards a životnost instance listeneru/mutexů.
- Soubory: App.xaml.cs, MainWindow.NativeEvents.cs, MainWindow.PracticeSession.cs, MainWindow.Shell.cs; Application/PracticeCoordinator.cs, ExitCoordinator.cs.
- Výsledek: přepnutí záložky nemá skryté globální stop; jedna aktivní hodnoticí relace a jedno účtování času.
- Hotovo, když: zavření pomocného okna neukončí aplikaci a zrušené ukončení zachová všechna okna; finální shutdown uvolní služby přesně jednou.
- Ověření: nahrávání během zavření Live, odmítnuté uložení, sleep při exportu, jedna power reakce, instance aktivace dokud běží aplikace.

#### D08 — Zavést příkazy a dialogy s vlastníkem

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D04, D05, D07.
- Změna: CommandRouter, registr příkazů, WindowContext a DialogService; globální versus dokumentové akce, pickery a serializace potvrzení.
- Soubory: MainWindow.Files.cs, MainWindow.Accessibility.cs, Desktop/CommandRouter.cs, DialogService.cs, WindowContext.cs.
- Výsledek: stejné akce z menu, toolbaru a zkratky; uložení a undo míří do aktivního dokumentu, systémový dialog patří volajícímu oknu.
- Hotovo, když: změna fokusu nemění transport a modalita neblokuje nesouvisející dokument; zrušený picker nic nepřipraví.
- Ověření: zkratky v textu/slideru/NumberBox, více oken, zavření vlastníka během async akce a konflikt dvou dialogů.

### Fáze C — Pracovní plocha a skutečné nástroje

#### D09 — Postavit nový hlavní shell

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D02, D08.
- Změna: nahradit stránkovou navigaci knihovnou, TabView hostem, inspektorem, nástroji, společným transportem a stavem operací; přidat děliče a skrývání oblastí.
- Soubory: MainWindow.xaml, MainWindow.Shell.cs, Views/Shell/WorkspaceView.xaml, LibraryPane.xaml, InspectorPane.xaml, TransportBar.xaml.
- Výsledek: pracovní rozložení z oddílu 2, nezávislé scroll oblasti; menu Okno/Zobrazení a jasný zdroj přehrávání.
- Hotovo, když: hlavní akce jsou dostupné bez scrollování celé stránky a vypnuté audio neskrývá užitečný obsah.
- Ověření: geometrie v 1280×720 a 1440×900, úzké okno podle monitoru, klávesnicové oblasti a přístupné děliče.

#### D10 — Implementovat hostitele a přesuny nástrojů

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D03, D09.
- Změna: registr WindowManager, jedna identita každého nástroje, attach/detach, fokus, systémové rámy a explicitní Vždy navrchu.
- Soubory: Desktop/WindowManager.cs, ToolHost.cs, Windows/ToolWindow.xaml a jejich view models.
- Výsledek: plovoucí nástroje jsou skutečná Windows okna; přesun má příkazovou i drag/drop cestu.
- Hotovo, když: druhé vyvolání aktivuje existující nástroj a přemístění zachová stav; X nástroje pouze skryje UI.
- Ověření: přesuny při běhu zvuku, alt-tab/minimalizace, odběry bez úniku a jedna plná UI instance nástroje.

#### D11 — Přestavět ladičku

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D06, D10.
- Změna: samostatný nástroj s dominantní notou a odchylkou; ladění, referenční tóny a kalibrace dostupné uvnitř něj.
- Soubory: MainWindow.Tuner.cs → Views/Tools/TunerView.xaml a ViewModels/TunerViewModel.cs; Controls/PitchGauge.
- Výsledek: kompaktní i plná kompozice sdílí stejnou analýzu a konfiguraci; žádná syntetická nota při tichu.
- Hotovo, když: přemístění nerestartuje kalibraci, nevytváří další analysis odběr ani automatický referenční tón.
- Ověření: stale/nejistý signál, změna ladění, reference, chybějící zařízení a synchronní zobrazení v Live.

#### D12 — Přestavět metronom

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D06, D10.
- Změna: kyvadlo/doby, BPM/tap a transport nahoře; rozšířený editor rytmu v nástroji, jasné stavy odpočtu/tichých taktů.
- Soubory: MainWindow.Metronome.cs → Views/Tools/MetronomeView.xaml a ViewModels/MetronomeViewModel.cs; Controls/MetronomeVisual.cs.
- Výsledek: plná a Live vizualizace čtou stejný native beat/phase; nastavení a volba Doby/Kyvadlo se dál ukládají.
- Hotovo, když: přesun a přepnutí vizualizace nemění časování, epochu ani monitoring a neotevírá zvukovku.
- Ověření: zachovat 20 existujících metronome scénářů po převodu; lichý takt, 16 dob, count-in, gap, reduced motion a úzké plovoucí okno.

#### D13 — Přestavět aparát

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D06, D10.
- Změna: viditelný efektový řetězec, preset/bypass a základní parametry; detaily vybraného efektu v nástroji, žádné dlouhé seznamy velkých karet.
- Soubory: MainWindow.Amp.cs → Views/Tools/AmpView.xaml a ViewModels/AmpViewModel.cs; Controls/ValueDial a VintageHousing.
- Výsledek: jeden globální preset a řetězec sdílený s Live; knob má dostupný nativní číselný vstup.
- Hotovo, když: bypass je zřetelný, Live a plný nástroj se shodují, plovoucí okno neobnovuje starý preset.
- Ověření: živé změny, uložení presetů, theme/kontrast a rozdíl aparát bypass / vypnutý monitoring.

#### D14 — Postavit samostatné Live a vysvětlit monitoring

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D07, D11, D12, D13.
- Změna: LiveWindow s poslechem, ladičkou, metronomem, kompaktním aparátem a nahráváním; připojitelný detail měřáků a F11.
- Soubory: MainWindow.Live.cs, MainWindow.Focus.cs → Windows/LiveWindow.xaml, Views/LiveView.xaml, ViewModels/LiveViewModel.cs; společný monitoring status model.
- Výsledek: při vypnutém poslechu je důvod u ovladače i mixu a skutečný příkaz Zapnout poslech; zdroj signálu a výstup jsou oddělené.
- Hotovo, když: zavření Live při záznamu zachová záznam a viditelný stav v hlavním okně; opětovné otevření obnoví pohled bez automatického zapnutí.
- Ověření: vypnutý monitoring, gain 0, bypass, metronom v mixu při vypnuté kytaře, silent input, zařízení unavailable, jeden audio host.

#### D15 — Ukládat a obnovovat víceokenní rozložení

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D04, D10, D14.
- Změna: versioned WorkspaceLayoutStore, okna/panely/záložky, monitor/DPI fitting a výchozí Cvičení/Hraní/Studio.
- Soubory: Rocker.Infrastructure/WorkspaceLayoutStore.cs, WindowPreferences.cs; Desktop/WorkspaceController.cs.
- Výsledek: deterministická obnova a příkaz Obnovit výchozí rozložení; staré window.json poskytne počáteční hlavní bounds/theme.
- Hotovo, když: chybějící monitor, chybný soubor i budoucí verze rozložení bezpečně obnoví použitelné UI, nepoškodí data a nespustí audio.
- Ověření: negativní souřadnice druhého monitoru, DPI změna, malá work area, limity počtu oken, atomický zápis, topmost a skrytý panel.

### Fáze D — Dokumenty a média

#### D16 — Implementovat dokumentový host a registry

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D04, D08, D09, D15.
- Změna: DocumentManager, tab order, dirty indicator, duplicate activation, detach/reattach a stav aktivního dokumentu nezávislý na transportu.
- Soubory: Desktop/DocumentManager.cs, DocumentHost.cs, Windows/DocumentWindow.xaml, ViewModels/DocumentViewModel.cs.
- Výsledek: jedna relace konkrétního objektu může změnit okno bez ztráty práce; dokumentový host neurčuje zvukové jádro.
- Hotovo, když: přesun zachová identitu, výběr a undo; zrušené zavření či neúspěšné uložení dokument nezničí.
- Ověření: dva různé dokumenty, otevření stejného souboru jinou cestou, změna aktivního okna, drag/drop zrušení a zavření vlastníka zvuku.

#### D17 — Převést skladby a společný mediální transport

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D06, D16.
- Změna: každá skladba dostane relaci; waveform, A/B, rychlost a pozice v dokumentu, zdrojové údaje v inspektoru.
- Soubory: MainWindow.Songs.cs, MainWindow.Preparation.cs, Views/Documents/SongView.xaml, ViewModels/SongViewModel.cs.
- Výsledek: více otevřených skladeb s uchovanými pozicemi; pouze explicitně zvolená skladba ovládá společný mediální slot.
- Hotovo, když: otevření druhé skladby nezastaví první ani tiše nezmění clip; přechod řeší záznam a konfliktní zdroje.
- Ověření: příprava/seek/loop během změny okna, chybějící soubor, cancel importu a waveform ze skutečně dekódovaného média.

#### D18 — Převést nahrávky, značky a export nahrávky

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D07, D16, D17.
- Změna: nahrávky v knihovně, dokument waveform se značkami a A/B; záznam společný v Live/transportu; export nad konkrétním RecordingId.
- Soubory: MainWindow.Recordings.cs, MainWindow.RecordingWaveform.cs → RecordingView, RecordingViewModel, ExportRecordingDialog.
- Výsledek: dry/processed/mix bus i předtakt zachované; export nepoužije právě vybranou jinou nahrávku po změně fokusu.
- Hotovo, když: zavření pohledu nezahodí take, zrušené uložení nechá draft dostupný a přenesené značky/metadata zůstanou přesné.
- Ověření: záznam během přesunu, zero monitor versus processed bus, stereo mix, chyba disku, přepsání/export cancel a uchování zdroje.

#### D19 — Převést looper na sdílený nástroj

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D06, D10, D18.
- Změna: LooperSession, panel vrstev a jasný transport/stav pending/record/play; zachovat vrstvy a uložené výsledky.
- Soubory: MainWindow.Looper.cs → Views/Tools/LooperView.xaml, ViewModels/LooperViewModel.cs, Application/LooperSession.cs.
- Výsledek: jedna běžící looper relace může být viditelná v nástroji nebo Live a uložit výsledek do knihovny/projektu.
- Hotovo, když: přemístění nevymaže vrstvy a skrytý looper zůstane viditelný jako zdroj; konkurenční media příprava má definované rozhodnutí.
- Ověření: kvantizované hranice, stop/save pending, limity paměti, přejmenování vrstvy, device reset a opětovné otevření.

#### D20 — Přestavět Studio jako projektový editor

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D16, D18, D19.
- Změna: samostatná relace každého projektu, časová osa a seznam stop, výběrový inspektor, mixer a dostupné import/export/save; historie v projektu.
- Soubory: MainWindow.Studio.cs → Views/Documents/StudioView.xaml, ViewModels/StudioViewModel.cs, Application/StudioSession.cs; ProjectRepository zachovat.
- Výsledek: dvě otevřené projektové relace mohou mít odlišný výběr a undo, ale pouze jeden aktivní projektový transport.
- Hotovo, když: detach zachová výběr, zoom, neuložené úpravy i historii a záznam se připne k původnímu správnému projektu.
- Ověření: přepnutí při overdubu, skutečné alignment/tickets, poškozená/chybějící média, per-project undo a export nepřepíše původní soubory.

#### D21 — Přestavět notový dokument

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D03, D16, D17.
- Změna: celoplošný notový host, lišta stop a zoomu, inspektor taktů/smyčky; oddělit ScorePreview/model od jediného okna.
- Soubory: MainWindow.Notation.cs, notová část MainWindow.xaml → ScoreView, ScoreViewModel; Rocker.Notation jen při nutné změně hostování.
- Výsledek: více otevřených partitur s vlastním stavem; explicitní převzetí syntetického přehrávání nad společným mediálním slotem.
- Hotovo, když: výběr taktu, opakování, zoom, follow a smyčka zůstanou po odpojení; netvrdit, že syntéza je původní nahrávka.
- Ověření: GP/MusicXML fixtures, resize/DPI, hit testing, renderer lifetime, přechod na jiný score při hraní a chybné soubory.

#### D22 — Převést video cvičení a WebView2

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D03, D07, D16, D17.
- Změna: VideoDocumentSession a vlastní host; oddělit bridge/profile a uložené smyčky od MainWindow; explicitní vlastnictví video výstupu.
- Soubory: MainWindow.Video.cs, MainWindow.VideoPractice.cs → VideoView, VideoViewModel, VideoDocumentSession; Rocker.Video a Video bridge.
- Výsledek: změna záložky neprovádí skryté pause. Přesun dodrží ověřenou strategii D03; zavření vlastníka video zastaví.
- Hotovo, když: nepřibude duplicitní embed/playback, origin a navigační/permission pravidla zůstanou a online chyba neblokuje jiné dokumenty.
- Ověření: lokální bridge, A/B/rate, nové HWND, crash WebView2, souběh s native zdroji; skutečný YouTube/ASIO souběh zůstává ručně označený.

### Fáze E — Výuka, knihovna a pracovní kontext

#### D23 — Převést lekce a jejich výběr

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D07, D16.
- Změna: knihovna lekcí a LessonDocumentSession; kroky/instrukce/úloha v dokumentu, konkrétní nastavení v inspektoru.
- Soubory: MainWindow.Lessons.cs → LessonLibraryView, LessonView, LessonViewModel; stávající Rocker.Domain/LessonSession.
- Výsledek: pokračování otevře přesný úkol; rozpracovaná relace nepřijde o krok při změně okna.
- Hotovo, když: otevřené dvě lekce se nehodnotí zároveň a dokončení jedné správně obnoví plán/pokrok.
- Ověření: stejná note-by-note pravidla a referenční drill případy, dirty/session close, odkazy na konkrétní trenažér a chybějící obsah.

#### D24 — Převést přesný, rytmický a palm mute trénink

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D07, D12, D23.
- Změna: dokumenty přesného tréninku, power chordů, gallopu a palm mute; společný koordinátor aktivní hodnoticí relace.
- Soubory: MainWindow.Surgical.cs, MainWindow.Rhythm.cs, MainWindow.PalmMute.cs → příslušné Views/Documents a view models; doménové algoritmy zachovat.
- Výsledek: hodnocení vychází z původních skutečných vstupů; přepnutí nezamění tréninkovou úlohu ani neúčtuje čas dvakrát.
- Hotovo, když: Pause/Stop/Close mají konkrétní výsledek a spuštění konkurenčního tréninku má výslovný přechod.
- Ověření: nota-gate, preview origin, onset/calibration, epoch reset, off-window running, chybějící signál; žádný nový slib detekce celé techniky.

#### D25 — Převést hudební knihovnu a hry

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D07, D16, D23.
- Změna: kategorie akordů/stupnic/riffů v knihovně, referenční dokumenty a relace Hmatník/Sluch; hledání podle obsahu.
- Soubory: MainWindow.Library.cs, MainWindow.Riffs.cs, MainWindow.Games.cs → knihovní/dokumentové views a view models; existující diagramy.
- Výsledek: otevření riffu/cvičení má přímou návaznost na konkrétní úlohu; audio ukázka není druhý nezávislý transport.
- Hotovo, když: diagramy, handedness, uložené rekordy a režim bez odměn zůstávají; přesun nepřehraje ukázku znovu.
- Ověření: klávesnice nad hmatníkem, preview cancel, ochrana pracovního média, licenční metadata obsahu a baseline pravidla.

#### D26 — Převést tři AI pohledy

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D05, D16, D23.
- Změna: AI dokumenty a request relace; průběh/cancel, výsledky a použití plánu se řídí vlastníkem, nikoli aktuální navigací.
- Soubory: MainWindow.Ai*.cs → Views/Documents/Ai…, view models a AI session; SecretStore/AiHttpClient zachovat.
- Výsledek: odpověď dorazí do původního dokumentu po změně fokusu; otevření AI dokumentu samo nic neposílá.
- Hotovo, když: zavření řeší probíhající request, chybějící klíč je srozumitelný a rady stále uvádějí skutečný zdroj statistik.
- Ověření: offline/timeout/error/cancel, zánik hostitele, DPAPI a redakce citlivých údajů; skutečné online volání označit zvlášť.

#### D27 — Převést přehled, pokrok a profil kytary

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D07, D23, D24, D25.
- Změna: přehledový a statistický dokument, pokračování v konkrétní úloze, profil ve správě nastavení; agregace událostí z více oken.
- Soubory: MainWindow.Overview.cs, Progress.cs, Instrument.cs → přehledové views/view models; PracticeSession a ProgressRules zachovat.
- Výsledek: jedna tréninková aktivita má jedno účtování času a jeden výsledek; změny profilu se promítnou do otevřených diagramů a nástrojů.
- Hotovo, když: pokrok reaguje na dokončení úlohy bez vynuceného návratu domů, čas nezvyšuje samotné otevření okna.
- Ověření: monotónní hodiny, pauzy/přestávky, půlnoc, levá ruka, režim bez odměn a změna profilu během otevřených dokumentů.

#### D28 — Převést nastavení, zařízení a první spuštění

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D08, D14, D15, D27.
- Změna: jedno SettingsWindow s kategoriemi včetně zařízení, profilu a souborů; vlastněný průvodce, rozlišení zvuk/poslech/efekty.
- Soubory: MainWindow.Settings.cs, Devices.cs, Secrets.cs, FirstRun.cs, Backups.cs → Windows/SettingsWindow.xaml a kategoriové views.
- Výsledek: skutečný výběr ASIO/WASAPI, mono vstup a stereo výstup na M-Track Solo, dostupné formáty a odhady ovladače; žádný slib naměřené celkové latence.
- Hotovo, když: reconfigure bezpečně řeší aktivní záznam a změnu oznámí všem oknům; první spuštění nespouští monitoring samo.
- Ověření: dostupnost zařízení, odmítnutý formát, DPAPI, změna theme ve všech oknech, opětovné vyvolání nastavení a zachování poslední funkční konfigurace.

#### D29 — Propojit sestavy, hledání, oblíbené a soubory

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D15–D28.
- Změna: staré NavigationTarget přeložit do dokumentu/nástroje; Ctrl+K, favorites, ResumeTask, recent files, asociace a drop používají jednotný registr.
- Soubory: MainWindow.PracticeWorkspace.cs, Files.cs, Ux.cs; Desktop/WorkspaceController.cs a registr mapování.
- Výsledek: sestava může otevřít správnou práci bez ztráty ostatních dokumentů; rozložení se neplete se zvukovým presetem.
- Hotovo, když: všech 32 ID má dosažitelnou akci a legacy sestavy fungují; příchozí soubor neobchází konflikty přehrávání/záznamu.
- Ověření: staré fixture, duplicitní soubor, aktivační queue při modálním dialogu a při exportu, drop do různých oken, Ctrl+K bez audio side effects.

### Fáze F — Data, spolehlivost a finální grafika

#### D30 — Dokončit operace, zálohy, recovery a aktualizace

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D05, D07, D18, D20, D26, D28, D29.
- Změna: centrum operací, globální bariéry pro obnovu a update, cancellable import/export; zálohování rozložení jako explicitní volba.
- Soubory: MainWindow.Backups.cs, Migration.cs, Recovery.cs, Updates.cs; OperationRegistry, BackupService, ColdProfileRecovery a UpdatePreparation.
- Výsledek: ukončení, obnova a aktualizace vědí o všech relacích a oknech; žádný skrytý draft, request nebo druhý instalátor.
- Hotovo, když: data/media se flushnou před vypnutím, secret/caches se nezálohují a obnova rozložení nemůže spustit operace.
- Ověření: full backup všech skupin/médií, cancel/failure, interrupted restore, update backup a návrat při chybě; soukromé release ověřování zachovat.

#### D31 — Ošetřit chyby, závody a odběry

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D10–D30.
- Změna: sjednotit chyby a cleanup, omezit obnovené registry, odpojit event subscriptions, ošetřit stale callbacks a závody přemístění/close/stop.
- Soubory: Application, Desktop, view models; postupně odstranit dočasné MainWindow adaptéry, které už nemají uživatele.
- Výsledek: zavřená okna se neupravují z async pokračování a služby nemají reference na zaniklé XAML stromy.
- Hotovo, když: opakované přesuny/otevírání nezvyšují host count ani počet odběrů; selhání pomocného pohledu neshodí globální stav.
- Ověření: opakované open/move/close cykly, pozdní AI/media výsledek, fronty příkazů, hardware fault a úniky UI/renderer/WebView2 objektů.

#### D32 — Dokončit Vintage kompozici a přístupnost

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D02, D09–D31.
- Změna: sjednotit mezery, typografii, tituly, toolbar ikony, výběr, minimum nástrojů a inspektor; dokončit theme/DPI/fokus všech oken.
- Soubory: Themes/Vintage.xaml, Controls, všechny nové views; docs/windows/desktop/design-spec.md.
- Výsledek: desktopové rozložení s původními retro přístroji, přehledné stavy a skutečně dostupné ovladače místo pouze dekorace.
- Hotovo, když: všechny funkce mají dostupnou klávesnicovou cestu, žádné klíčové tlačítko se neztratí při zmenšení a HC nepoužívá textury místo kontrastu.
- Ověření: 100/150/200 % DPI, tma/světlo/HC, reduced motion, UI Automation názvy a fokus; fyzický Narrator/monitor přesuny evidovat samostatně.

### Fáze G — Automatická přejímka a kompatibilita

#### D33 — Přestavět automatizaci na okna a relace

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D16–D32; scénáře zakládat průběžně už od D03.
- Změna: nahradit kontroly pouze navigovaných stránek registrem funkčních scénářů nad okny/dokumenty; zachovat baseline doménových/native/datových kontraktů.
- Soubory: Rocker.Domain.Checks, Rocker.Infrastructure.Checks, Rocker.Probe/Diagnostics; windows/smoke.ps1, verify-candidate.ps1 a Windows workflow.
- Výsledek: reporty mapují 32 funkcí, desktopové scénáře a zdrojovou revizi; nový počet snímků není natvrdo 35 ze starého shellu.
- Hotovo, když: build neprojde s chybějícím pohledem, neprovedeným testem nebo zastaralým reportem a důkazy rozlišují simulovaný native signál/hardware.
- Ověření: nezměnit smysl původních 22 native suites ani 20 metronome kontrol; nové scénáře počítat podle skutečné evidence, netvrdit staré počty pro nový běh.

#### D34 — Ověřit migraci a bezpečný návrat

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D15, D29, D30, D33.
- Změna: přechod RC6 → desktopový kandidát se zálohou profilu; oddělit nový versioned layout od hudebních dokumentů a předem popsat rollback.
- Soubory: WorkspaceLayoutStore, Infrastructure compatibility checks, windows/installer/smoke.ps1; docs/windows/desktop/migration.md.
- Výsledek: stará data, sestavy, značky, owned media, projekty a tajné údaje zůstávají použitelné; žádná nová DB migrace bez samostatné potřeby.
- Hotovo, když: update i neúspěšný update/repair zachovají data a asociace; starší aplikace nemusí číst nové rozložení, ale nesmí přepsat novější hudební data.
- Ověření: staré zálohy, window.json v1, chybný/budoucí layout, dirty projekty před update, úplnost médií/hashů a návrat k záloze bez tiché ztráty nových změn.

#### D35 — Dokončit galerii a uživatelský návod

- Stav: implementováno; automatická Windows evidence a hranice ruční přejímky jsou v [matici](docs/windows/desktop/acceptance.md) a [finálním reportu](docs/windows/desktop/candidate-validation.json).

- Závislosti: D32, D33, D34.
- Změna: skutečné Windows screenshoty všech cílových funkcí a významných společných oken; video/GIF přesunů jen z opravdového runtime, oddělit syntetický signál.
- Soubory: docs/windows/desktop/gallery.html, runtime snímky a validation JSON; windows/w43 user guide a balená nápověda podle zdroje balení.
- Výsledek: všechny návrhy a finální snímky lze prohlédnout společně; návod ukazuje knihovnu, okna, zdroj transportu, monitoring a obnovu rozložení.
- Hotovo, když: galerie má čerstvou revizi/run, žádné staré obrázky vydávané za nový shell a všechny lokální odkazy fungují.
- Ověření: skutečná geometrie a UI stavy, mobilní prohlížení galerie, image decode/modal/filter; snímek nedokazuje skutečnou odezvu zařízení.

### Fáze H — Sestavený kandidát a závěrečná přejímka

#### D36 — Sestavit, předat a uzavřít desktopového kandidáta

- Stav: automaticky dokončeno. [Neveřejný koncept vydání](https://github.com/JendaNDT/rocker/releases/tag/untagged-2c740c526ba4bfde626b), [ověření všech nahraných souborů](docs/windows/releases/delivery-validation.json), Windows běh 38477297083, publisher 38479952180. Veřejný repozitář nevedl k veřejnému vydání. Ruční hardware/stabilní přejímka W44–W48 zůstává neprovedená.

- Závislosti: D01–D35.
- Změna: dokončit Windows build, nativní/datové/UI kontroly a instalační cyklus, vybrat přesnou revizi/tag; publikovat soukromý instalátor a zdroje zvlášť.
- Soubory: windows/build.ps1, package.ps1, installer, verify-candidate.ps1; docs/windows/releases/current.json a notes.md, PROJECT_STATUS.md.
- Výsledek: ucelená přestavěná aplikace, návod, známé limity a důkazy; Jenda ani bratr neinstalují SDK a nic nekompilují.
- Automaticky hotovo, když: správný source commit, úspěšný Windows run, kompletní reporty, instalační/aktivační/repair/uninstall testy a shoda hashů publikovaných balíků.
- Ručně hotovo až po: bratrově přejímce pracovních úloh a M-Track Solo, řešení závad a opakovaném sestavení oprav. Předání kandidáta není automaticky stabilní vydání.

<a id="milestones"></a>
## 7. Milníky a pořadí

| Milník | Rozsah | Co musí být prokazatelné |
| --- | --- | --- |
| M1 — Definitivní návrh | D01–D03 | Všech 32 funkcí zmapovaných; společná galerie návrhů; ověřená strategie více oken |
| M2 — Oddělené služby | D04–D08 | Jeden audio host; aplikace nezávislá na životnosti pohledu; explicitní příkazy, operace a shutdown |
| M3 — Desktopový základ | D09–D16 | Pracovní plocha, nástroje, Live, dokumenty a obnova rozložení v reálném Windows runtime |
| M4 — Média a projekty | D17–D22 | Více otevřených relací bez nečekaného převzetí zvuku; Studio, záznam, looper, noty a video |
| M5 — Úplnost funkcí | D23–D30 | Výuka, knihovna, AI, pokrok, profily, nastavení, sestavy, data a aktualizace převedené |
| M6 — Ucelená přejímka | D31–D35 | Stabilní lifecycle, konečná grafika, automatická matice a kompatibilita, skutečná galerie/návod |
| M7 — Předání | D36 | Sestavený soukromý kandidát; manuální výsledky a případné opravy vedené odděleně |

Čísla D určují výchozí pořadí. Pokud krok vyžaduje větší zásah, rozdělit jej
na malé komity se zachovaným funkčním buildem a jasnou dočasnou hranicí.
Nerozpracovat mnoho paralelních přepisů MainWindow. D33 se připravuje
průběžně; finální povinné scénáře jsou bránou před D36.

Neodhadovat délku celé přestavby z počtu řádků tohoto dokumentu. Po D03 a
D08 lze dát realistický odhad podle ověřeného hostování a skutečného rozsahu
extrakce služeb. Kritická návaznost je návrh → hostování → sdílené služby →
shell/nástroje → dokumenty → převedené funkce → přejímka/předání.

<a id="validation"></a>
## 8. Ověřování a podmínky předání

### Povinné desktopové scénáře

1. Otevřít lekci, noty a skladbu; přepínání zachová práci a samo nepřebere audio.
2. Odpojit aktivní dokument na druhé okno a znovu připojit; zachovat ID, výběr, pozici a historii.
3. Vyvolat metronom dvakrát; existuje jeden plný nástroj a jeden globální zdroj.
4. Při běžícím metronomu přesouvat nástroj; native epoch/tempo a beat jsou stabilní.
5. Live a plný nástroj ukazují stejný native snapshot, lichý takt i count-in.
6. Zapnout/vypnout monitoring; post-amp signál a mix se liší správně, stav vysvětluje důvod.
7. Pustit metronom/skladbu s vypnutým poslechem kytary; mix hraje a text netvrdí úplné ztlumení.
8. Začít nahrávání, zavřít Live a znovu otevřít; záznam pokračuje a je dostupný v transportu.
9. Zavřít vlastníka záznamu nebo celou aplikaci; uložit/zrušit/selhání uložení se chovají bezpečně.
10. Otevřít dva projekty; Ctrl+S/undo a záznam míří do správné relace, žádné dvě aktivní mediální časové osy.
11. Připravit jinou skladbu během přehrávání/záznamu; nesmí tiše vyměnit native clip.
12. Přepnout dokument při tréninku; aktivní hodnocení a čas se nespouštějí ani neúčtují znovu.
13. Přesunout noty/video; nevytvořit druhý přehrávač a zachovat doložená bridge pravidla.
14. Změnit okno během AI/exportu; výsledek patří původnímu objektu a cancellation je funkční.
15. Otevřít picker nebo potvrzení z odpojeného dokumentu; správný vlastník, fokus a zrušení.
16. Obnovit rozložení se zmizelým monitorem; okna jsou dosažitelná a zvuk zůstává vypnutý.
17. Nechat poškodit/změnit verzi layoutu; fallback nepoškodí uživatelská data.
18. Sleep/hotplug při více oknech; jedno bezpečné zastavení bez automatického monitoringu po návratu.
19. Ctrl+K, legacy sestava a asociovaný soubor otevřou správnou relaci; instance mutex platí pro celou aplikaci.
20. Příprava update/restore kontroluje všechna okna a konfliktní operace; cancel zachová funkční relace.
21. Opakovat open/detach/attach/close; nejsou trvale rostoucí registr oken, event odběry a hosty.
22. Main X ukončí všechna okna až po společném souhlasu; cancel neudělá částečný shutdown.

### Automatická evidence

Každý report obsahuje zdrojovou revizi, Windows run, konkrétní scénáře a
jejich passed/failed/skipped, podmínky, typ signálu a známé limity.
Potřebné nové reporty: desktop-windows.json, document-sessions.json,
desktop-audio-ownership.json, workspace-layout.json, desktop-migration.json
a desktop-coverage.json. Jména jsou plánovaná; v D33 se uzamkne skutečný
reportový kontrakt a kandidát jej bude povinně kontrolovat.

Dosavadní verify-candidate očekává 32 navigovaných pohledů a 35 snímků;
to po přestavbě nemusí odpovídat počtu hostitelů. Nahradit tento požadavek
explicitní maticí všech 32 funkcí, která navíc zahrnuje multi-window scénáře.
Nesnížit funkční pokrytí pouhým odstraněním starých assertionů.

- Pro změny čistých modelů spustit příslušné Domain/Infrastructure Checks a stávající frozen referenční případy.
- Pro audio/Interop zachovat native/interop kontrakty a měřit jednoho hosta, device starts, native clock a skutečný obsah mixu.
- Pro okna a finální geometrii vždy skutečné WinUI sestavení a spuštění na Windows; Linux ověří jen podporované části.
- Pro každý netriviální zásah zachovat projektovou TypeScript/Vitest kontrolu; počty hlásit z čerstvého běhu, ne z historického reportu.
- Sdílené .NET projekty stavět postupně nebo do oddělených výstupů; souběžné buildy nesmějí závodit o DLL.
- Při screenshotu vyčkat na skutečný ustálený layout, zaznamenat velikost/DPI/theme a label syntetických hodnot.
- Automatizace používá umělé profily a média. Žádné skutečné bratrovy soubory, klíče nebo tokeny v commitech/reportech.

### Ruční přejímka ucelené verze

Bratr ověří především: začít cvičit bez hledání stránek; naladit, zapnout
poslech a efekty; cvičit s metronomem; nahrát a najít výsledek; otevřít
noty/video vedle Live; upravit projekt a bezpečně ukončit. Dále testuje
M-Track Solo, výběr jednoho vstupu, sluchátka na stejném zařízení,
driver/buffer stabilitu, skutečnou odezvu a souběh YouTube podle backendu.
Reálný druhý monitor a fyzické změny DPI se ověří, pokud je má k dispozici;
jejich absence se zaznamená, nesmí být vydávána za průchod.

Jendovo potvrzení jedné opravené situace s monitoringem nenahrazuje
měření round-trip latence, dlouhý provoz ani celou W44–W48 přejímku.
Sestavený kandidát lze předat k testu s poctivě označenými neprovedenými
hardwarovými scénáři. Stabilní stav označit až po zhodnocení výsledků.

### Definice dokončení přestavby

- Všechny D01–D35 mají odpovídající výsledky a důkazy; všech 32 funkcí je dosažitelných a skutečně funguje.
- Hlavní práce probíhá přes dokumenty, knihovnu, inspektor a okna; žádný běžný postup nevyžaduje starou univerzální stránkovou navigaci.
- Existuje jeden audio host; zavírání/přesuny nemají skryté restartovací nebo datové účinky.
- Monitoring má vysvětlený stav; obnovení oken, attach/detach, menu, fokus a klávesnice jsou ověřené.
- Data, média, sestavy a historie podporované editace jsou zachované; neúspěšný přechod má bezpečný návrat.
- Windows build, automatické kontroly, instalace/update/repair/aktivace/uninstall a publikované hashe patří téže vybrané revizi.
- Galerie a návod odpovídají hotovému runtime. Ruční omezení a známé chyby jsou výslovně uvedené.
- D36 odděluje automaticky ověřeného kandidáta, skutečnou přejímku a stabilní předání.

<a id="risks"></a>
## 9. Rizika, omezení a návrat

| Riziko | Opatření / rozhodnutí |
| --- | --- |
| Více oken nad monolitickým MainWindow | Nejdřív extrakce služeb D05–D08; nekopírovat okno včetně jeho hostu |
| Souběžné dokumenty převezmou jediné média | Explicitní TransportCoordinator; příprava se řeší ještě před mutací shared native clip |
| Přesun UI spustí Unloaded a zruší relaci | Lifetime dat oddělený od pohledu; subscription cleanup je jiný než session close |
| Ztráta historie nebo state při detach | Stejná relace a per-document undo; renderer stav serializovat, nepřenášet pouze screenshot |
| WebView2 přes nové HWND | Windows experiment D03, doložený postup D22; nezdvojit embed ani původní bezpečnostní pravidla |
| Chybná modalita/fokus mezi okny | WindowContext, HWND/XamlRoot dialog ownership a jednotná fronta potvrzení |
| Více časovačů a nadbytečné UI práce | Jedna publikace snapshotů, bounded odběry, skryté pohledy nevykreslovat bez potřeby |
| Násobení statistického času | Jeden PracticeCoordinator nad skutečnou aktivitou, ne časovač v každém okně |
| Okno mimo obrazovku po změně monitoru | Versioned layout, DPI/work-area fitting a dostupné reset rozložení |
| Profesionální UX bude příliš složité | Připravené rozložení Cvičení; hlavní akce jasné, pokročilé parametry v inspektorech; více oken volitelné |
| Retro dekorace zhorší čitelnost | Omezené přístrojové materiály, nativní forms/lists, high contrast a ověřená minima |
| Staré reporty naznačí dokončení nového UI | Nová coverage a revize; staré W důkazy ponechat jako historii, ne důkaz D |
| Aktualizace změní data a rollback je nečitelný | Před-update záloha, oddělený layout soubor, dokumentované kompatibilní návraty; žádný automatický downgrade DB |
| Nová závislost zvýší licence/distribuci | Výchozí cesta bez nové docking závislosti; každé přidání zvlášť zkontrolovat a zahrnout do sources/notices |

RC6 zůstává poslední konkrétní funkční release, dokud nový kandidát
neprojde automatickými branami. Nezveřejňovat rozpracovaný shell jako
hotový update. Před změnou verze uložit profilovou zálohu. Návrat k RC6
není automatické obnovení staré zálohy přes nová data: nejprve zazálohovat
aktuální stav a nabídnout jasné rozhodnutí o práci vzniklé po aktualizaci.

## 10. Zahájení práce a údržba plánu

První implementační krok je D01, následuje společný návrh D02 a Windows
experiment D03. Jenda 13. 10. 2026 výslovně schválil zahájení realizace
celého tohoto plánu („Ok. Tak se můžeš pustit do práce.“). Schválení
není důkazem dokončení jednotlivých kroků; jejich stavy a evidence se vedou zvlášť.
Při zahájení realizace navázat na dohodnutý postup: malé návazné změny,
automatické kontroly a sestavení průběžně, uživatelské testování uceleného
kandidáta. Zachovat aktuální pravidla [AGENTS.md](AGENTS.md).

Při změně rozsahu aktualizovat současně matici 32 funkcí, kroky, milestone
a přejímací scénáře. V PROJECT_STATUS uvádět skutečný nejbližší krok,
zdrojovou revizi a zda existuje sestavený kandidát. V implementačních
commitech psát kód, komentáře a commit message anglicky; UI a vysvětlení
uživateli česky. Nevyžadovat od Jendy kompilaci, SDK nebo test každého řezu.
