# W04 — původní WebM a AAC nahrávky

Offline modul nyní rozšiřuje původní WAV/MP3/Ogg/Opus cestu o WebM/Opus a
systémové MP4/AAC. Přehrávač v okně a společný transport zatím zapojené nejsou.
W04 zůstává rozpracovaný; syntéza not a YouTube mají vlastní následující části.

## WebM/Opus

`webm_decoder.cpp` používá pouze parser z libwebm 1.0.0.31,
`6745fd29e0245fc584b0bb9f65018ea2366fe7fb`, pod BSD-3-Clause s patentovým
grantem. Vlastní souborový adaptér používá Unicode cestu a ověřuje rozsahy
čtení. CMake nesestavuje upstream vzorové programy ani nemění globální
kompilátorové volby jejich starším projektem.

Dekódování Opus paketů používá přímo implementaci již přibalenou v připnutém
JUCE; nepřidává další odlišnou kopii Opus. Toto napojení na vendored header
je vázané na přesnou revizi JUCE a změna JUCE vyžaduje nové integrační ověření.
OpusHead určuje počet kanálů a zisk. Podporované jsou mono/stereo s mapováním 0,
právě jedna zvuková stopa A_OPUS a souvislá časová osa. Video stopy se ignorují.
Šifrování, jiná mapování, více zvukových stop a časové skoky se výslovně odmítají.
Kvantování časových značek kontejneru má toleranci 4 ms, nikoli volné ignorování
mezer. Není to univerzální editor Matrosky.

Počáteční CodecDelay (případně OpusHead pre-skip) se odečítá právě jednou.
DiscardPadding se aplikuje na svůj blok. Referenční mono WebM proto dává
přesně 48 000 snímků, nikoli 1,008 s délky kontejneru. Výstup je 48 kHz PCM;
vstupy se zachovávají odděleně. Nadlimitní pakety, padding, PCM, poškozený
OpusHead a neúplný kontejner končí chybou.

## MP4/AAC a Windows

`mf_decoder.cpp` používá systémový synchronní Source Reader s float PCM.
Volá se na pracovním vlákně s MTA COM apartmentem. Otevře jen zvukovou stopu,
vyjedná a ověří frekvenci, kanály, 32bitový float a blokové zarovnání; interleaved
vzorky převádí do stejného planárního Clip jako ostatní dekodéry.
COM objekty, zamčené buffery, startup a shutdown mají vlastněný životní cyklus
včetně chybových větví. Délka se odvozuje ze skutečných vzorků; žádné automatické
trimování podle deklarované délky kontejneru.

`mfplat.dll` a `mfreadwrite.dll` se načítají přes LoadLibraryEx ze System32 až
při požadavku na MP4. Funkce se získají přes GetProcAddress. Statically linked
jsou pouze GUID/COM knihovny; chybějící volitelná DLL nesmí znemožnit spuštění
ostatních částí. Windows build přes dumpbin ověřuje absenci eager MF importů
v mediálním kontrolním EXE. DLL ani kodeky Windows se do ZIPu nepřibalují.

Linux vrací `platformUnavailable`, nikoli předstíraný výsledek dekódování.
Stejný typ chyby vrací Windows při chybějících DLL/funkcích nebo nepodařeném
spuštění Media Foundation. Čistý Windows N bez Media Feature Packu není
v tomto běhu ručně ověřený; tuto situaci nelze označit za fyzicky otestovanou.
Nativní UI později převede stav na českou informaci a ostatní formáty zůstanou
použitelné. Podpora dalších kodeků MP4 se odvíjí od systému; důkaz se týká AAC.

## Kontroly a stav

Linux ověření aktuálně prošlo 138 kontrolami skutečných dekodérů/DSP a 2/2
CTest sadami. Windows běh musí navíc skutečně načíst mono/stereo AAC,
zachovat aktivní kanál, projít šesti rychlostmi, načíst cestu s českými znaky
a odmítnout poškozené MP4. Tyto případy nejsou nahrazené mockem.

Tests nyní očekávají, že referenční WebM lze dekódovat. Původní assertion
`unsupportedFormat` se změnil na kontroly skutečného PCM. AAC zůstává na
Linuxu explicitním stavem `platformUnavailable`, na Windows má kladné
kontroly. Původní ověřený report 103 kontrol se nepřepisuje; další běh má
samostatný protokol a identifikaci sestavení.

Zálohy W01 a jejich soubory zůstávají nezměněné. Přibyly pouze umělé
stereo reference a WebM s neznámou délkou segmentu v `windows/fixtures/media`, jejich původ, příkazy a hashe
jsou v tamním README. Poškozené varianty se vytvářejí v unikátní dočasné
složce testu a po dokončení se odstraní. FFmpeg do aplikace nepatří.

Paměťové limity z [první mediální části](media.md) platí dál pro soubor a
logické PCM. Kapacity vektorů, parser a systémový dekodér mají další pracovní
paměť; nejde o naměřený pevný limit procesu. Dlouhé klipy potřebují další
blokové zpracování/cache. Subjektivní kvalita na kytaře a fyzický poslech
zůstávají v ruční přejímce po W43.

Rozšířená Linux sada také prošla AddressSanitizer, UndefinedBehaviorSanitizer
a detekcí úniků (`halt_on_error=1`, 138 kontrol, 2/2 CTest). To zahrnuje
poškozené WebM a kontejner s neznámou velikostí/délkou; nejde o univerzální
fuzzing ani záruku bezchybnosti cizích kodeků. Windows původní sadu ověřil
běh 38143961620, včetně skutečného AAC; streaming reference dostává navazující
samostatné Windows sestavení.
