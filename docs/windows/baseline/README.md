# W01 — výchozí stav Rockeru před nativním přepisem

Dokončeno 10. 10. 2026. Tento balík plní [první krok Windows roadmapy](../../../WINDOWS_ROADMAP.md#w01): zachycuje existující funkce, jejich zdroje, data, referenční vstupy a skutečné výsledky kontrol. Nativní aplikace ani její audio tímto krokem nevznikají.

## Co je zafixované

- Výchozí revize PWA: `77df8e37e318e01d0d135db3a8640cf046d64fa5`. Aplikační zdroje a lockfile jí při pořízení odpovídají. Předchozí roadmapa a návrhy již byly necommitovanými změnami pracovní kopie; tento krok je zachoval.
- [Manifest](manifest.json): revize, skutečné verze balíčků a SHA-256 celkem 259 aplikačních, konfiguračních a obsahových souborů. Verze se čtou z lockfile, nikoli jen z rozsahů package.json.
- [Čitelná inventura](inventory.md) a její [strojová podoba](inventory.json): všech 32 pohledů, společné funkce, odkazy na zdroje a existující testy, data, přejímací scénář a známý rozdíl vůči návrhu.
- [Výsledky ověření](validation.json): čerstvé kontroly současné PWA, samostatné kontroly podkladů a výslovně neprovedené kontroly.
- [Návrhy všech pohledů](../design/vsechny-navrhy.md) zůstávají vizuálním zadáním. Inventura rozlišuje, co už je v PWA a co má teprve přibýt.

## Referenční obsah

[content.json](fixtures/content.json) je otisk dat ze zdrojových modulů, nikoli zatím budoucí produkční formát Windows aplikace. Zůstávají stabilní ID i český obsah. Funkce včetně podmínek achievementů nejsou serializovány do JSON; jejich chování pokrývají zdrojové testy a reference logiky.

| Obsah | Skutečný počet |
| --- | ---: |
| Hlavní sekce | 13 |
| Etapy / lekce | 3 / 18 |
| Akordy / stupnice / riffy | 26 / 6 / 14 |
| Achievementy | 11 |
| Tovární ladění | 9 |
| Tovární presety aparátu / metronomu | 4 / 6 |
| Taktové míry / zvuky kliku | 7 / 5 |
| Cvičení Přesného tréninku | 5 |
| Power chord / Gallop / Palm mute cvičení | 3 / 2 / 2 |

[domain-cases.json](fixtures/domain-cases.json) obsahuje 69 skutečných vstupů a výsledků současné čisté logiky: frekvence, notace, tempo, swing, akordy, čas cvičení, plán, streaky, markery, názvy exportů, chat a gallop. Číselné výsledky jsou současná JavaScript reference; při portu do C++/C# používat zdůvodněnou numerickou toleranci, nikoli slepou bajtovou rovnost floating point výpočtů.

[surgical-traces.json](fixtures/surgical-traces.json) obsahuje tři úplné průchody stavovým automatem: čisté cvičení, stejné pitch classes o oktávu výše a chybu následovanou třemi čistými drilly. Zachycují původní chování. Neříkají, že živý detektor správně rozpozná každý skutečný úhoz.

## Zálohy a nahrávky pro migraci

Veškerá uživatelská data v této sadě jsou uměle vytvořená. Nejde o kopii bratrova profilu. API klíč je prázdný a zkušební video ID není určené k síťovému přehrání. JSON stringy stores zachovávají skutečný obal Zustand se stavem a verzí.

| Soubor | Účel a ověřený výsledek |
| --- | --- |
| [settings-v2.json](fixtures/settings-v2.json) | Všech 11 klíčů, vlastní ladění/presety, Zen, levá ruka, česká notace, historie, profil a pokrok. Skutečné stores při rehydrataci zachovají očekávané hodnoty. |
| [settings-v1.json](fixtures/settings-v1.json) | Starší formát s prvních sedmi skupinami dat; strukturálně platná záloha. Chybějící novější skupiny se nesmějí vydávat za vyexportované. |
| [settings-partial.json](fixtures/settings-partial.json) | Pouze nastavení; ověřená platnost obalu, budoucí importer musí explicitně řešit chybějící data. |
| [settings-invalid.json](fixtures/settings-invalid.json) | Cizí identifikátor aplikace; současný guard zálohu odmítne. |
| [settings-corrupt-value.json](fixtures/settings-corrupt-value.json) | Platný obal, neplatný vnitřní JSON pokroku. Dokládá nutnost dvojí validace v budoucím importeru. |
| [recordings.json](fixtures/recordings.json) | Pět umělých záznamů včetně MIME, délky, času vytvoření a pojmenovaných časových značek. Po připojení odpovídajícího blobu projdou skutečným sanitizeRecording. |

Referenční obal recordings.json je pomocný formát baseline, nikoli již implementovaný export IndexedDB. Kompletní migrační export vznikne ve W12. PWA dnes zálohuje nastavení odděleně a audio do této zálohy nezahrnuje.

## Zvukové a notové soubory

[media.json](fixtures/media.json) uvádí původ, parametry, očekávání a kontrolní součty každého souboru. Vzorky byly vytvořené matematicky pro tento projekt; neobsahují cizí skladbu, skutečnou kytaru ani osobní nahrávku. Nové autorské testovací podklady v této sadě se řídí [MIT licencí projektu](../../../LICENSE).

| Podklad | Obsah | Co se ověřuje |
| --- | --- | --- |
| [Ticho](fixtures/audio/silence.wav) | 1 s, mono, 48 kHz, PCM16 | Žádný platný detekovaný tón |
| [A4](fixtures/audio/a4-440.wav) | 1 s, 440 Hz, amplituda 0,2 | Referenční frekvence a ladění |
| [E2](fixtures/audio/low-e2.wav) | 2 s, MIDI 40, amplituda 0,2 | Nízká kytarová struna bez reálného nástroje |
| [Pouze vstup 1](fixtures/audio/input-1-only.wav) | Stereo; 110 Hz vlevo, vpravo nula | Výběr kanálu a správné mono směrování |
| [Pouze vstup 2](fixtures/audio/input-2-only.wav) | Stereo; vlevo nula, 220 Hz vpravo | Opačný kanál; žádné skryté sčítání |
| [Impulsy](fixtures/audio/impulses.wav) | Pulsy na vzorcích 4 800, 24 000 a 43 200 | Softwarové časování a zarovnání; nikoli měření fyzické latence |
| [Opakované E2](fixtures/audio/repeated-e2.wav) | 4 tóny po půl sekundě, oddělené tichem | Budoucí detekce opakovaného stejného tónu |
| [WebM/Opus](fixtures/audio/a4-440.webm), [Ogg/Opus](fixtures/audio/a4-440.ogg), [MP4/AAC](fixtures/audio/a4-440.m4a), [MP3](fixtures/audio/a4-440.mp3) | Překódování vlastního A4 | Reálné kodeky pro prověření dekodéru; tolerance délky počítá s kontejnerem/paddingem |
| [MusicXML](fixtures/notation-scale.musicxml) | Vlastní stupnicový úsek C–D–E–F, 1 takt a 1 stopa | Čtyři noty se načtou aktuálním AlphaTab parserem |

PCM podklady používají sinus `0.2 × sin(2π × f × n / 48000)`, zaokrouhlený na PCM16; u nízkého E platí `f = 440 × 2^((40−69)/12)`. Opakované tóny mají půlsekundový cyklus, zvuk prvních 0,375 s s exponenciálním útlumem `exp(−5t)` a zbytek ticho. Impulsy mají amplitudu 0,5. Tyto jednoduché signály nejsou realistický model snímače ani důkaz fungujícího rozpoznání palm mute.

Komprimované vzorky vznikly FFmpegem zaznamenané verze z a4-440.wav, bez vstupních metadat, s datovým tokem 96 kbit/s: libopus pro WebM/Ogg, AAC pro M4A a libmp3lame pro MP3. Všechny čtyři se úspěšně dekódovaly. Jejich bajty jsou zafixované kontrolními součty; jiná verze enkodéru nemusí vytvářet shodné soubory. Tento výsledek neprokazuje schopnosti dosud nevybraného nativního dekodéru.

Skutečné ukázky formátů Guitar Pro zatím v sadě nejsou; cílenou matici a původ souborů doplní W04. MusicXML se v tomto kroku ověřovalo parserem, nikoli vykreslením v nativním okně nebo přehráním přes ASIO.

## Důležité odchylky a neznámé

1. Denní plán má v dnešní logice 17 minut a směruje jen na obecné sekce. Přímé otevření úlohy a její dokončení v plánu patří do W33.
2. Live se nezapočítává do času cvičení. Staré cesty Studia/Looperu se přesměrují na nezapočítávaný recorder. Pravidla aktivních session opraví W32.
3. Záloha neobsahuje nahrávky a může obsahovat skutečný AI klíč, pokud si ho uživatel uložil. Nová migrace musí obojí řešit výslovně.
4. Nahrávky mají markery. Jejich vynechání by znamenalo ztrátu uživatelských dat; jsou zahrnuté v referenci i roadmapě.
5. Dokončené lekce se ukládají, pozice rozpracovaného kroku nikoli. Chat obsahuje pouze roli a text; historické časy zpráv nejsou k dispozici a importer je nesmí vymýšlet.
6. Tónová detekce používá A4 = 440 Hz; nastavitelná reference není současná uložená volba. Sousední stejné tóny dnešní Přesný trénink nerozlišuje a obsah je záměrně neobsahuje.
7. Studio a Looper mají pracovní stav v paměti. Pan, solo, trvalé projekty a vracení jednotlivých vrstev patří mezi nová desktopová rozšíření.
8. Současný ukazatel latence neobsahuje celou vstupní cestu. Audio z více vstupů se v nahrávací větvi sčítá. Výběr jediného fyzického vstupu a měření celkové odezvy jsou nativní práce.
9. AI zpětná vazba je odvozená ze statistik, nikoli z poslechu. Aktuální dostupnost modelů, ceny a limity služby je třeba ověřit samostatně.
10. Hardware, ovladač, souběh YouTube/ASIO, skutečná odezva, kvalita DSP při hraní a ergonomie WinUI zatím nejsou potvrzené. Jejich místo v roadmapě je W04, W39 a W44; dokončení inventury je neoznačuje za hotové.

## Původ obsahu a licence pro následný krok

| Obsah | Zjištěný podklad | Další postup |
| --- | --- | --- |
| Vlastní zdrojové soubory a běžný výukový obsah | Kořenová MIT licence; zafixované zdroje a ID | Ve W02 potvrdit úplnost oznámení a distribuční model. |
| Dva riffy pojmenovaných skladeb | Smoke on the Water a Seven Nation Army mají ve zdroji uvedeného původního interpreta | Není doložené samostatné oprávnění k distribuci přepisu skladeb. Ve W02 rozhodnout licenční podklad nebo náhradu; nemazat je potichu z funkční inventury. |
| Ostatních 12 riffů | Zdroj je výslovně označuje jako původní cvičení | Zachovat původ a ověřit jej při obsahovém/licenčním uzavření. |
| Bravura | [OFL 1.1 přímo u fontu](../../../public/alphatab/font/Bravura-OFL.txt) | Zachovat licenční text, copyright a podmínky názvu při balení. |
| SONiVOX | Veřejný sf3 má shodný SHA-256 s balíčkem AlphaTab 1.8.3; [licenční text balíčku](licenses/sonivox-LICENSE.txt) uvádí Apache 2.0 | Pro nativní distribuci přibalit příslušné licence/oznámení. Ve veřejné soundfont složce současné aplikace samostatný text není. |
| AlphaTab | Nainstalovaná a zamčená verze 1.8.3, balíček uvádí MPL-2.0 | W02 řeší podmínky budoucí distribuce; W04 proveditelnost .NET integrace. |
| Nové testovací tóny, XML a umělé profily | Vytvořené pro W01; žádná osobní data ani komerční nahrávka | MIT projektu; lze používat pro budoucí testy. |

Tato tabulka je inventura podkladů a otevřených rozhodnutí. Nepředstavuje hotové licenční schválení celé nativní distribuce a neprovádí žádné změny licencí.

## Jak kontrolu zopakovat

Z kořene repozitáře s jeho nainstalovanými závislostmi:

```sh
node scripts/verify-windows-baseline.mjs
npx tsc --noEmit
npm test
npm run lint
```

Baseline kontrola nic nepřepisuje. Porovnává zdrojové hashe, obsah a očekávané výsledky, kontroluje inventuru, skutečnou rehydrataci všech stores, záznamy s bloby, kanály a impulsy PCM i parse MusicXML. Neprovádí síťové volání AI ani YouTube. Po úmyslné změně referenční PWA bude drift hlásit chybu; aktualizace reference musí být samostatně zdůvodněná, nikoli automatické přepsání očekávání.

Výsledek této session: **561/561 existujících testů v 54 souborech, 0 přeskočených; TypeScript a lint prošly.** Samostatná baseline kontrola prošla pro 32 pohledů, 11 stores, 69 referenčních případů, 3 průchody drillem, 12 mediálních souborů a 259 zdrojových hashů. Produkční build se podle pravidel tohoto sandboxu nespouštěl. Kontroly nativního Windows programu a fyzického zvuku dosud nelze provést.

Dalším krokem je [W02 — platforma, licence a distribuce](../../../WINDOWS_ROADMAP.md#w02). W01 je hotový podklad pro tento krok, nikoli automatické rozhodnutí o JUCE licenci nebo minimální verzi Windows.
