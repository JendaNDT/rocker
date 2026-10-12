# Rocker pro Windows — úplný implementační plán

Stav k 11. 10. 2026: W01–W03 jsou dokončené — [inventura](docs/windows/baseline/README.md) a [platforma, licence a předání](docs/windows/decisions/0001-platform-and-distribution.md). W03 má [první implementaci](windows/README.md), úspěšné [Windows sestavení a automatické kontroly](docs/windows/w03-windows-validation.json) i spustitelný ZIP; Jenda potvrdil přehrávání, zastavení, opětovné spuštění a zastavení zvuku při zavření okna. Další ruční scénáře a hardwarové testy zůstávají neověřené. Celá nativní aplikace zatím hotová není. Podkladem je současná PWA a sada 32 návrhů rozhraní. Tento dokument je hlavní roadmapou pro nově zvolený směr; [původní roadmapa](ROADMAP.md) zůstává historií webové verze.

**Výsledek:** instalovatelný kytarový pomocník pro Windows se skutečným nativním ovládáním, vzhledem Vintage a samostatným zvukovým jádrem. Zachová všechny již implementované pracovní postupy Rockeru, převede uživatelská data a doplní práci se soubory a projekty. Bratr dostane až funkčně kompletní testovací verzi; na svém počítači s M-Audio M-Track Solo ověří zvuk, stabilitu a pocit ze hry.

**Změna pracovního postupu potvrzená Jendou 11. 10. 2026:** nejprve postupně
implementovat celou aplikaci včetně Vintage rozhraní podle W04–W43; Jenda
bude ručně testovat až uceleného kandidáta. Průběžné automatické kontroly,
Windows sestavení a evidence zůstávají povinné. Nečekat po každém kroku na
další `piš`, poslech ani klikání Jendy. Ruční přejímky prototypů se přesouvají
do závěrečné přejímky po W43, nejsou tím označené jako úspěšné. Implementaci
a automaticky ověřený rozsah evidovat odděleně od ručně neověřeného chování.
Jednotlivé prototypy nejsou výzvou k uživatelskému testování; předání k testu
následuje až po sjednocení funkcí, designu, dat a instalačního balíku.

## Orientace

- [Dohodnutý rozsah](#scope)
- [Architektura a zvuk](#architecture)
- [Design a všech 32 pohledů](#design)
- [Postup W01–W48](#implementation)
- [Milníky a podmínky dokončení](#milestones)
- [Převod dat](#migration)
- [Ověřování a závěrečný test se zvukovkou](#validation)
- [Rizika, zdroje a návaznost další práce](#handoff)

Návrhy: [všech osm listů](docs/windows/design/vsechny-navrhy.md), [interaktivní galerie](docs/windows/design/index.html), [specifikace ovládání](docs/windows/design/README.md). Galerie je prohlížitelný návrhový podklad v HTML, nikoli technologie budoucí aplikace. Obrázky obsahují ukázkové hodnoty a nenahrazují přejímací podmínky v tomto plánu.

<a id="scope"></a>
## 1. Dohodnutý rozsah a výchozí stav

### Co má první úplná Windows verze obsahovat

1. Nativní okno, nabídky, formuláře, seznamy, klávesnici, fokus, pravé tlačítko, přetahování souborů, systémové dialogy a běžné chování Windows.
2. Vintage jako jednotný vizuální styl: tmavé teplé plochy, tlumená mosaz, krémové hudební přístroje. Běžné ovladače zůstávají přehledné a přístupné.
3. Všechny současné funkce: Live, ladička, metronom, lekce, přehrávání vlastních skladeb, YouTube, Přesný trénink, metalové trenažéry, aparát, nahrávky, Studio, Looper, knihovna, noty, hry, AI tutor, profil, pokrok, zálohy a úvodní průvodce.
4. Nativní audio pro živou kytaru, efekty, společné časování, nahrávání a přehrávání. Volba jednoho vstupního kanálu a nezávislá volba toho, co se poslouchá a nahrává.
5. Trvalé soubory nahrávek a projektů, zálohy včetně volitelných nahrávek, bezpečné uložení AI klíče, obnova po chybě a migrace z PWA.
6. Automatické i ruční ověření bez kytary, sestavení instalátoru a nakonec test celé aplikace na bratrově skutečném zařízení.

### Co je potvrzené a co je návrh řešení

| Oblast | Stav a rozhodnutí |
| --- | --- |
| Platforma a vzhled | Uživatel chce skutečnou Windows aplikaci a vybral Vintage. Samotný obal PWA nesplňuje zadání. |
| Cílová zvukovka | Potvrzen M-Audio M-Track Solo. Kytara je v jednom ze dvou vstupů; číslo kanálu zatím není potvrzené. |
| Uživatelské rozhraní | W02: C# / WinUI 3, .NET 10 LTS, Windows App SDK 2.5.1; výchozí cíl Windows 11 x64 od 24H2. Bratrova verze OS není potvrzena, skutečnou integraci ověří W03. |
| Zvuk | W02: C++20 a JUCE 9.0.3 pod AGPLv3, přibalené ASIO hlavičky pod GPLv3. Ověření integrace následuje ve W03–W04. |
| Ovladač | Preferovat podporovaný ASIO ovladač výrobce, pokud na konkrétním počítači funguje stabilně; nabídnout WASAPI. Přesná verze ovladače ani dostupné buffery nebyly na zařízení ověřeny. |
| Testování s kytarou | Uživatel ji nyní nemá. Vývoj nesmí vyžadovat bratrův průběžný test každého kroku. Jeho test následuje po dokončení celého kandidáta. |
| Licence a distribuce | Osobní předání bratrovi: hotový instalátor, odpovídající zdroje dostupné samostatným odkazem podle AGPL/GPL §6(d), bez nutnosti je stahovat. Ruční aktualizace; veřejné vydání ani placená licence nejsou pro zvolenou cestu potřeba. Původní MIT licence se nemění. |
| Přesná odezva | Je neznámá, dokud nebude změřena celá skutečná cesta. Čísla v návrzích nejsou měření. |

Není potřeba backend ani uživatelský účet pro základní funkce. Lekce, nástroje, projekty a lokální soubory fungují offline; YouTube a AI mají jasně oddělenou závislost na internetu.

### Co přebíráme ze současné aplikace

Inventura vychází z [obrazovek](src/lib/screens.ts), [komponent](src/components), [audio modulů](src/audio), [doménové logiky](src/lib), [README](README.md) a existujících testů. Aplikace má 13 hlavních pracovních sekcí; adresy instrument, studio, looper a youtube jsou přesměrování. Návrhy je rozpracovávají do 32 pohledů, nikoli do 32 nezávislých aplikací.

- Přenést obsah a jeho stabilní identifikátory: 18 lekcí, 26 akordů, 6 stupnic, 14 riffů, 11 achievementů, ladění a tovární presety. Počty představují současný inventář, nikoli zákaz dalšího obsahu.
- Převést ověřenou čistou logiku do testovatelných doménových modulů. React, CSS, Zustand a Web Audio API se nepovažují za nativní implementaci; slouží jako reference chování.
- Přesný trénink musí zachovat průchod tóny, výběr chybového místa a drill. Obrázek s vlnovým průběhem nepokrývá celou tuto existující funkci.
- AI zpětná vazba dnes vychází ze statistik a kontextu, nikoli z poslechu kytary. Rozhraní nesmí tvrdit opak.
- Denní plán má v nativní verzi otevřít přímo konkrétní úlohu. Aktivní cvičení v Live se má správně započítávat do pokroku.
- Současný export nastavení neobsahuje nahrávky z IndexedDB. Převod zvuku je samostatná část migrace.

Desktopová doplnění z návrhů, která patří do tohoto plánu: trvalé projekty Studia, panorama a solo stop, návrat poslední vrstvy Looperu, správa souborů, přímé odkazy na cvičení a úplnější záloha. Jsou označena jako nová práce, nikoli jako již existující schopnosti PWA.

### Oddělený backlog

Původní nedokončený Bod 22 (songbook a sdílení riffů), separace nástrojů, AR, hosting VST, synchronizace přes cloud, mobilní port a nové inteligentní hodnocení rytmu či akordů nejsou podmínkou shody s dnešní aplikací. Evidovat je po W48 jako samostatné rozšíření. Základní export a import souborů do první Windows verze patří. Mobilní přepínače rozložení, PWA instalace, service worker a mobilní vibrace se nahrazují odpovídajícím desktopovým chováním. Obnova starého tématu může nabídnout Vintage; Studio a Glassy nejsou součástí nově vybraného designu.

<a id="architecture"></a>
## 2. Navržená architektura

| Vrstva | Odpovědnost | Co do ní nepatří |
| --- | --- | --- |
| WinUI 3 / C# | Nativní okno, navigace, formuláře, pracovní pohledy, přístupnost, vizualizace | Zpracování jednotlivých audio bloků |
| Doména a aplikační služby C# | Lekce, plány, pokrok, projekty, soubory, AI, orchestrace | Závislost výukové logiky na konkrétní obrazovce nebo ovladači |
| Verzované rozhraní C ABI | Příkazy jádru, konfigurace, kopie stavových snímků, bezpečné vlastnictví prostředků | Předávání spravovaných objektů nebo výjimek přes hranici DLL |
| Nativní jádro C++ / JUCE | Zařízení, sample clock, routing, DSP, mix, metronom, nahrávací fronty | WinUI, síť, přístup k databázi, blokující operace v audio callbacku |
| Pracovní vlákna | Detekce tónu, dekódování, časové protažení, waveform, čtení a zápis souborů | Blokování živého průchodu zvuku |
| Lokální úložiště | SQLite metadata a verze schématu, zvukové soubory, projekty, zálohy | Tajné klíče v běžném JSON nebo databázovém exportu |
| Windows služby | Systémové dialogy, bezpečné úložiště klíče, instalace, asociace souborů | Nutnost účtu nebo serveru pro běžné cvičení |

Původní orientační návrh struktury: desktop/Rocker.sln, desktop/src/Rocker.App, desktop/src/Rocker.Domain, desktop/src/Rocker.Infrastructure, desktop/src/Rocker.Audio.Interop, desktop/native/RockerAudio, desktop/tests, desktop/packaging, shared/content a shared/fixtures. Návrhové a rozhodovací dokumenty patří do docs/windows. Implementace používá kořen `windows/` a řešení `Rocker.slnx` podle W05, aby zachovala zavedené buildy W03/W04. PWA zůstává po dobu přepisu referencí; sdílený obsah má jeden určený zdroj pravdy.

### Živá zvuková cesta

Vybraný vstupní kanál M-Track Solo → vstupní úroveň a volitelná kalibrovaná brána → odbočka suchého záznamu a analýzy → aparát a efekty → odbočka záznamu po efektech → společný mix s metronomem, skladbou, stopami a looperem → hlavní výstup → sluchátka ve stejném interfejsu.

- Vstupní kanál zvolí uživatel. Druhý vstup se nepřimíchává automaticky. Mono kytara se správně směruje do obou sluchátek; skutečně stereofonní podklady zůstávají stereo.
- Nahrávání výslovně rozlišuje suchou kytaru, kytaru po efektech a výsledný mix. Metronom a podklad se do kytarové stopy nepřidají skrytě.
- Jeden časový základ řídí metronom, nahrávání, looper i Studio. Grafika čte stav; její časovač neurčuje zvukové události.
- Analýza ladění a palm mute běží nad kopiemi vstupu mimo kritickou zvukovou cestu. Zpoždění potřebné k rozpoznání nízkého tónu nesmí zdržovat monitoring.
- Audio callback nesmí alokovat, čekat na zámek, volat C#, zapisovat na disk, logovat ani komunikovat se sítí. Použít předem připravené buffery a omezené fronty s určeným chováním při přeplnění.
- UI předává změny parametrů příkazy; hodnoty se bezpečně vyhlazují. Měřiče se čtou omezenou frekvencí, nikoli callbackem do spravovaného kódu pro každý audio blok.
- Životní cyklus jádra patří aplikaci. Navigace mezi pohledy zvuk nevypíná; ukončení, změna zařízení, uspání a chyba mají explicitní stavový automat.

### Strategie odezvy

Výchozí experiment je 48 kHz a 128 vzorků pouze tehdy, pokud je zařízení skutečně nabízí. Podle výsledku vyzkoušet 64 nebo 256 vzorků. Při 48 kHz odpovídá 128 vzorků přibližně 2,67 ms na jeden blok; není to prodleva od kytary do sluchátek. Ta zahrnuje vstup, ovladač, bezpečnostní buffery, zpracování, výstup a převodníky.

V rozhraní oddělit: dostupné nastavení zařízení, odhad hlášený ovladačem, známé zpoždění DSP, stav výpadků a případně skutečně změřenou celkovou odezvu. Záznam měření obsahuje datum, zařízení, ovladač, vzorkování, buffer a použitý řetězec. Nepředstírat změřenou hodnotu, pokud máme jen odhad.

ASIO a přehrávání YouTube nemusí na každém ovladači fungovat současně. W04 a W30 ověří souběh a nabídnou srozumitelný profil pro živou kytaru a profil se sdíleným zvukem Windows. Přepnutí může vyžadovat zastavení transportu; nesmí přerušit nahrávání bez rozhodnutí uživatele. Současný vstup a výstup přes různá fyzická zařízení není výchozí nízkolatenční cesta a vyžaduje řešení rozdílných hodin.

### Dvě integrační hranice

**YouTube:** oficiální vložený přehrávač ve vyhrazeném WebView2, s nativními okolními ovladači. Celá aplikace na něm nestojí. Nezavádět stahování nebo extrakci zvuku videa. YouTube nelze slibovat jako vzorkově přesnou součást audio grafu.

**Noty:** preferovat nativní integraci AlphaTab přes jeho .NET API a renderer, například SkiaSharp. Oficiální projekt uvádí .NET API; hotovou integraci s WinUI a společným zvukovým jádrem to samo o sobě nepotvrzuje. W04 ji musí prokázat. Pokud nestačí, porovnat jiný nativní renderer; použití webového prohlížeče pro noty by bylo změnou dohodnutého rozsahu a vyžaduje výslovné rozhodnutí. Noty se nesmějí beze slova vynechat.

<a id="design"></a>
## 3. Design a matice všech obrazovek

Nejprve vznikne společný designový systém; poté budou pohledy průběžně napojovány na funkce. Obrázky jsou směr pro kompozici a materiály. Přesné české texty, chování, kontrast, menu a dialogy určuje specifikace a skutečné ovladače Windows.

Společné požadavky: systémová titulková lišta nebo korektně integrovaná vlastní oblast s nativním chováním okna; nabídky Soubor, Úpravy, Zobrazení, Nástroje, Nápověda; čitelné hierarchie; pevně dostupný stav zvuku; klávesové zkratky jen ve správném kontextu; viditelný fokus; tooltipy a přístupné názvy. Respektovat systémové měřítko, zvětšení textu, vysoký kontrast, omezení animací a Narrator. Žádné klíčové rozlišení pouze barvou. Live musí jít používat z odstupu s kytarou v ruce.

Každý pohled musí mít navržený běžný stav, prázdný obsah, načítání, chybu a stav bez zvukového zařízení, pokud se ho týká. Ukázkový signál je vždy viditelně označený. Stejně tak offline režim, chybějící AI klíč, nepodporovaný soubor a probíhající ukládání. Obrázkové měřiče se nesmějí použít jako falešná funkční data.

| Pohled | Co musí být funkční | Hlavní kroky |
| --- | --- | --- |
| 01 Přehled | Denní plán, pokračování přesně v úloze, historie, přechod do Live | W32, W33 |
| 02 Live | Velká ladička, BPM, nahrávání, stálý zvuk, skutečný stav vstupu | W17–W22, W33 |
| 03 Ladička | Vedená a chromatická, ladění, vlastní ladění, reference, kalibrace | W19 |
| 04 Metronom | Akcenty, subdivize, předtakt, swing, rampy, ticho, bubny, polyrytmus, presety | W18 |
| 05 Výběr lekce | Obsah, postup, filtry a přímý vstup z plánu | W06, W25 |
| 06 Průběh lekce | Kroky, poslech, tónová kontrola, dokončení a obnovení | W25 |
| 07 Vlastní skladba | Import, waveform, zpomalení bez změny výšky, A/B, předtakt a rampa | W21 |
| 08 YouTube cvičení | Oficiální video, A/B, dostupné rychlosti, uložené a pojmenované úseky | W04, W30 |
| 09 Přesný trénink | Původní tónový drill; samostatně cvičení úseku skladby podle návrhu | W21, W26 |
| 10 Power chordy | Výběr, polohy, přehrávání, trénink, tempo a rekordy | W27 |
| 11 Gallop | Vzory, akcenty, předtakt a řízené zrychlování | W27 |
| 12 Palm mute | Kalibrace, živá analýza, přiměřená interpretace výsledků | W19, W27 |
| 13 Aparát | Celý řetězec, presety, bypass, měřiče a volba nahrávání | W17 |
| 14 Nahrávky | Záznam, seznam, přejmenování, časové značky, bezpečné mazání, přehrávání, A/B | W22 |
| 15 Studio | Trvalé projekty, stopy, gain, mute, panorama, solo, overdub a mixdown | W24 |
| 16 Looper | První smyčka, vrstvy, jejich návrat, správné délky a export | W23 |
| 17 Akordy | Diagram, prstoklad, kategorie, poslech, levá ruka | W28 |
| 18 Stupnice | Tonika, ladění, poloha, značení a propojení s hmatníkem | W28 |
| 19 Riffy | Obsah, tabulatura, poslech a přímý start tréninku | W28 |
| 20 Noty a tabulatury | Otevření GP/MusicXML, stopy, zoom, přehrávání a výběr taktů | W04, W29 |
| 21 Hmatník | Herní logika, klávesnice, nápověda, skóre | W28 |
| 22 Sluch | Intervaly, opakování zvuku, odpovědi a vysvětlení | W28 |
| 23 AI — rozhovor | Kontext, bezpečný klíč, stream odpovědi, zrušení a historie | W10, W31 |
| 24 AI — plán | Validace návrhu, prohlédnutí a úprava, uložení, odkazy do úloh | W31, W33 |
| 25 AI — zpětná vazba | Rady ze statistik a poznámek s přiznaným zdrojem | W31 |
| 26 Moje kytara | Profil a doporučení, ladění, levá ruka, související nástroje | W32 |
| 27 Pokrok | Aktivní čas, streaky, achievementy, rekordy, přestávky | W32 |
| 28 Nastavení | Zvuk, vzhled, klávesnice, AI, soubory, dostupnost a reset | W34 |
| 29 Zvuková zařízení | Backend, zařízení, kanál, buffer, stav a diagnostika | W14–W16, W20 |
| 30 Zálohy a soubory | Nastavení i zvuk, bezpečná obnova, migrace a umístění dat | W09–W12, W37 |
| 31 První spuštění | Dokončení i bez kytary, zařízení, profil, import, zkušební signál | W34 |
| 32 Export nahrávky | Rozsah a formát, systémové uložení, průběh, zrušení a přepsání | W22, W24, W35 |

Všechny pohledy procházejí závěrečnou vizuální a funkční přejímkou W36 a dostupností W40. Studio, Looper, YouTube a profil kytary se nemají implementovat podruhé kvůli historickým adresám PWA.

<a id="implementation"></a>
## 4. Postup implementace

Kroky jsou seřazené tak, aby se daly provádět postupně. Závislosti uvádějí minimální předpoklady; práce uvnitř kroku se rozděluje na malé ověřitelné změny. Každý krok končí konkrétním výsledkem a kontrolou. Pouhá přítomnost obrazovky nestačí k označení kroku za hotový.

### Fáze A — Inventura a technická rozhodnutí

<a id="w01"></a>
#### W01 — Zafixovat inventář a výchozí chování

- [x] Hotovo 10. 10. 2026. Závislosti: žádné. [Výsledek a omezení](docs/windows/baseline/README.md), [inventura](docs/windows/baseline/inventory.md), [protokol kontrol](docs/windows/baseline/validation.json).
- Zapsat výchozí revizi PWA, inventář všech funkcí a jejich dat, obrazovky a známé nedostatky. Navázat 32 pohledů na současné moduly a přejímací scénáře.
- Připravit referenční data a zvukové ukázky s jasným původem, anonymizované zálohy a soubory pro import. Obsah knihovny prověřit i z hlediska práv k distribuci.
- Zaznamenat výsledky existujících kontrol. Při přípravě prostředí prošlo 561 testů v 54 souborech, TypeScript a lint; tento výsledek neprokazuje funkčnost budoucí Windows aplikace.
- Hotovo, když má každá existující funkce zdroj, cílový pohled a scénář ověření. Neznámé vlastnosti a ručně neověřené audio jsou výslovně oddělené od výsledků testů.
- Důkaz: zafixovaná revize PWA a 259 hashů, všech 32 pohledů, 11 skutečně rehydratovaných stores, 69 referenčních případů, 3 průchody drillem, 12 mediálních podkladů a soupis licenčních otázek. Čerstvě prošlo 561/561 testů v 54 souborech, TypeScript, lint i samostatná kontrola baseline. Windows a M-Track Solo zůstávají neověřené podle dalšího plánu.

<a id="w02"></a>
#### W02 — Uzavřít platformu, licence a distribuci

- [x] Hotovo 10. 10. 2026. Závislosti: W01. [Rozhodnutí a omezení](docs/windows/decisions/0001-platform-and-distribution.md), [verze a licenční podklady](docs/windows/decisions/platform-selection.json).
- Zvolit a zaznamenat podporované Windows, CPU architekturu, stabilní .NET, Windows App SDK, C++ toolchain a vydanou verzi JUCE. Uzamknout verze a kontrolovatelné zdroje závislostí; neodvozovat podporu z náhodné větve upstreamu.
- Rozhodnout mezi vyhovující cestou AGPLv3 a komerční licencí JUCE. Posoudit ASIO SDK, AlphaTab, renderer, DSP, kodeky, fonty a soundfonty. Evidovat podmínky a licence distribuovaných binárek i obsahu.
- Výchozí cíl je Windows x64; minimální OS přizpůsobit skutečnému bratrovu systému a podpoře závislostí. Ověření systému může počkat do plánování sestavení, neblokuje sepsání roadmapy.
- Hotovo, když existuje rozhodovací dokument s podporovanou platformou a legální cestou distribuce. Pokud JUCE nevyhoví, vybrat a porovnat náhradní audio vrstvu před navazující implementací; tiše nepřelicencovat MIT repozitář.
- Výsledek: uživatel potvrdil samostatný odkaz na zdroje; do instalátoru se nepřibalují. Vybrány AGPL JUCE a GPL ASIO, native WinUI/C# + C++ stack a soukromé předání s ručními aktualizacemi. Ověřené verze jsou výběr, nikoli důkaz sestavení; W03/W04 prověří integrace, W05 uzamkne celý graf. Bratrův OS se potvrdí před cíleným balíkem a finální licence skutečně distribuovaných součástí se ověří před W43.

<a id="w03"></a>
#### W03 — Ověřit WinUI a hranici mezi C# a C++

- [x] Dokončeno v rozsahu technického řezu se zaznamenanými omezeními. Závislosti: W02. [Implementace a Windows přejímka](windows/README.md), [protokol Windows kontrol](docs/windows/w03-windows-validation.json), [historické Linux kontroly](docs/windows/w03-validation.json).
- V malém technickém řezu sestavit WinUI okno a nativní audio DLL, předat příkaz, přečíst stav a korektně uvolnit prostředky. Použít umělý tón a virtuální nebo dostupné zařízení, kytara není nutná.
- Navrhnout verzovaný C ABI kontrakt: neprůhledné handle, velikosti struktur, kódy chyb, kopírování řetězců, vlastnictví bufferů a přesně určená vlákna. Ve spravované vrstvě bezpečná životnost handle; žádné výjimky přes ABI.
- Prověřit opakovaný start/stop, nesoulad verze DLL a čisté ukončení bez visícího vlákna. UI dostává pouze stavové snímky mimo audio callback.
- Hotovo, když Windows sestavení spolehlivě přehraje a zastaví tón a integraci lze automaticky ověřit. Je to interní důkaz proveditelnosti, nikoli verze vyžadující bratrův test.
- Stav: Windows runner sestavil MSVC/JUCE DLL i WinUI, provedl C++ kontrakt, skutečné C# volání do testovací DLL a odmítnutí nekompatibilní DLL. Publikované okno se automaticky otevřelo a po běžném zavření skončilo kódem 0. Pád při načítání XAML po publikování opravil `EnableMsixTooling`. Je dostupné [předběžné vydání](https://github.com/JendaNDT/rocker/releases/tag/windows-w03-preview-20261011) se spustitelným ZIPem a samostatnými zdroji. Jenda 11. 10. 2026 potvrdil poslech tónu, zastavení, opětovné spuštění a ukončení zvuku zavřením okna; základní zvukový průchod je potvrzený. Jenda následně potvrdil Tab/Enter, 20 cyklů start/stop, stereo a nové spuštění po zavření během tónu. Odpojení zařízení výslovně odmítl testovat; W03 uzavíráme podle základního kritéria technického řezu, nikoli jako kompletní produktovou či hardwarovou přejímku. Nepodepsanou DLL původně blokoval zapnutý Smart App Control (0x800711C7); přehrávání uživatel ověřil po jeho vypnutí. Podepsané vydání se zapnutou ochranou, přístupové klávesy, přístupnost, DPI a odpojení zařízení zatím ověřené nejsou; [ruční protokol](docs/windows/w03-manual-acceptance.md) uchovává zbývající scénáře. Historický Linux build není distribučním balíkem.

<a id="w04"></a>
#### W04 — Ověřit noty, média a souběh zvuku

- [ ] Rozpracováno. Závislosti: W02, W03. První technický řez: [nativní noty](docs/windows/w04/README.md); Windows ověření a ruční přejímka mají vlastní protokol. Navázal [offline mediální modul](docs/windows/w04/media.md) a [původní nahrávky](docs/windows/w04/legacy-media.md): WAV/MP3/Ogg/Opus/WebM/Opus, Windows MP4/AAC a zpomalení. Windows prošel 167/167 mediálními kontrolami, Linux 138/138 s ASan/UBSan. Navazuje [společné přehrávání a základní syntéza not](docs/windows/w04/common-playback.md); Windows build 38147210735 prošel včetně přípravy MIDI v produkční DLL a nativního ovládání. Navazuje [oficiální YouTube/WebView2](docs/windows/w04/video.md); Windows lokální bridge prošel v běhu 38148531570; online a hardwarová přejímka zůstává odložená po W43.
- Prokázat načtení reprezentativního Guitar Pro a MusicXML do .NET AlphaTab, vykreslení v nativním okně, hit testing, výběr taktů a dostupnost dat pro společné přehrávání. Samostatně rozhodnout, jak renderer a syntéza zapadnou do jádra.
- Zvolit dekódování MP3/WAV a starých nahrávek WebM/Opus, Ogg/Opus a podporovaných MP4. Ověřit algoritmus zpomalení bez změny výšky, kvalitu, paměť a licenci; JUCE samotné automaticky neřeší všechny tyto požadavky.
- Ověřit oficiální YouTube embed ve WebView2, povolenou komunikaci a souběh jeho výstupu s audio hostem. Na dostupném Windows zařízení ověřit WASAPI; specifický souběh M-Track Solo zůstává v matici W44.
- Hotovo, když každá oblast má běžící důkaz a zvolenou závislost, nebo konkrétní blokující rozhodnutí s variantami. Neodsunout nezvládnutý renderer, dekodér nebo licenci až za hotové obrazovky.

### Fáze B — Základ aplikace a společný design

<a id="w05"></a>
#### W05 — Založit řešení, sestavení a Windows CI

- [x] Dokončeno: [řešení, zámky a vrstvy](docs/windows/w05/README.md); čisté uzamčené Windows sestavení, testy, běžící okno, balení a samostatné symboly prošly v běhu 38150968516. Závislosti: W02–W04.
- Na výslovný požadavek uživatele z 11. 10. 2026 je jednoduchý Windows runner a balení prototypu předřazeno do W03, aby dostával hotové EXE. Neznamená to dokončení zbytku W05; výsledek konkrétního sestavení se musí doložit před předáním.
- Založit vrstvy podle architektury, CMake pro C++, správu balíčků a jejich zámků pro .NET, jednotné verze produktu a sestavení. Nativní testy spouštět přes CTest, doménu například přes xUnit.
- Připravit Windows runner pro sestavení WinUI a DLL, unit testy a později UI testy a balení. CI uchová protokoly, symboly a artefakty. Linux cloud může kontrolovat obsah a část čisté logiky, nenahrazuje Windows ověření.
- Zachovat oddělené kontroly PWA, dokud slouží jako reference a zdroj migračního exportu. Build artefakty a uživatelská data nepřidávat do Gitu.
- Hotovo, když čistý checkout na popsaném Windows prostředí vytvoří spustitelnou aplikaci a skutečně provede testy bez ručních oprav cest.

<a id="w06"></a>
#### W06 — Přenést obsah a čistou doménovou logiku

- [x] Dokončeno v rozsahu společné čisté domény: verzovaný export všech 15 skupin a všech 69 referenčních případů W01 je přenesený. [Doménový přenos](docs/windows/w06/README.md) dále porovnává 372 případů a 350 rytmických/palm-mute/přestávkových stavů. Windows běh 38151654548 potvrdil všech 69 + 372 případů a 37 + 350 stavů. Navazující Windows běh 38152583162 potvrdil i distribuční náhrady dvou riffů a parametry shellu; specifické doménové moduly dalších funkcí navazují v jejich krocích. Závislosti: W01, W05.
- Zavést verzovaný společný formát obsahu, stabilní ID a validační schéma. Exportovat lekce, akordy, stupnice, riffy, cvičení, ladění a presety z existujících dat bez ručního přepisování stovek hodnot.
- Portovat hudební výpočty, lekční automaty, tempo, pravidla pokroku a validaci do čistých C# modulů. Algoritmy DSP zůstávají v C++; nesmí vzniknout druhá nesynchronizovaná kopie pravidel.
- Přenést relevantní scénáře existujících testů a porovnávat shodné vstupy a výstupy přes referenční JSON. Počet testů není cílem; zachování chování ano.
- Hotovo, když obsah projde kontrolou ID a vazeb a doménové výsledky odpovídají referenci nebo zdokumentované opravě.

<a id="w07"></a>
#### W07 — Postavit nativní shell a navigaci

- [x] Dokončeno v rozsahu shellu: [nativní shell](docs/windows/w07/README.md) otevírá 32 pohledů, drží jediný host a kontext úloh, ukládá okno a vzhled. Windows běh 38152583162 prošel; stránky s nedokončenými funkcemi jsou jasně označené. Závislosti: W05.
- Vytvořit hlavní okno, systémové chování titulku a změny velikosti, menu, navigaci podle pracovních skupin, stavový řádek a prostor pro transport. Obnovovat velikost a polohu bezpečně i po odpojení monitoru.
- Zavést příkazy, kontextové nabídky, navigaci s parametry úlohy a návrat do předchozího místa. Zabránit násobným instancím audio hostu při navigaci nebo opětovném otevření souboru.
- Definovat chování zavření okna při nahrávání a neuložené práci; žádné skryté ukončení záznamu. Zkratky nepřebírají psaní v textových polích.
- Hotovo, když lze všechny cílové pohledy otevřít v nativním shellu, okno se chová standardně a navigace neztrácí rozpracovaný stav. Prázdné pohledy zatím nejsou hotové funkce.

<a id="w08"></a>
#### W08 — Implementovat Vintage designový systém

- [ ] Rozpracováno: [společné Vintage komponenty](docs/windows/w08/README.md), nativní ovladače, diagramy, měřák a kompaktní transport. [Windows build a snímky prošly](docs/windows/w08/windows-validation.json); Live a kompletní produktové pohledy se připojí v příslušných funkčních krocích. Závislosti: W07.
- Převést návrhy do tokenů barev, typografie, rozestupů, hran a stavů. Preferovat Segoe/system fonty pro ovládání; dekorativní písmo a materiály jen tam, kde pomáhají hudebnímu přístroji.
- Připravit společné seznamy, formuláře, panely, transport, level metr, knoby se zadáním čísla, diagramy a chybové stavy. Každý vlastní widget musí mít klávesnici a UI Automation reprezentaci.
- Rozpracovat stavové specifikace všech 32 pohledů, včetně malého okna, vysokého DPI, vysokého kontrastu, offline režimu a prázdných dat. Systémový dialog nereplikovat obrázkem nebo vlastním webovým formulářem.
- Hotovo, když existuje nativní katalog komponent a ověřené rozložení reprezentativního přehledu, Live, tabulky a nastavení. Detaily galerií se řídí touto implementovatelnou specifikací.

### Fáze C — Data, soubory a migrace

<a id="w09"></a>
#### W09 — Zavést lokální úložiště a projekty

- [x] Hotovo v rozsahu datové vrstvy: [SQLite, projekty a autosave](docs/windows/w09/README.md), [Windows důkaz](docs/windows/w09/windows-validation.json). Editor a další funkce se připojí ve vlastních krocích. Závislosti: W05, W06.
- SQLite pro strukturovaná data a index, soubory pro zvuk, manifest projektu se stabilními ID stop a relativními odkazy. Oddělit tovární obsah, uživatelská data, cache a dočasné nahrávky.
- Definovat schéma nastavení, profilu, pokroku, lekcí, presetů, smyček, nahrávek, projektů a AI historie. Každá změna schématu má migraci, validaci a zálohu před převodem.
- Zavést transakce, atomické nahrazení manifestu, autosave, obnovu po pádu a kontrolu chybějících médií. Nastavení umístění souborů nesmí vytvářet neplatné odkazy ani tiše přepisovat jiné projekty.
- Hotovo, když restart i vynucené přerušení zápisu zachovají poslední konzistentní stav a uživatel může najít a přenést svůj projekt.

<a id="w10"></a>
#### W10 — Bezpečně uložit AI klíč a citlivá nastavení

- [x] Hotovo v rozsahu systémového úložiště a nastavení: [DPAPI a životní cyklus klíče](docs/windows/w10/README.md), [Windows důkaz](docs/windows/w10/windows-validation.json). Převod starých klíčů navazuje ve W12. Závislosti: W09.
- Zvolit Windows Credential Locker nebo úložiště chráněné DPAPI podle modelu balení. Oddělit tajnou hodnotu od běžných nastavení a záloh, umožnit její změnu a odstranění.
- Nikdy neukládat klíč do logu, projektu, historie promptů nebo exportu. Síťová vrstva předává jen potřebný kontext; soukromé poznámky a audio neposílat automaticky.
- Vyřešit import starého nastavení, které klíč může obsahovat: nabídnout jeho bezpečné převzetí nebo nové zadání, nepřekopírovat jej do SQLite jako běžný text.
- Hotovo, když kontrola souborů, záloh a diagnostiky neodhalí klíč a aplikace má srozumitelný stav po jeho odstranění či ztrátě přístupu k systémovému úložišti.

<a id="w11"></a>
#### W11 — Definovat a implementovat desktopové zálohy

- [x] Hotovo v rozsahu záloh a obnovy: [verzovaný archiv, náhled, vratná kopie a recovery](docs/windows/w11/README.md), [Windows důkaz](docs/windows/w11/windows-validation.json). Ruční pickery navazují ve W37/W42. Závislosti: W09, W10.
- Navrhnout verzovaný manifest archivu, metadata souborů, kontrolní součty a volbu přibalit nahrávky a projekty. Tajné klíče v exportu standardně nejsou; pouhá záloha databáze nesmí být vydávána za úplnou zálohu zvuku.
- Obnova předem ukáže obsah, neznámou verzi, konflikty a strategii sloučení či nahrazení. Před přepsáním vytvoří vratnou zálohu; nedokončená obnova nesmí poškodit aktuální data.
- Ošetřit neplatné cesty v archivu, velikost a počet souborů, volné místo, chybné kontrolní součty a přerušenou operaci. Externí soubory nekopírovat mimo zvolený prostor.
- Hotovo, když cyklus export → čistý profil → import obnoví všechna zahrnutá data a chybné či novější nekompatibilní archivy skončí bezpečnou chybou.

<a id="w12"></a>
#### W12 — Připravit převod z PWA včetně nahrávek

- [ ] Rozpracováno: [převod všech 11 skupin a původních nahrávek](docs/windows/w12/README.md), skutečný referenční archiv PWA, náhled a deduplikace. [Windows integrace prošla](docs/windows/w12/windows-validation.json); produkční webový export zatím není nasazený. Závislosti: W04, W09–W11.
- Implementovat import starých záloh nastavení podle 11 klíčů v kapitole o migraci. Zachovat ID, časové údaje a vlastní obsah; známé rozdíly mezi webovým a nativním nastavením přeložit, ostatní vysvětlit v reportu.
- Připravit malý migrační export v PWA pro IndexedDB nahrávky s původními soubory, MIME typem, názvem, délkou a časem vytvoření. Změna exportu a případné nasazení jsou samostatný implementační úkol; tento dokument je neprovádí.
- Native aplikace nemůže přímo převzít localStorage jiného prohlížeče. Nabídnout jasný postup exportu ze stávajícího profilu a importu souboru; původní data neodstraňovat.
- Hotovo, když existuje ověřený převod referenčního archivu včetně starých audio formátů, deduplikace a reportu vynechaných záznamů. Finální uživatelský průchod doplní W37.

### Fáze D — Nativní audio základ

<a id="w13"></a>
#### W13 — Implementovat realtime host a řízení životního cyklu

- [ ] Rozpracováno: [řízení hostu a realtime kontrakty](docs/windows/w13/README.md), předalokované SPSC fronty, souběžné kontroly a audit alokací společného rendereru. [Windows W13/W14 prošel](docs/windows/w13/windows-validation.json); analýza a disk se připojí ve funkčních krocích. Závislosti: W03, W05.
- Vytvořit host s jasnými stavy zastaveno, spouštění, běží, změna konfigurace, chyba a ukončování. Jednoznačné vlastnictví zařízení a všech vláken; ochrana proti závodu start/stop a starým výsledkům požadavků.
- Předalokované audio buffery, omezené fronty příkazů, oddělené pracovní fronty pro analýzu a disk. Při přeplnění analýzy lze zahodit starý snímek; při ztrátě nahrávaných dat se chyba musí přiznat.
- Přidat headless režim, testovací generátor a offline zpracování stejným DSP grafem. Testovací signál je volitelný a viditelně označený.
- Hotovo, když opakované starty, chyby a ukončení nezanechají prostředky, realtime cesta nevolá UI a diagnostika odhalí zakázanou alokaci či blokující operaci.

<a id="w14"></a>
#### W14 — Zařízení, ASIO a WASAPI

- [ ] Rozpracováno: [enumerace, výslovná volba, formát, panel a zachování konfigurace](docs/windows/w14/README.md). [Automatické Windows ověření prošlo](docs/windows/w14/windows-validation.json), hardware se finálně testuje až ve W44. Závislosti: W02, W07, W13.
- Enumerovat ovladače, zařízení, názvy kanálů, podporované vzorkování a buffery. Nabízet skutečné hodnoty, ukládat volbu podle stabilní identity a při zmizení zařízení vyžádat srozumitelný náhradní postup.
- Zpřístupnit podporovaný ASIO control panel a WASAPI režimy. Neinstalovat ani nepřibalovat cizí ovladač bez práv; odkazovat na ověřený zdroj výrobce.
- Ošetřit obsazené zařízení, Windows omezení vstupu, nesoulad formátu, hotplug, změnu výchozího zařízení a spánek/probuzení. Nepřepnout živou kytaru potichu na reproduktory notebooku.
- Hotovo, když změna nastavení skončí potvrzeným stavem nebo návratem k poslední funkční konfiguraci. Testovat dostupné backends na Windows; M-Track Solo se finálně ověří ve W44.

<a id="w15"></a>
#### W15 — Routing jednoho vstupu, mix a monitoring

- [ ] Rozpracováno: [skutečný mono vstup, stereo monitoring a měřáky](docs/windows/w15/README.md), [Windows W15 prošel](docs/windows/w15/windows-validation.json), Linux routing a realtime kontrakty. Aparát a nahrávací writer navazují ve W17/W22; Windows ověření ještě probíhá. Závislosti: W14.
- Zavést volbu konkrétního mono vstupu, správné stereo výstupy a oddělené sběrnice pro suchou kytaru, efekty, podklad, metronom a výsledný mix. Jednotná pravidla úrovní a headroomu.
- Zapnutí monitoringu musí být výslovné. Přidat dostupné ztlumení, měřiče před i po efektech a ochranu před náhlým skokem hlasitosti při přepnutí parametrů či zařízení.
- Preferovat vstup i sluchátka na stejném interfejsu. Přímý hardwarový monitoring popsat jako samostatnou cestu bez softwarových efektů, nikoli jako výsledek optimalizace jádra.
- Hotovo, když signál pouze na zvoleném kanálu zní v obou sluchátkách, nezvolený kanál je izolovaný a výběr nahrávací sběrnice odpovídá skutečně zapsanému obsahu.

<a id="w16"></a>
#### W16 — Společný transport, hodiny a evidence latence

- [ ] Rozpracováno: [vzorkové hodiny, tempo a A/B hranice](docs/windows/w16/README.md), [Windows W16 prošel](docs/windows/w16/windows-validation.json), test proměnlivých bloků a tisíců smyček bez ztráty frakčního přesahu. Předtakt/metronom a metadata navazují ve W18/W22. Závislosti: W13–W15.
- Zavést počítadlo vzorků, převody takty/doby/čas, přesné události uvnitř bloku a sjednocené příkazy play, stop, seek, count-in a loop. Při změně vzorkování bezpečně přepočítat stav nebo zastavit podle předem daného pravidla.
- Oddělit hudební čas, čas nahrávky a okamžik prezentace UI. Metadata zaznamenají konfiguraci zařízení a známé DSP zpoždění pro pozdější kompenzaci stop.
- Zobrazovat zvlášť odhad ovladače, buffer, zpoždění DSP a výsledky skutečného měření. Samotný součet webových baseLatency a outputLatency se nepřebírá jako celková latence.
- Hotovo, když syntetický test prokáže přesné pozice událostí bez kumulovaného driftu přes hranice bloků, smyček a změn tempa; údaje v UI odpovídají svému skutečnému významu.

### Fáze E — Aparát, metronom a analýza kytary

<a id="w17"></a>
#### W17 — Přenést celý aparát a jeho presety

- [ ] Rozpracováno: [plný DSP řetězec, skutečný bypass a Vintage presety](docs/windows/w17/README.md), Linux 7/7 CTest a nativní parameter roundtrip. [Windows formulář/persistence prošly](docs/windows/w17/windows-validation.json); volba nahrávací sběrnice naváže ve W22 a poslech je odložený. Závislosti: W06, W08, W15, W16.
- Implementovat kompresi, vstupní zesílení, overdrive/distortion/fuzz, třípásmový EQ, dosavadní charakteristiky kabinetu, delay, reverb, chorus a výstup. Zachovat smysl parametrů a převod továrních i uživatelských presetů; nový zvuk neslibovat jako bitově totožný s Web Audio.
- Zajistit smoothing, správnou práci se stavem filtrů, ochranu před neplatnými vzorky, DC a denormály, stabilitu zpětné vazby efektů a přechody bez lupnutí. Zpoždění oversamplingu, lookaheadu a případné konvoluce patří do evidence.
- Ověřit skutečný bypass. Vypnutý nebo nulový efekt nesmí nepozorovaně přidávat prodlevu; současná webová komprese v řetězci je konkrétní důvod pro toto ověření. Nastavit jasné chování doznívání efektů při vypnutí.
- Napojit pohled Aparát a společný kompaktní ovladač pro další pohledy. Preset, měřiče a nahrávací zdroj jsou společný stav, zvuk pokračuje při navigaci.
- Hotovo, když offline impulsy, sinusy, šum a referenční riffy projdou kontrolou stability, úrovně a měřitelné latence, presety se obnovují a živá změna parametru nezpůsobuje slyšitelné skoky.

<a id="w18"></a>
#### W18 — Metronom a všechny doprovodné zvuky

- [ ] Rozpracováno: [nativní kliky, celý formulář a presety](docs/windows/w18/README.md). Vzorkové/PInvoke testy i [Windows sestavení/formulář/persistence prošly](docs/windows/w18/windows-validation.json). Společný start nahrávání/Looperu naváže ve W22/W23. Závislosti: W06, W08, W16.
- Přenést všechny současné takty, subdivize, per-beat akcenty a zvuky, tap tempo, swing, předtakt, zrychlování, cvičné ticho, drum mode, polyrytmus, presety a vizualizaci kyvadla.
- Clicky, bicí i pozdější hudební náhledy běží ve společném audio grafu. Hudební události plánovat na konkrétní vzorek; UI kyvadlo a zvýraznění doby odvozovat od transportu.
- Zachovat ovládání tempa číslem, klávesami i velkým tlačítkem v Live. Změny během hraní mají určený okamžik použití a neduplikují dobu.
- Hotovo, když deterministické testy potvrzují pozice dob, polyrytmu, předtaktu a ramp i při proměnlivé délce audio bloku; dlouhé přehrávání nedriftuje.

<a id="w19"></a>
#### W19 — Ladička, detekce tónu a analýza palm mute

- [ ] Rozpracováno: [ladička, worker, kalibrace a společné referenční tóny](docs/windows/w19/README.md). Linux detektor/FFT/worker/RT/PInvoke testy i [Windows formulář/persistence prošly](docs/windows/w19/windows-validation.json). Ruční poslech a technika budou v přejímce. Závislosti: W06, W08, W15, W16.
- Zvolit a ověřit detektor mono tónu s odpovídající licencí, přenést výpočty centů, stabilizaci a confidence gate. Zachovat vedený i chromatický režim, referenční A, značení H/B, tovární a vlastní ladění, referenční tón a volitelný potvrzovací zvuk.
- Kalibraci šumu, měřič vstupu a algoritmus palm mute odvodit ze skutečných vstupních dat. Ticho, slabý či zkreslený signál mají vlastní stav; bez signálu se nezobrazuje poslední tón jako aktuálně měřený.
- Vyhodnocovat mimo audio callback, omezit počet aktualizací UI a měřit stáří výsledku. Vysvětlit, že analýza nízkých tónů potřebuje delší okno než samotný monitoring.
- Hotovo, když testy pokrývají rozsah ladění, rozladění, harmonické, šum, ticho a opakované úhozy. Palm mute prezentovat jako kalibrovanou pomůcku, nikoli spolehlivý univerzální klasifikátor techniky.

<a id="w20"></a>
#### W20 — Diagnostika zvuku a automatické audio kontroly

- [ ] Rozpracováno: [čas grafu, oddělené čítače a diagnostický export](docs/windows/w20/README.md), Linux integrační/RT/PInvoke kontroly prošly. [Windows formulář a náhled reportu prošly](docs/windows/w20/windows-validation.json). Závislosti: W14–W19.
- Zavést bezpečné čítače výpadků, dobu callbacku, maximum front, ztracené bloky, přetečení úrovní a důvody restartu. Jejich sběr nesmí blokovat realtime vlákno.
- Přidat podporovaný diagnostický export s verzí aplikace, zařízením, ovladačem, konfigurací a naměřenými hodnotami. Neobsahuje API klíče ani uživatelovy nahrávky; před odesláním lze obsah prohlédnout.
- Vytvořit opakovatelnou sadu offline a simulovaných integračních testů: impulsy, tónové sekvence, vícero kanálů, různé délky bloků, změny zařízení, dlouhý běh a zatížení UI.
- Hotovo, když lze odlišit chybu DSP, ztrátu dat a problém zařízení. Testy bez hardwaru ověřují zpracování a časování, nikdy nepotvrzují skutečnou latenci M-Track Solo.

### Fáze F — Skladby, nahrávky, Looper a Studio

<a id="w21"></a>
#### W21 — Přehrávání vlastních skladeb a práce s úsekem

- [ ] Rozpracováno: [blokové čtení, samostatný transport a nativní úseky](docs/windows/w21/README.md), [Windows formulář a persistence prošly](docs/windows/w21/windows-validation.json). Rozšíření dlouhých WebM/AAC navazuje před kandidátem. Závislosti: W04, W08, W09, W16.
- Implementovat asynchronní otevření a dekódování souboru, waveform cache, časovou osu, seek, A/B smyčku, změnu rychlosti bez změny výšky, předtakt a postupné zrychlování.
- Velké soubory číst postupně; příprava waveformy a time stretching nesmějí blokovat monitoring. Délku, pozice a mezní hodnoty počítat z audio dat, ne z nepřesného UI časovače.
- Ukládat pojmenované úseky a poslední pozici. Při chybějícím souboru nabídnout jeho dohledání; neplatný formát nesmí ukončit aplikaci.
- Hotovo, když se přehrávání a smyčka chovají správně na krátkých i dlouhých souborech, změna rychlosti zachová ladění a opakované hledání polohy nezanechává staré dekódovací úlohy.

<a id="w22"></a>
#### W22 — Spolehlivé nahrávání, knihovna a export

- [ ] Rozpracováno: [PCM24 diskový writer, obnova po pádu, nativní knihovna a export](docs/windows/w22/README.md). Linux 14/14 CTest, C# propojení a 20 kontrol ukládání nahrávek prošly; [Windows sestavení, formulář a persistence prošly](docs/windows/w22/windows-validation.json). Fyzický záznam je odložený do závěrečné přejímky. Závislosti: W09, W15–W17, W21.
- Nahrávat vybranou sběrnici přes předalokovanou frontu na pracovní diskové vlákno. Délka je odvozena od počtu vzorků. Primární bezeztrátový výstup je PCM WAV; podporovanou hloubku a velké soubory řešit podle reálných limitů writeru.
- Záznam během práce bezpečně průběžně uchovávat, po ukončení dokončit hlavičku a metadata. Zaplněný disk, chyba oprávnění nebo nestíhající zápis nesmějí vytvořit falešně úspěšný záznam; nabídnout obnovu dostupné části.
- Implementovat seznam, přejmenování, výběr více položek, přehrání, pojmenované časové značky včetně skoku na ně, porovnání dvou take-ů A/B, export a mazání s možností návratu či potvrzením. Zachovat původní importované soubory i jejich markery, případný převod označit.
- Pohled exportu rozlišuje suchý zvuk, efekty a mix, formát a rozsah. Systémový dialog vybírá cestu; export běží na pozadí, lze jej zrušit a přepsání je výslovné.
- Hotovo, když restart i simulovaný pád neztratí dokončené take-y, délka a kanály souhlasí se zdrojem a export se přehraje i mimo Rocker.

<a id="w23"></a>
#### W23 — Looper se správným časováním a vrstvami

- [ ] Rozpracováno: [nativní vzorkový Looper, vrstvy a uložené sessions](docs/windows/w23/README.md). Linux 15/15 CTest, kompenzovaný impuls, 20 000 opakování, callback audit a obnovení vrstev prošly. [Windows formulář a projektová persistence prošly](docs/windows/w23/windows-validation.json); poslech je odložený. Závislosti: W16, W18, W22.
- Přenést první záznam určující délku, přehrávání smyčky a overdub vrstev. Určit volné versus taktově zarovnané zahájení, předtakt a pravidla zaokrouhlení hranic.
- Vrstvy mají společnou délku v celých vzorcích, správné úrovně a přechod přes konec bez lupnutí; při nové vrstvě se využije známá kompenzace zachycení a přehrávání.
- Doplnit návrat poslední vrstvy, bezpečné vymazání, persistenci nebo výslovné uložení session a export do nahrávek či Studia. Paměťové limity a ztráta zařízení mají viditelný stav.
- Hotovo, když opakování dlouho nedriftuje, navrstvení impulsů se zarovná podle zvoleného režimu a undo vrátí přesně poslední vrstvu, nikoli celou session.

<a id="w24"></a>
#### W24 — Studio a trvalé vícestopé projekty

- [x] Dokončeno v automaticky ověřitelném rozsahu. Závislosti: W09, W16, W21–W23. [Implementace Studia a zarovnaného nahrávání](docs/windows/w24/README.md) prošla 16 nativními testy, kontrolami CLR a přenosu projektu; [Windows 38178598242](docs/windows/w24/windows-validation.json) prošel opraveným formulářem, CLR a 17 nativními testy. Fyzická zvukovka a poslech zůstávají odložené.
- Přenést současné vrstvení stop, hlasitost, mute a WAV mixdown. Doplnit projekt na disku, relativní média, panorama, solo, časovou osu, zoom, přesný posun a návrat podporovaných editací.
- Zavést jednotné pravidlo overdubu a kompenzace: známé vstupní a výstupní zpoždění i latence DSP se započítají jednou. Pokud nejsou změřené, rozhraní rozlišuje odhad a umožní pozdější kalibraci.
- Offline mixdown používá stejný mix a DSP jako poslech, zohlední délku a dozvuky podle zvoleného rozsahu. Výpadek zvukovky nepoškodí projekt ani nezablokuje offline export.
- Hotovo, když projekt po zavření a přenesení obnoví stopy, mix i pozice, testovací overdub se zarovná a export odpovídá výsledku poslechu v definované toleranci.

### Fáze G — Výuka, trénink a knihovna

<a id="w25"></a>
#### W25 — Lekce a postup výukou

- [x] Dokončeno v automaticky ověřitelném rozsahu. Závislosti: W06, W08, W09, W18, W19. [Nativní lekce a zvukové ukázky](docs/windows/w25/README.md) mají všech 18 lekcí, tři typy kroků, tónovou kontrolu, trvalý postup a přehrávání v jediném grafu. Prošlo 17 nativních testů a kontroly CLR/domény; [Windows 38178598242](docs/windows/w25/windows-validation.json) ověřil oba skutečné WinUI formuláře.
- Implementovat výběr všech lekcí, jednotlivé kroky, přehrání ukázek, tónovou kontrolu, navigaci klávesnicí, dokončení a pokračování po restartu.
- Zachovat rozdíl mezi kontrolou jednotlivého tónu a akordovým režimem „hraj se mnou“. Monofonní detektor nesmí bez dalšího slibovat vyhodnocení celého akordu.
- Chybějící vstupní zařízení blokuje pouze úlohy, které je potřebují. Čtení, poslech a manuální potvrzení vhodného kroku musí mít jasná pravidla, nikoli fingovaně úspěšnou detekci.
- Hotovo, když lze projít všechny typy kroků, obnovit rozpracovanou lekci, uložit výsledek pouze jednou a otevřít konkrétní lekci z plánu.

<a id="w26"></a>
#### W26 — Přesný trénink bez ztráty původní logiky

- [x] Dokončeno v automaticky ověřitelném rozsahu. Závislosti: W06, W19, W21, W25. [Přesný trénink](docs/windows/w26/README.md) má nativní průchod, drill, přehled chyb a oddělené A/B cvičení. Skutečné umělé PCM prošlo C++ analýzou do doménového hodnocení a rozlišilo opakovaný stejný tón od držení; [Windows 38179590084](docs/windows/w26/windows-validation.json) prošel sestavením, 17 nativními testy a oběma skutečnými WinUI instancemi. Fyzická kytara zůstává neověřená.
- Přenést stavy průchod → výběr chybového místa → drill → dokončení ze současného surgicalEngine. Zachovat evidenci chyb na pozicích, výběr problémového okna a tři čistá opakování; parametry odpovídají referenčnímu obsahu a testům.
- Určit rozpoznání nového úhozu a opakovaného stejného tónu. Jeden držený tón nesmí posouvat úlohu každý snímek; dva samostatné úhozy stejné výšky musí jít odlišit.
- V návrhu s waveformou nabídnout vedle tónového drillu režim úseku skladby nad W21. Nezaměnit práci s A/B úsekem za celé dosavadní hodnocení jednotlivých tónů.
- Hotovo, když stejné sekvence správných a chybných tónů vyvolají očekávané přechody a UI jasně ukazuje, zda měří tón, nebo pouze řídí opakování skladby.

<a id="w27"></a>
#### W27 — Power chord, Gallop a Palm mute

- [x] Dokončeno v automaticky ověřitelném rozsahu. Závislosti: W06, W08, W18, W19, W25. [Tři nativní trenažéry](docs/windows/w27/README.md) mají hudební doprovod ve společném jádru, předtakt, 5% zrychlování, rekordy a profil kalibrace palm mute. Linux prošel 18 nativními testy, CLR a kalibrací ze skutečných umělých PCM; [Windows 38180951366](docs/windows/w27/windows-validation.json) prošel i oběma skutečnými WinUI instancemi. Hardware a poslech zůstávají neověřené.
- Přenést výběr cvičení, polohy a zobrazení power chordů, gallop vzory, doprovod, předtakt, změny tempa a záznam rekordů. Hudební náhledy používají společný engine.
- Palm mute má kalibraci, citlivost, skutečnou analýzu vstupu a čitelnou míru jistoty. Nastavení může vyžadovat úpravu pro jinou kytaru, snímač či úroveň; ukládat relevantní profil.
- Nahrávání a kompaktní aparát jsou dostupné přes společné služby, bez druhého spuštěného řetězce. Výsledky se propisují do jednotného pokroku.
- Hotovo, když všechny tři trenažéry fungují na připravených signálech a bez zařízení ukazují správné omezení; skutečnou použitelnost palm mute doplní W44.

<a id="w28"></a>
#### W28 — Akordy, stupnice, riffy a obě hry

- [x] Implementováno a ověřeno automaticky na Windows. Závislosti: W06, W08, W18, W21, W25. [Nativní knihovny a obě hry](docs/windows/w28/README.md) mají 26 akordů, šest stupnic, 14 riffů, propojený tónový drill a společný zvukový doprovod. Doména, Windows build a oba WinUI profily prošly v běhu 38183099284; fyzický poslech a klávesnice zůstávají pro závěrečnou přejímku.
- Vytvořit nativní diagramy a hmatník s prstokladem, barré, levým/pravým provedením, laděním, značením kořenových tónů, filtrováním a přístupnou textovou alternativou.
- Přenést veškerý současný obsah a zvukové náhledy. Riff otevře konkrétní cvičení s odpovídajícím tempem; změna ladění nesmí nabízet neplatné polohy bez vysvětlení.
- Přenést hmatníkovou hru a sluchové intervaly, zadávání myší i klávesnicí, nápovědu, skóre a opakovaný poslech. Sluchové cvičení vyžaduje výstup, ne povinné zapnutí mikrofonu.
- Hotovo, když všechny položky inventáře jdou najít a použít, hry dávají stejné výsledky jako doménové testy a hraní zvukových náhledů nenaruší živý aparát.

### Fáze H — Noty, YouTube, AI a osobní data

<a id="w29"></a>
#### W29 — Nativní noty a tabulatury

- [x] Implementováno; Windows build a oba skutečné WinUI profily prošly v běhu 38183099284. [W29](docs/windows/w29/README.md) obsahuje skutečnou matici sedmi formátů, stopy, zoom, hranou pozici, opakované smyčky a nativní syntézu. Závislosti: W04, W08, W16, W21.
- Implementovat zvolený nativní renderer a podporované současné formáty Guitar Pro a MusicXML. Matice formátů obsahuje skutečné testovací soubory, nikoli jen přípony v dialogu.
- Přidat stopy, zoom, pohyb ve skladbě, zvýraznění hrané pozice, změnu tempa a smyčku taktů. Řešit fonty a soundfonty pro offline instalaci a jejich licence.
- Přehrávání not propojit s hlavním audio grafem přes události či vzorky podle ověřené architektury W04. Nevytvořit nezávislý výstupní engine, který se pere o ASIO zařízení a nerespektuje hlavní hlasitost.
- Hotovo, když reprezentativní soubory projdou načtením, zobrazením i poslechem, chybný soubor nespadne do procesu a noty fungují bez internetu.

<a id="w30"></a>
#### W30 — YouTube a nativní ovládání cvičení

- [x] Implementováno; ověření Windows čeká. [W30](docs/windows/w30/README.md): nativní URL, A/B a ukládání úseků, rychlosti skutečně nabídnuté videem, ochrana před pozdními zprávami a sdílený zvuk. Závislosti: W04, W08, W09, W14, W21.
- Vložit pouze oficiální přehrávač do omezeného WebView2. Nativní UI spravuje URL, uložené úseky, A/B body, názvy a rychlosti, které přehrávač skutečně nabízí.
- Řešit zakázané vložení videa, smazané video, výpadek internetu, změnu stránky a chybějící WebView2 runtime. Dodržet aktuální pravidla přehrávače a nezakrývat povinné ovládání.
- Omezit povolené navigace a zprávy webového obsahu, nevystavit mu souborový systém ani AI klíč. Ověřit souběh zvuku a srozumitelnou nabídku WASAPI, pokud ASIO konfigurace druhý výstup nepřipustí.
- Hotovo, když uložené smyčky fungují a obnovují se, video se při odchodu chová podle stanoveného pravidla a aplikace nepředstírá vzorkově přesné časování internetového přehrávače.

<a id="w31"></a>
#### W31 — Všechny tři části AI tutora

- [x] Implementováno; ověření Windows čeká. [W31](docs/windows/w31/README.md) obsahuje tři nativní pohledy, rušitelný HTTP stream, validovaný upravitelný plán, historii a oddělené DPAPI klíče. Aktuální podmínky Gemini vyžadují změnu poskytovatele pro tuto osobní aplikaci; starý klíč se nepřenáší jinam. Závislosti: W06, W09, W10, W25, W28.
- Přenést rozhovor, generátor plánu a zpětnou vazbu pomocí nativního HTTP klienta s rušením požadavků, streamováním, chybovými stavy a rozumným řízením opakování. Neposílat duplicitní požadavky bez vědomí uživatele.
- Ověřit aktuálně dostupné modely, API, podmínky a limity poskytovatele. Historické údaje o bezplatných kvótách nebo názvech modelů nepřenášet jako zaručeně platné.
- Kontext vychází z profilu, postupu a volitelných poznámek. Výstup plánu validovat proti schématu a existujícím úlohám; před přijetím jej uživatel může prohlédnout a upravit. Odpověď AI není příkaz k souborům či systému.
- Zpětná vazba viditelně říká, že vychází ze statistik; nehodnotí neodeslaný zvuk. Historie má mazání a pravidla exportu, klíč zůstává v systémovém úložišti.
- Hotovo, když všechny tři pohledy projdou testy s mock serverem pro úspěch, neplatný obsah, timeout, zrušení a vyčerpaný limit. Skutečný integrační test používá pouze oprávněně poskytnutý klíč; nikdo jej nevkládá do repozitáře.

<a id="w32"></a>
#### W32 — Profil, pokrok a skutečně aktivní cvičení

- [ ] Implementace a doménové kontroly hotové, nativní Windows kontrola čeká. Závislosti: W06, W09, W25–W28.
- Přenést profil kytary, doporučení nastavení, ladění a orientaci hráče, historii, streaky, achievementy, tempo rekordy a připomínky přestávek.
- Zavést explicitní cvičební session se startem, pauzou a koncem. Live zahrnout do aktivního času; pouhé otevření obrazovky není důkaz hry. Poslechová cvičení a hry se započítají i bez vstupního signálu podle skutečné aktivity.
- Zabránit dvojímu započtení při navigaci, souběhu transportů, uspání a opakovaném dokončení úlohy. Den a streak vyhodnocovat v místním čase uživatele, včetně změny pásma a půlnoci.
- Hotovo, když je pokrok reprodukovatelný z testovacích session, restart nepřičte čas navíc a profil mění související pohledy bez nesouladu ladění.

### Fáze I — Propojení obrazovek a desktopové chování

<a id="w33"></a>
#### W33 — Dokončit Přehled, Live a návaznosti

- [x] Implementováno: [W33](docs/windows/w33/README.md), Windows integrace čeká. Závislosti: W17–W19, W22, W25–W28, W31, W32. Windows run 38187437967 prošel; ruční scénáře zůstávají odložené.
- Denní plán a AI plán otevírají konkrétní lekci, riff, trenažér nebo úsek s potřebnými parametry. Zachovat „pokračovat“ a návrat do plánu, stav hotové úlohy a doporučený další krok.
- Live propojí velkou ladičku, metronom, aparát, vstupní úroveň a jeden skutečný nahrávací transport. Nepovolí spuštění dvou konkurenčních rekordérů ze dvou obrazovek.
- Udržet stabilní ovládání zvuku při přepínání pohledů a režim pro hraní z odstupu. Zabránění uspání platí jen během zvolené aktivní session a po zastavení se uvolní.
- Hotovo, když lze absolvovat celý denní plán od naladění přes cvičení po uložení nahrávky bez ručního hledání stejné úlohy znovu a bez přerušení aparátu navigací.

<a id="w34"></a>
#### W34 — Nastavení a první spuštění

- [x] Implementováno: [W34](docs/windows/w34/README.md), Windows kontrola čeká. Závislosti: W08–W12, W14, W19, W20, W31–W33. Windows run 38187437967 prošel; ruční scénáře zůstávají odložené.
- Sjednotit nastavení zvuku, vstupního kanálu, vzhledu, klávesnice, AI, umístění dat a diagnostiky. Zachovat Zen režim skrývající gamifikaci. Ověřit okamžik uplatnění každé volby a jasně odlišit změny vyžadující restart zvuku.
- Průvodce zahrne profil, případný import, výstup a vstup, otestování úrovně, monitoring a informace o ukládání. Jde dokončit bez kytary i bez AI klíče; lze se k němu vrátit.
- Nabídnout jasně označený ukázkový signál pro ověření aplikace bez zařízení. Reset jednotlivého nastavení není nečekané smazání celé databáze.
- Hotovo, když první spuštění čistého profilu, importujícího uživatele a uživatele bez zvukovky vede k použitelnému rozhraní se správnými omezeními.

<a id="w35"></a>
#### W35 — Dokončit práci se soubory a příkazy Windows

- [x] Implementováno: [W35](docs/windows/w35/README.md); asociace naváže v instalátoru W41, Windows kontrola čeká. Závislosti: W07, W11, W21–W24, W29, W34. Windows run 38187437967 prošel; ruční scénáře zůstávají odložené.
- Skutečné dialogy Otevřít/Uložit, podporované přetažení, poslední soubory, „Zobrazit ve složce“, kontextové nabídky, schránka a asociace vlastního projektového formátu. Otevření souboru z Průzkumníka předat správné instanci.
- Zkratky svázat s jednotnými příkazy a jejich dostupností. Mezerník ovládá příslušný transport mimo psaní textu, Escape zavře vhodný dialog, Ctrl+Z vrací jen definované editace a nemá fingovanou univerzální funkci.
- Ošetřit české a dlouhé cesty, read-only složky, odpojené disky, přepsání souboru, chybějící médium a změnu názvu projektu. Souborové operace jsou zrušitelné a ponechají konzistentní stav.
- Hotovo, když stejné příkazy fungují z menu, klávesnice i pravého tlačítka a export vypadá i chová se jako běžná práce ve Windows.

<a id="w36"></a>
#### W36 — Přejmout všech 32 pohledů a doladit design

- [ ] Všech 32 implementací zapojených, [matice a nativní galerie](docs/windows/w36/README.md); Windows snímky a automatické ověření čekají. Ruční přejímka je odložená. Závislosti: W08, W17–W35.
- Projít matici obrazovek jednu po druhé. Každá musí používat skutečná data a služby, mít hotové povinné stavy a naplnit příslušný pracovní postup, nikoli jen připomínat obrázek.
- Sjednotit české texty, rozměry, řazení ovladačů, hustotu tabulek, čitelnost z odstupu, prázdné stavy a konzistenci transportu. Rozdíly proti konceptu zapsat do návrhové specifikace.
- Pořídit aktuální screenshoty nativního programu a porovnat s návrhy. Ověřit práci myší i klávesnicí na běžném notebooku, velkém monitoru a malém přichyceném okně.
- Hotovo, když je všech 32 řádků přejímací matice splněných a žádná podstatná funkce se neskrývá za nefunkčním tlačítkem či ukázkovým výsledkem.

### Fáze J — Integrita, odolnost a provozní kvalita

<a id="w37"></a>
#### W37 — Ověřit skutečný přechod z webové verze

- [ ] Automatický skutečný browser → SQLite prošel: [W37](docs/windows/w37/README.md); Windows import naváže v buildu. Osobní profil ani živá PWA nebyly měněné. Závislosti: W12, W22–W24, W31, W32, W35.
- Projít export ze skutečného testovacího browser profilu a import do čisté Windows instalace. Porovnat nastavení, postup, vlastní ladění a presety, YouTube smyčky, AI historii i metadata a přehratelnost nahrávek.
- Ověřit starší zálohu, prázdný profil, opakovaný import, částečně poškozený archiv, chybějící zvuk a nepodporovanou budoucí verzi. Report musí ukázat počty přijatých, duplicitních a odmítnutých položek.
- Zajistit návrat k předchozímu desktopovému stavu po přerušeném importu. PWA data zůstávají dostupná; v paměti neuložené Studio či Looper nelze zpětně vydávat za migrované.
- Hotovo, když kontrolní seznam všech 11 stores a všech zahrnutých nahrávek souhlasí a uživatel dostane srozumitelný návod bez zásahů do interních složek prohlížeče.

<a id="w38"></a>
#### W38 — Chyby, přerušení a obnova práce

- [ ] Implementováno: [W38](docs/windows/w38/README.md); místní automatické kontroly prošly, Windows zprávy a AAC čekají na build. Závislosti: W20, W22–W24, W30, W31, W36, W37.
- Simulovat odpojení zařízení a disku, obsazený ovladač, plný disk, chybějící práva, uspání a probuzení, síťový výpadek a ukončení během záznamu, exportu, importu a autosave.
- Ověřit, že se neobnoví nechtěný hlasitý monitoring, nesmaže poslední platný projekt a chyba jednoho online nástroje nevyřadí lekce ani nahrávky.
- Zotavení musí nabídnout konkrétní akci: vybrat zařízení, dohledat médium, obnovit take, uložit jinam nebo operaci zrušit. Log nesmí přehlušit původní chybu dalšími selháními.
- Hotovo, když každý scénář skončí popsaným bezpečným stavem, aplikaci lze dál použít a případná ztráta části nahrávky je přiznaná, nikoli skrytá.

<a id="w39"></a>
#### W39 — Výkon a dlouhý běh

- [ ] Implementováno: [W39](docs/windows/w39/README.md); zrychlených 60 minut místně prošlo, skutečný hodinový hardware běh je odložený. Windows protokol čeká. Závislosti: W20, W24, W29–W31, W36, W38.
- Na popsaném referenčním Windows počítači měřit dobu audio callbacku, vytížení, paměť, start, načítání velkých souborů, počet výpadků a reakce UI. Výsledky vždy uvést s konkrétní konfigurací.
- Zátěž: aparát + metronom + nahrávání, Looper s vrstvami, vícestopý projekt, noty, změna velikosti a DPI, práce v AI a samostatně souběh YouTube. Počet stop a velikost dat zvolit před testem a uvést jako ověřený rozsah.
- Cíl pro vybranou stabilní konfiguraci: 60 minut aktivní session bez ztracených audio bloků, bez rostoucího úniku paměti a bez znatelného blokování UI. Callback musí mít rezervu do délky bloku; sledovat i špičky, nejen průměr.
- Hotovo, když existuje reprodukovatelný výkonový protokol a nastavení s rezervou. Výsledek testovacího PC se nevydává za měření bratrova počítače.

<a id="w40"></a>
#### W40 — Přístupnost, soukromí a kontrola distribuce

- [ ] Implementováno: [W40](docs/windows/w40/README.md); UI Automation a balíkový licenční audit čekají na Windows kontrolu. Ruční přístupnost je odložená. Závislosti: W02, W10, W11, W30, W31, W35–W39.
- Projít celou aplikaci pouze klávesnicí a Narratorem, vysoký kontrast, zvětšený text a různé DPI. Ověřit pořadí fokusu, oznámení změn a možnost ovládat vlastní hudební widgety.
- Prověřit, co opouští počítač při AI, YouTube, aktualizaci a diagnostice. Základní cvičení funguje bez sítě; audio se nikam neodesílá bez samostatného vědomého rozhodnutí.
- Prověřit importy nedůvěryhodných souborů, hranici WebView2, tajné hodnoty v logu a archivu, licenční oznámení a seznam distribuovaných závislostí. Případnou telemetrii oddělit od nutné funkčnosti a výslovně nabídnout.
- Hotovo, když zůstává dostupné veškeré podstatné ovládání a nejsou otevřené chyby úniku dat, nebezpečného importu nebo nevyřešené licence bránící distribuci.

### Fáze K — Instalace a kompletní testovací verze

<a id="w41"></a>
#### W41 — Instalátor a čistý Windows počítač

- [ ] Čeká. Závislosti: W02, W05, W36, W40.
- Ověřit model z W02: unpackaged WinUI aplikace s jednoduchým instalátorem pro daného uživatele. Zvolit nástroj a jeho licenci; MSIX je alternativa pouze při prokázané potřebě. Před cíleným balíkem potvrdit bratrův Windows a kompatibilitu zvolených závislostí.
- Zahrnout správnou architekturu knihoven a potřebné runtime závislosti podle zvoleného modelu instalace: Windows App SDK, .NET, C++ runtime a případně WebView2. Stanovit chování bez internetu a administrátorských práv.
- Placený certifikát není podmínkou tohoto soukromého předání. Ověřit a popsat skutečné chování Windows u výsledného balíku, včetně případného upozornění. Pokud bude zvolen podpis, chránit klíč; nepožadovat obecné vypnutí ochrany Windows.
- Hotovo, když instalace, první start, spuštění souboru, oprava a odinstalace fungují na čistém podporovaném Windows a odstranění aplikace nesmaže nečekaně uživatelovy projekty.

<a id="w42"></a>
#### W42 — Aktualizace, kompatibilita a návrat verze

- [ ] Čeká. Závislosti: W09, W11, W41.
- Zavést verzi aplikace, formátu projektu i databáze, kontrolu kompatibility a migrační zálohu. Aktualizace se nenainstaluje ani nerestartuje aplikaci uprostřed nahrávání.
- Použít ruční předání nové verze podle W02. Stažený balík musí mít ověřený původ a integritu; aplikace se má dát dál používat offline. Samotný kontrolní součet nenahrazuje ověření původu.
- Ověřit aktualizaci přes existující instalaci a rollback. Pokud starší verze neumí nové schéma, nabídnout kompatibilní zálohu místo pokusu otevřít data naslepo.
- Hotovo, když scénář starší instalace → upgrade → kontrola dat → podporovaný návrat neztratí nastavení, projekty ani nahrávky.

<a id="w43"></a>
#### W43 — Uzavřít funkčně kompletního kandidáta

- [ ] Čeká. Závislosti: W01–W42.
- Uzavřít úplnost všech 32 pohledů a všech funkcí v inventáři. Dokončit offline/syntetické testy, Windows integrace, design, migraci, instalaci a dokumentaci známých omezení.
- Připravit instalační balík, poznámky k verzi, krátký návod pro bratra a formulář výsledků testu. Vedle instalátoru zpřístupnit zdarma odpovídající zdroje a build skripty podle W02, samostatným odkazem bez povinného stažení. Přiložit vyžadovaná licenční oznámení výsledných komponent. Kandidát nesmí mít známé blokující chyby ukládání, ztráty dat, licence nebo základního pracovního postupu.
- Oddělit „ověřeno na testovacím Windows“ a „čeká na M-Track Solo“. Pokud schází skutečný AI integrační klíč nebo konkrétní kodek, popsat to jako nesplněnou kontrolu, ne zelený test.
- Hotovo, když bratr dostane ucelenou instalovatelnou aplikaci a nemusí sestavovat projekt ani zkoušet sérii nehotových prototypů. Toto je první plánovaný bratrův přejímací balík.

<a id="w44"></a>
#### W44 — Test s bratrem na M-Track Solo

- [ ] Čeká. Závislosti: W43.
- Provést checklist z kapitoly 7: skutečný systém a ovladač, vstup s kytarou, sluchátka ve stejném zařízení, správný monitoring, ladění, aparát, metronom, nahrávání, Looper, Studio a souběh podkladů.
- Porovnat podporované buffery a backends, zaznamenat výpadky, pocit ze hry i číselná měření. Skutečnou celkovou latenci změřit dostupnou vhodnou metodou; bez měření ji označit za neověřenou.
- Nasbírat konkrétní poznámky k ovládání s kytarou: čitelnost z odstupu, dosažitelnost hlavních příkazů, nečekané zastavení zvuku a srozumitelnost chyb.
- Hotovo, když existuje vyplněný protokol se stabilní konfigurací nebo seznamem reprodukovatelných závad. Samotný test může odhalit nutné změny; úspěch není předem předpokládaný.

### Fáze L — Doladění a předání

<a id="w45"></a>
#### W45 — Opravit zjištění z reálného hraní

- [ ] Čeká. Závislosti: W44.
- Seřadit nálezy: ztráta dat a pády, nefunkční zařízení, výpadky a odezva, nesprávné časování, funkční chyby, potom ergonomie a kosmetika.
- Doladit konfiguraci M-Track Solo, kompenzace, detektor, smoothing a výkonnost podle důkazů. Neřešit špatný pocit ze hry pouze změnou čísla ukazatele latence.
- Každou opravu doplnit vhodným regresním scénářem; u závad závislých na hardwaru nechat ověřit právě postižený průchod, nikoli bezdůvodně opakovat celé testování.
- Hotovo, když nejsou otevřené blokující nálezy a bratr potvrdí, že opravené chování odpovídá skutečnému hraní.

<a id="w46"></a>
#### W46 — Závěrečná regrese a stabilní sestavení

- [ ] Čeká. Závislosti: W45.
- Z finální revize spustit předepsané nativní testy, integrace, migrace, kontroly balení a cílené UI scénáře. Ověřit, že distribuovaný instalátor obsahuje stejné binárky, které byly testované.
- Zopakovat dlouhý audio test a hardwarové scénáře dotčené opravami. Zkontrolovat čistou instalaci i aktualizaci bratrova testovacího profilu.
- Sepsat známá neblokující omezení a skutečně ověřené kombinace Windows, zařízení a ovladače. Nízkou odezvu negarantovat pro libovolnou zvukovku.
- Hotovo, když výsledky odpovídají kritériím vydání a nejsou směšované testy různých revizí nebo nastavení.

<a id="w47"></a>
#### W47 — Uživatelský návod a předání vývoje

- [ ] Čeká. Závislosti: W46.
- Dokončit krátké návody pro instalaci, první spuštění, výběr kanálu, monitoring, nahrávání, projekty, import z PWA, zálohy a řešení typických chyb.
- Aktualizovat README, PROJECT_STATUS, architekturu a pravidla AGENTS pro skutečný nativní stack. Zachovat odkaz na webovou historii, vysvětlit nové testovací a sestavovací příkazy.
- Přiložit rozhodnutí o licencích, seznam závislostí, návod reprodukovatelného sestavení a mapu vrstev. Diagnostika má uživatele vést k věcnému hlášení bez sdílení tajných hodnot.
- Hotovo, když další vývojář dokáže z čistého prostředí aplikaci sestavit a bratr podle návodu zvládne běžný cyklus hraní, uložení a zálohy.

<a id="w48"></a>
#### W48 — Předat finální balík a otevřít další backlog

- [ ] Čeká. Závislosti: W46, W47.
- Připravit finální instalační balík, kontrolní součty, poznámky k vydání a samostatně dostupný archiv odpovídajících zdrojů podle W02. Symboly uchovat pro diagnostiku; podpis není automatickou podmínkou soukromého předání.
- Předání testovací nebo finální verze konkrétnímu příjemci, veřejné vydání a změna nasazení jsou samostatné kroky podle zvoleného způsobu distribuce. Tato roadmapa je sama neprovádí ani nepublikuje.
- Zapsat konečný výsledek přepisu, zachovat možnost obnovy dat a převést zbylé nápady do prioritizovaného backlogu bez míchání s hotovou funkcionalitou.
- Hotovo, když je předaná verze dohledatelná, reprodukovatelná a přijatá podle všech kritérií níže. Další rozšíření má vlastní zadání.

<a id="milestones"></a>
## 5. Milníky a pravidlo dokončení

| Milník | Kroky | Ověřitelný výsledek |
| --- | --- | --- |
| M1 — Proveditelnost | W01–W04 | Rozsah, platforma a licence jsou uzavřené; existují důkazy pro WinUI/C++, noty, dekodéry a video. |
| M2 — Nativní základ | W05–W12 | Sestavení na Windows, shell Vintage, doména, úložiště a migrační formát. |
| M3 — Zvukové jádro | W13–W20 | Zařízení, mono routing, společné hodiny, aparát, metronom, ladička a diagnostika fungují bez nutnosti kytary. |
| M4 — Kompletní funkce | W21–W36 | Veškeré přehrávání, záznam, Studio, Looper, výuka, AI a všech 32 pohledů jsou zapojené. |
| M5 — Kompletní testovací vydání | W37–W43 | Převod dat, odolnost, výkon, přístupnost, instalace a aktualizace jsou prověřené; bratr dostává celý produkt. |
| M6 — Ověřená skutečná kytara | W44–W45 | Protokol M-Track Solo, stabilní konfigurace a opravy potvrzené při hraní. |
| M7 — Připraveno k předání | W46–W48 | Finální regrese, balík, návody, licenční podklady a stav projektu odpovídají sobě. |

Plán není časový slib. Odhad kalendáře má smysl po M1, kdy budou známé výsledky nativních integrací a licenční cesta. Velké kroky W17, W24, W29 a W37 se při realizaci rozdělí na menší změny se samostatnými testy. Části s nezávislými závislostmi lze realizovat souběžně, ale výstup se přejímá podle stejné matice.

Každý dokončený krok zaznamená: revizi, dotčené soubory, stručné výsledné chování, provedené testy, neprovedené kontroly a známé omezení. Zaškrtnutí nestačí bez odpovídajícího výsledku. Implementovaný pohled, software ověřený na Windows a chování potvrzené na M-Track Solo jsou tři různé stavy.

### Kritéria celé migrace

- Všech 32 pohledů a všechny existující funkce mají splněný scénář; desktopová doplnění uvedená v rozsahu jsou funkční.
- Základní ovládání tvoří nativní Windows prvky. WebView2 je pouze vyhrazená integrace YouTube; případná další výjimka musí být samostatně rozhodnutá a zdokumentovaná.
- Audio přežije navigaci, směruje správný vstup, zaznamená správnou sběrnici, umožní stabilní konfiguraci a nezkresluje význam údajů o odezvě.
- Na referenční konfiguraci projde definovaný dlouhý test. M-Track Solo má vlastní protokol; neověřenou kombinaci nelze označit za otestovanou.
- Nahrávky, projekty, záloha i převod z PWA projdou skutečným uložením a opětovným načtením, včetně starých formátů a neúplných dat.
- Instalace, aktualizace, obnova po chybě a odinstalace jsou ověřené. Uživatelská data a tajné klíče mají odpovídající ochranu.
- Nezůstávají blokující pády, ztráty dat, nefunkční základní průchody ani nevyřešené distribuční licence.

<a id="migration"></a>
## 6. Převod dat bez ztráty historie

Zdroj: [backup.ts](src/lib/backup.ts), [recordingsDb.ts](src/lib/recordingsDb.ts) a jednotlivé [stores](src/stores). Aktuální webová záloha má identifikátor aplikace rocker, druh settings-backup a verzi 2; starší verze mohou obsahovat jen část klíčů. Vnitřní hodnoty je nutné parsovat a validovat, nikoli pouze opsat do nativního úložiště.

| Zdrojový klíč | Cílový obsah a zvláštní zacházení |
| --- | --- |
| rocker-settings | Obecná nastavení, notace, ladění a audio preference. Webové identifikátory zařízení přeložit na nové uživatelské potvrzení volby. |
| rocker-progress | Minuty, historie, streaky, achievementy a rekordy. Zachovat minulost; nové měření session platí do budoucna. |
| rocker-instrument | Profil kytary a volby ovlivňující výuku či doporučení. |
| rocker-lesson-progress | Dokončené lekce se stejnými ID obsahu. Rozpracovaný krok PWA neukládá; jeho budoucí ukládání je desktopové doplnění. |
| rocker-yt-loops | Identifikátor videa, úseky a jejich názvy. Nepřenáší se stažený zvuk ani video. |
| rocker-ai | Model a preference. API klíč oddělit od běžných dat a případně převzít jen do systémového úložiště. |
| rocker-chat | Historie konverzací podle uživatelovy volby; zachovat role a text po validaci. PWA čas jednotlivých zpráv neukládá, importer jej nesmí vymýšlet. |
| rocker-amp | Parametry aparátu převést na nativní rozsahy a zdokumentovat odlišný zvuk či jednotky. |
| rocker-amp-presets | Vlastní presety, názvy, ID a parametry; řešit konflikty s továrními. |
| rocker-metronome | Tempo, takty, akcenty, zvuky a rozšířené režimy podle skutečného uloženého schématu. |
| rocker-custom-tunings | Uživatelská ladění s validací počtu strun, tónů a odkazů z profilu. |

Nahrávky jsou samostatné záznamy IndexedDB. Přenosový balík musí obsahovat původní blob, MIME typ a metadata včetně pojmenovaných časových značek; importer ověří obsah i přehratelnost. Název končící .wav z komprimovaného záznamu WAV neudělá. Pokud je nutný převod, dekódovat správným kodekem a zachovat originál či výslovně uvedenou bezeztrátovou pracovní kopii PCM; předešlou ztrátovou kompresi tím nelze odstranit.

Současné Studio a Looper mají pracovní stav v paměti. Neuložené stopy či vrstvy nelze obnovit z historické zálohy. Návod proto před migrací vyzve k uložení/exportu dostupných projektů či mixů a přesně uvede, co bude přeneseno. Nová verze zavádí trvalé projekty, nepředstírá jejich zpětnou existenci.

Pořadí obnovy: ověřit archiv → připravit dočasný prostor → zobrazit obsah a konflikty → vytvořit návratovou zálohu → převést data a média → potvrdit transakci → provést kontrolu odkazů a přehratelnosti → zobrazit report. Poškozené záznamy lze přeskočit pouze s uvedením konkrétního rozsahu; uživatel musí mít možnost celou operaci zrušit. Původní webový profil i zdrojový archiv zůstávají zachované.

<a id="validation"></a>
## 7. Ověřování během vývoje a na skutečném zařízení

### Co lze ověřit bez kytary

| Vrstva | Kontroly | Jaký závěr dovolují |
| --- | --- | --- |
| Obsah a doména | Schémata, stabilní ID, referenční vstupy/výstupy, lekce, pokrok, import | Zachování pravidel a dat |
| DSP offline | Impulsy, sinusy, šum, připravené legální nahrávky, mezní hodnoty | Stabilita, úrovně, algoritmická latence a očekávané zpracování |
| Časování | Různé velikosti bloků, hranice smyček, rampy, polyrytmus, opakované tóny | Přesnost interních událostí a detekční logiky |
| Nahrávání a projekty | Kontrolní signál, délka a kanály souboru, přerušení zápisu, reload, mixdown | Správnost a odolnost práce se soubory |
| Windows audio | Virtuální nebo dostupné zařízení, start/stop, backends, hotplug | Fungování konkrétního testovaného prostředí |
| UI | Windows UI Automation a ruční průchody, myš, klávesnice, Narrator, DPI | Skutečné desktopové chování; screenshot sám nestačí |
| AI a síť | Mock odpovědi, zrušení, chyby, offline; samostatně skutečná autorizovaná integrace | Zpracování kontraktu, případně ověřená služba |
| Distribuce | Čistý systém, upgrade, návrat, asociace, runtime závislosti | Instalovatelnost a kompatibilita daného balíku |

Existující webové testy slouží jako reference doménových scénářů. Nejsou náhradou za testy C++ jádra ani WinUI. Windows testy nelze nahradit úspěšným Linux buildem; fyzický M-Track Solo zase nelze nahradit virtuálním zařízením. Účelem tohoto rozlišení je mít pravdivý stav dokončení, ne brzdit vývoj čekáním na kytaru.

Při změně čistě nativních částí se spouští odpovídající .NET a C++ kontroly; webový TypeScript a Vitest zůstávají povinné pro zásah do referenční PWA či migračního exportu. Zásah do DSP vyžaduje audio regresi, do persistence migrace a obnovu, do UI dotčený pracovní průchod. Celou sadu provést při milníku a kandidátu; neopakovat bezdůvodně všechny testy po pouhé úpravě textu.

### Přejímací protokol pro bratra

Připravit jej před předáním W43 jako krátký vyplnitelný dokument. Uživatel nic nesestavuje. Ke každé chybě se uloží verze balíku, konfigurace, postup, očekávání, skutečný výsledek a volitelná anonymizovaná diagnostika.

1. **Zapsat prostředí.** Windows a jeho verzi, typ počítače, M-Track Solo, skutečný ovladač a připojení. Ověřit podporu výrobce a správný ovladač; přesný model již potvrzený uživatelem není potřeba znovu zjišťovat.
2. **Ověřit zapojení.** Kytara v podporovaném nástrojovém vstupu podle značení zařízení, odpovídající režim vstupu, nepřebuzená úroveň; sluchátka zapojená do stejného interfejsu. V aplikaci vybrat právě tento vstup. Druhý kanál nesmí přimíchávat šum nebo jiný signál.
3. **Rozlišit monitoring.** Pro test efektů a softwarové odezvy poslouchat výstup aplikace. Přímý hardwarový monitoring může vytvořit druhou suchou cestu a výsledek zkreslit; zaznamenat jeho stav. Začít přiměřeně nízkou hlasitostí.
4. **Najít stabilní nastavení.** Vyzkoušet dostupný profil kolem 48 kHz / 128 vzorků, potom podle podpory a stability menší či větší buffer. Zapsat skutečně použité hodnoty, výpadky a vytížení. Nejmenší číslo není automaticky nejlepší použitelná volba.
5. **Posoudit živou kytaru.** Suchý průchod a několik presetů aparátu, změna efektů při hraní, ladění a palm mute. Zaznamenat odezvu, šum, lupání, zkreslení a pohodlí ovládání.
6. **Změřit celkovou odezvu, pokud je dostupné vhodné zapojení.** Použít měřicí nástroj nebo ověřený test impulsem/korelací a vhodnou linkovou návratovou cestou podle možností zařízení. Návratový kanál nesmí současně vytvářet neřízenou monitorovací smyčku. Oddělit měření samotného zařízení a průchodu Rockerem; pro celý software použít externí referenci nebo schéma, které prokazatelně zahrnuje jeho zpracování. Uvést metodu, počet opakování, rozptyl a aktivní DSP. Pouhá velikost bufferu, interní časovač ani test přímého monitoringu nestačí. Pokud vhodné měření nelze provést, zůstane číselná celková odezva neověřená a zaznamená se subjektivní výsledek.
7. **Prověřit celý den cvičení.** Přehled → ladička → lekce nebo riff → metronom → záznam → poslech/export. Ověřit návaznosti, oba výstupní kanály, ukládání a započtení skutečné session.
8. **Prověřit Looper a Studio.** Několik vrstev, delší opakování, nové stopy k podkladu, jejich vzájemné zarovnání, uložení projektu a znovuotevření. Kontrolovat slyšitelný posun overdubu i datový výsledek.
9. **Prověřit podklady a video.** Lokální skladba, noty, YouTube a běžný systémový zvuk. Výslovně zaznamenat, zda ovladač dovoluje souběh a zda funguje připravený WASAPI profil.
10. **Prověřit stabilitu a zotavení.** Definovaná alespoň hodinová aktivní session ve stabilní konfiguraci, potom kontrolované odpojení a připojení zvukovky, uspání a restart aplikace. Nahrávky a projekty musí zůstat dostupné.
11. **Prověřit pohodlí a data.** Ovládání s kytarou, velikost textu, klávesy, přejmenování a export, úplná záloha a její obnovení do testovacího profilu. Nic neobnovovat přes jedinou existující kopii bez návratové zálohy.

Výsledkem není jediné „funguje“. Je to konkrétní doporučený profil zařízení, naměřené či výslovně neověřené hodnoty, seznam nalezených problémů a potvrzení pracovních postupů. Pokud zařízení vyžaduje větší buffer, aplikace ho má nabídnout a pravdivě ukázat kompromis. Po opravě se znovu ověří dotčené chování.

<a id="handoff"></a>
## 8. Hlavní rizika a rozhodnutí, která nesmějí zapadnout

| Riziko | Kdy se řeší | Připravený postup |
| --- | --- | --- |
| JUCE / ASIO nebo mediální licence nevyhovuje distribuci | W02, W04, W43 | W02 vybral AGPL/GPL a oddělené stažení zdrojů; u skutečně sestavených komponent ověřit jejich oznámení a podmínky. |
| AlphaTab renderer není přímo použitelný ve WinUI | W04 | Ověřit .NET renderování a adaptér, případně jiný nativní renderer. Výjimku s webovým UI nelze předpokládat. |
| Staré nahrávky nepřečte zvolený dekodér | W04, W12 | Skutečná matice MIME a kodeků, konverze na pracovní PCM při zachování původního souboru. |
| ASIO a video nemohou sdílet zařízení | W04, W30, W44 | Srozumitelný WASAPI profil a řízené přepnutí; negarantovat souběh neověřeného ovladače. |
| Příliš malý buffer vede k výpadkům | W20, W39, W44 | Měření a stabilní profil místo pevně vynuceného minima. |
| Nové DSP zní jinak než webové presety | W17, W44 | Mapování parametrů, referenční poslechy, zdokumentované změny a doladění na kytaře. |
| Migrace ztratí data nebo vyzradí klíč | W10–W12, W37 | Oddělení tajných hodnot, kontrolní součty, transakce, návratová záloha a report. |
| Návrh slibuje schopnost, kterou starý kód nemá | W01, W26, W31, W36 | Rozlišit původní funkci a desktopové doplnění; AI nevydávat za analýzu nahrávky. |
| Linux cloud dává falešný dojem Windows ověření | W05 a všechny milníky | Povinný Windows runner a ruční UI kontrola; hardwarový výsledek má vlastní protokol. |
| Instalace a aktualizace se řeší příliš pozdě | W02, W05, W41 | Model balení vybrat brzy a průběžně sestavovat zkušební balíky, kompletní přejímka ve W41. |

### Zdroje pro technická rozhodnutí

Zdrojový kód repozitáře byl při přípravě plánu hlavním podkladem pro existující chování. Externí odkazy slouží k ověření konkrétních API a licencí; před uzamčením závislostí je znovu zkontrolovat pro vybrané vydání.

- [Microsoft WinUI](https://github.com/microsoft/microsoft-ui-xaml) a [dokumentace WinUI 3](https://learn.microsoft.com/windows/apps/winui/winui3/) — nativní desktopové rozhraní a platforma.
- [JUCE](https://github.com/juce-framework/JUCE), [licence](https://github.com/juce-framework/JUCE/blob/master/LICENSE.md), [audio devices](https://github.com/juce-framework/JUCE/tree/master/modules/juce_audio_devices), [DSP](https://github.com/juce-framework/JUCE/tree/master/modules/juce_dsp) — moduly, realtime callback a licenční podmínky.
- [AlphaTab](https://github.com/CoderLine/alphaTab) — zdrojový projekt uvádí .NET API a renderery; kompatibilitu konkrétní verze s WinUI a společným audiem je ještě nutné ověřit.
- [Web Audio specifikace](https://github.com/WebAudio/web-audio-api/blob/main/index.bs) a [současný aparát](src/audio/ampChain.ts) — vysvětlení, proč pouhé zabalení webového řetězce neodstraní jeho zpoždění.
- [YouTube IFrame Player API](https://developers.google.com/youtube/iframe_api_reference) a [WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) — referenční dokumentace pro budoucí integrační ověření, nikoli potvrzení již hotové integrace.

Model M-Track Solo je potvrzen uživatelem. Přesný aktuální ovladač, jeho podporované režimy a výsledná odezva nejsou tímto dokumentem ověřeny na zařízení; musí se doplnit z oficiální podpory výrobce a z testu konkrétní instalace. Žádná čísla z konceptu obrazovek se nepřebírají jako specifikace zvukovky.

### Jak pokračovat v další pracovní session

Začít nejnižším nedokončeným krokem tohoto dokumentu, nyní W04. Nejprve přečíst pravidla repozitáře, tento plán, rozhodnutí W02 a aktuální stav; ověřit skutečný Git diff. Před realizací konkrétní změny popsat její výsledek a soubory v rozsahu dohodnuté spolupráce. Po dokončení uložit důkazy kontrol a aktualizovat tento dokument i stav projektu. Podle pokynu z 11. 10. 2026 pokračovat v dohodnutém Windows rozsahu bez dalšího per-step schválení a bez čekání na ruční testy Jendy; ty následují až po W43.

Požadavek na tuto roadmapu znamená vytvoření plánovacího podkladu a uložení návrhů. Neoznačuje nativní jádro za hotové, nepřepíná živou PWA a sám o sobě nepublikuje instalátor. W01–W03 jsou dokončené v uvedeném rozsahu; další konkrétní práce naváže na W04; bratrův test zůstává až za W43.
