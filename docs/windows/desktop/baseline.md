# D01 — zafixovaný výchozí stav desktopové přestavby

Datum: 13. 10. 2026. Inventura vychází z revize
1e3e861 a hotového RC6, nikoli z původní PWA. D01 nemění chování aplikace.
Nezávislou historickou webovou referenci zachovává
[původní baseline](../baseline/README.md).

## Výsledky a reprodukovatelnost

- [inventory.json](inventory.json): 32 funkcí, jejich současné hostování, cílové relace, vlastnictví služeb, navigační účinky, operace a datové hranice.
- [source-inventory.json](source-inventory.json): read-only Roslyn soupis zdrojů, polí a metod MainWindow a pojmenovaných XAML prvků; hodnoty inicializátorů ani osobní data se nesbírají.
- [source-manifest.json](source-manifest.json): SHA256 zdrojů aplikace, domény, datových služeb, native/interop modulů, obsahu, buildů a referenčních fixture.
- [baseline-validation.json](baseline-validation.json): čerstvé lokální kontroly a odkaz na existující skutečnou Windows přejímku RC6; ruční scénáře jsou uvedené odděleně.
- [verify-windows-desktop-baseline.mjs](../../../scripts/verify-windows-desktop-baseline.mjs): ověřuje inventuru proti zafixované revizi, nikoli proti nově přepsanému programu.

Roslyn načetl 63 C# zdrojů aplikace, 362 polí a 441 metod přímo v partial
MainWindow. XAML má 133 pojmenovaných prvků napříč pohledy a komponentami.
Nejde o počet doménových funkcí ani počet služeb. Údaje slouží k dohledání
skutečných vazeb; nový kód se nebude automaticky generovat z tohoto soupisu.

## Vlastnictví služeb a stavů

| Oblast | Dnešní vlastník / stav | Cílová hranice |
| --- | --- | --- |
| Proces a aktivace | App.window, instanceMutex, setupGuard, instanceListener jsou navázané na hlavní okno | App + WindowManager + ExitCoordinator; mutex po celou životnost procesu |
| Audio a snapshoty | MainWindow.audio a timer; Closed uvolňuje host, RefreshState čte a rozesílá pozorování | Jeden AudioSession, sdílené snapshoty; audio lifetime aplikace |
| Zvukové zařízení | MainWindow.Devices; preference v AppSettings, katalog/capabilities a reconfigure stavy | AudioSession a globální Device operation; UI ve SettingsWindow |
| Aparát | ampApplyTimer, parametry, presety a ovladače jsou v MainWindow.Amp | Globální Amp view model a AudioSession; plný nástroj i Live čtou stejný stav |
| Metronom | metroEnabled, config, revisions, timer, seznam weak vizualizací | Globální Metronome view model; zachovat native fázi, presety a delayed apply |
| Monitoring | liveMonitor, liveGain a liveUpdating v Live; gain není uložený přepínač pro automatický restart | AudioSession stav poslechu; renderer a UI nesmějí jej měnit při otevření okna |
| Analýza/ladička | native analysis, kalibrace a ref. tón; UI MainWindow.Tuner/Live | Globální Tuner view model a sdílená analýza |
| Soubory a soukromý profil | userFiles, database, autosave a appSettings žijí v MainWindow | ApplicationServices, jediná datová relace a koordinovaný flush |
| Skladba a noty | song/mediaPath/mediaMidi/preview, position, speed a jeden native clip | Document sessions + TransportCoordinator, nejvýše jedna aktivní mediální časová osa |
| Video | jeden VideoView a bridge; videoPractice, smyčky a profile dir v MainWindow | VideoDocumentSession a jediný host přehrávaného embed; bezpečná migrace HWND |
| Nahrávání | captureDraft, markers a recording metadata; tlačítka ve více pohledech | Globální RecordingSession a jasný vlastník výsledku |
| Studio | jeden studioProject, undo max 20 snapshotů, pending edits, studioTicket a captureProject | Per-project session; explicitní media vlastník a historie konkrétního projektu |
| Looper | jeden native looper; UI, fingerprint dirty stavu a project ID v okně | Globální LooperSession, oddělený lifetime panelu a vrstev |
| Lekce | lessonSessions dictionary a savedLessonPositions; UI vybraného kroku | Per-lesson document; jedna aktivní hodnoticí relace |
| Trenažéry | chirurgický gate, rhythm state a palm calibration plus navigační podmínky | Dokumentové relace a PracticeCoordinator; změna fokusu nezmění hodnoticí úlohu |
| Knihovna a hry | catalog, výběry a herní stavy; preview sdílí audio | Knihovna + referenční/herní dokumenty, řízený preview owner |
| AI | aiActive, request/cancellation a jednotlivé stránky | AI document/request session; výsledek patří konkrétnímu objektu |
| Čas a pokrok | practiceSession, last evidence/frame, DailyMinutes a wake lock v okně | Jeden PracticeCoordinator pro všechna okna |
| Sestavy a hledání | PracticeWorkspace, favorite entries, resume target, navigation | Překlad starých ID do dokumentového registru a nástrojů |
| Zálohy/recovery/update | globální operace a bariéry rozptýlené po partial MainWindow | OperationRegistry, ExitCoordinator a aplikační update/restore |
| Dialogy/fokus | pickery inicializované HWND tohoto MainWindow, ContentDialog se ShellRoot.XamlRoot | WindowContext a DialogService konkrétního volajícího okna |
| Rozložení | window.json v1; jedna normalBounds/theme, uložené při odchodu | Samostatný versioned multi-window layout, fitting podle monitoru/DPI |

Pole z source-inventory jsou explicitně dohledatelná podle souboru, typu,
jména a řádku. Tabulka definuje vlastnictví oblastí; neznamená, že každé
pole má být přeneseno do jedné velké náhradní služby. Krátkodobé UI
proměnné a closure lokálních eventů se musí při extrakci posoudit zvlášť.

## Navigace má dnes funkční vedlejší účinky

[ShowWorkspace](../../../windows/Rocker.Probe/MainWindow.Shell.cs) kromě
zobrazení provádí následující:

1. Při odchodu z Live ukončí F11 focus mode.
2. Při odchodu z AI stránky ruší běžící request.
3. Při odchodu z aktuálního rytmického trenažéru vyvolá StopRhythmUiAsync.
4. Mimo palm mute vyvolá CancelPalm.
5. Mimo Sluch pozastavuje ear preview.
6. Mimo přesný trénink vypne surgicalListening.
7. Mimo video posílá pause do YouTube bridge.
8. Pro novou úlohu téhož ID odstraňuje předchozí cache pohledu.
9. V přehledu/lekci/riffech/pokroku znovu vytváří souhrnové UI.
10. Sám neinicializuje druhý NativeAudio, ale zobrazení ovladačů může mít
    event side effects; tuto hranici otestovat při převodu na view models.

RefreshSurgical, rytmické/palm funkce a Practice tracking dále kontrolují
navigation.Current.ViewId. To brání jednoduchému vykreslení stejných
Build metod do více oken: aktivní trénink musí získat vlastní identitu.

SaveCurrent míří podle navigovaného pohledu do Studia/Looperu, u not
exportuje MIDI a jinde flushuje autosave. Undo je dnes skutečně podporováno
ve Studiu; plán nesmí slibovat universální undo všem dokumentům bez
implementace. Nové menu bude ukazovat přesný dostupný příkaz pro daný druh.

## Operace a zavírání

| Druh práce | Současná ochrana / uchování | Zachovaný kontrakt |
| --- | --- | --- |
| Aktivní take | recording close guard, CaptureDraft, token, captureMarkers | Zavření celé aplikace nabídne zastavit/uložit; pomocný pohled není vlastník take |
| Looper | close guard při capture/dirty, fingerprint, vrstvy v native paměti | Skrýt nástroj není clear; uložení uchová vrstvy a projektové vazby |
| Studio edit | guard při pending/failed save, studioEditTask, undo | Neúspěšné uložení drží edit v paměti; historie patří projektu |
| Export | exportOperation/exportCancellation | Cancel uklidí dočasný výstup, zachová zdroj; uvolnění vlastníka řešit explicitně |
| Import/mediální příprava | fileOperation/fileCancellation, preparationCancellation, native generation | Otevření nového hostitele nesmí zneplatnit cizí výsledek |
| Záloha/obnova | backupOperationRunning + ochrany dalších operací | Obnova je globální bariéra před změnou databáze/médií |
| Změna zařízení | deviceOperationRunning + busy/power stavy | Zastavit bezpečně, nepřepnout na nevybranou kartu, monitoring zůstane vypnutý |
| Profil/klíče | profileOperation, secretOperationRunning | Vyčkat na atomické uložení; citlivá data nejsou součástí layout/diagnostiky |
| AI | request cancellation a explicitní aiActive | Výsledek má svého vlastníka; přepnutí záložky není automatické zrušení |
| Sestava/update | workspaceOperation/updateInstalling | Nelze zahájit konfliktní shutdown nebo obnovu souběžně |
| Uspání | powerStopTask, powerTransition, jediný nativní subclass | Jedna bezpečná reakce a zastavený stav při návratu |

MainWindow.Shell.Shell_Closing kontroluje operace, close guards, flush
cvičení/AI/autosave, polohu, media sources a audio.DisposeAsync.
Samotný Closed má další cleanup audio/eventů/video/update client.
App.Closed navíc ruší instance listener a uvolní mutex. Extrakce musí
zabránit dvojímu disposal i předčasnému uvolnění při zavření pomocného okna.

## Data a referenční podklady

Hudební data mají zůstat ve stávajícím SQLite schématu 1. Seznam platných
document kinds je převzatý přímo z UserData.Kinds v inventory.json.
Projektové manifesty, média nahrávek a referované soubory jsou samostatné
zdroje; samotný JSON nastavení není úplná záloha.

- [Domain fixtures](../baseline/fixtures/domain-cases.json), nativní catalog a další zamrazené doménové stavy se použijí beze změny.
- [Inventář referenčních médií](../baseline/inventory.json) a windows/fixtures zůstávají umělé, nikoli bratrovy soubory.
- Fixture zahrnují mono/oddělené kanály, tón a silence, nahrávací marker metadata, staré formáty a noty.
- Aktuální Infrastructure Checks ověřují kompletní zálohy, media ownership, projektové vazby, recovery a migraci.
- [WindowPreferences v1](../../../windows/Rocker.Infrastructure/WindowPreferences.cs) má pouze jednu polohu a theme; nepřepisovat jej destruktivně při migraci na nový layout.
- DPAPI secrets, update caches a WebView2 cache nemají být kopírované do multi-window layoutu.
- Studio undo, runtime playback, pending operations a některé herní/tréninkové stavy jsou v paměti; inventura je nevydává za data přežívající pád.

## Zachované a nové chování

| Oblast | Zachovat | Nově implementovat |
| --- | --- | --- |
| Audio | Jeden host, mono kytara na zvoleném kanálu, stereo mix, všechny dosavadní zdroje a native hodiny | Aplikační lifetime a explicitní vlastníci zdrojů mezi dokumenty |
| Hodnocení | Jednotlivé tóny a reference, kalibrovaný palm mute, skutečné baseline výsledky | Jedna aktivní relace bez vazby na právě zobrazenou stránku |
| Metronom | Sample fáze, kyvadlo/doby, předtakt, tiché takty a reduced motion | Nástrojové okno a synchronní zrcadlení v Live |
| Monitoring | Vypnutý při startu a po reconfigure/sleep; výslovný checkbox/gain | Text důvodu u mixu a příkaz zapnout poslech |
| Dokumenty | Původní obsah, data, A/B, markers, projekty, stávající editace | Souběžně otevřené relace, detach/attach, per-document selection/undo tam, kde je podporováno |
| Současné UI | Skutečné nativní ovladače a Vintage komponenty | Desktopová kompozice, knihovna, inspektor a systémové okno nástrojů |
| Ukládání | Typové validace, atomické zápisy, recovery a uložené kroky lekcí | Samostatné rozložení více oken; ne implicitní ukládání nových typů hudebních dat |
| Video | Oficiální embed, původní bezpečnostní omezení, přibližné A/B | Ověřené hostování v odpojeném okně bez duplicitního přehrávače |

## Co D01 ověřuje a co nikoli

Čerstvé lokální výsledky jsou uložené v baseline-validation.json. Linux
native konfigurace má 21 sad; specifická Windows real-device integrační
sada je součástí dřívějšího Windows běhu se 22 sadami. Tyto počty nesměšovat.
Linux nespuštěl WinUI okna ani ovladač M-Track Solo.

Existující Windows důkazy RC6 jsou zafixované s revizí a run ID; nový
Windows build je potřebný při prvním technickém řezu D03. Pro inventuru
beze změny runtime nemá smysl vytvářet stejný instalátor pod novým názvem.

Bratr potvrdil konkrétní průchod kytary po zapnutí monitoringu. Latence,
dlouhý provoz, všechny druhy vstupů/výstupů, fyzická přístupnost a nový
multi-window workflow tím nejsou potvrzené. D01 nepřebírá ruční výsledky
neexistujícího nového rozhraní.
