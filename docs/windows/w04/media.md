# W04 — offline dekódování a zpomalování

Implementace: `windows/native/include/media_clip.h`, `native/src/media_clip.cpp`
a `native/tests/media_tests.cpp`. Jde o C++ základ pro společný transport,
zatím nikoli o přehrávač nahrávek v okně. Původní testovací tón a notový pohled
se touto částí nemění. W04 zůstává rozpracovaný.

## Dekódování

JUCE 9.0.3 `juce_audio_formats` načítá referenční WAV, MP3 a Ogg/Opus z W01.
Formát se detekuje z obsahu streamu: samotná přípona `.ogg` by nebyla pro
JUCE Opus reader dostatečná, protože ten inzeruje `.opus`. Výstup je vlastněné
planární float PCM, se zachovaným počtem kanálů a vzorkovací frekvencí.
Žádný kanál se automaticky nemíchá; vstup kytary zvolí až směrování transportu.

Referenční MP3 má přes JUCE 49 536 snímků proti 48 000 u WAV/Opus. Součástí
dekódovaného MP3 je priming/padding, což se vejde do původní tolerance W01
50 ms. Modul to neskrývá ani automaticky netrimuje; transport a import mají
pracovat se skutečnou délkou, ne předpokládat totožnost formátů.

WebM/Opus a MP4/AAC tato část **ještě nedekóduje**. Vrací rozlišitelnou chybu
`unsupportedFormat`. Následuje libwebm pro kontejner a systémový Media
Foundation pro AAC včetně chybějících mediálních funkcí Windows N.
FFmpeg byl použit pouze pro přípravu referencí W01 a do aplikace se nepřidává.

## Zpomalení

Signalsmith Stretch 1.4.0, revize
`a670068d9aeb64913331d5cc29337b19a457a7df`, s Linear 0.6.4,
`de55e6a50ffcf6f8f43f649692d94691c7025151`; obě knihovny MIT.
Použitý vestavěný FFT backend nepřidává IPP ani další závislosti.
Revize jsou připnuté v CMake a ověřené při balení; plné zdroje se uchovávají
v samostatném zdrojovém ZIPu.

API `stretch` používá výchozí kvalitnější preset a `exact`, který řeší
počáteční vyrovnání i flush konce. Rychlost 0,25–2x mění počet snímků na
`round(inputFrames / speed)` při nezměněné výšce. Při 1x jde o přesnou kopii. Spektrální randomizace při velkých změnách má
pevný seed a explicitní MT19937, takže opakovaný offline render je
reprodukovatelný na stejném sestavení; bitovou shodu napříč platformami neslibuje.
Příliš krátké klipy se odmítají; žádná tichá náhrada prázdným zvukem.

Výpočty a alokace patří na pracovní vlákno, nikdy do zvukového callbacku.
Výstupní zařízení neotevírá tento modul: přehrávání připojí společný C++ host.
Do WinUI/PInvoke ani do callbacku tato část ještě připojená není.

## Paměť a chyby

Výchozí limity: vstupní soubor 64 MiB, jeden PCM klip 64 MiB a současný
vstup + výstup 128 MiB. To jsou rozpočty souboru a vzorků, nikoli změřený
limit celkové paměti procesu; dekodér a FFT mají další pracovní paměť.
Počet kanálů je 1–2, frekvence 8–192 kHz; délky se kontrolují před alokací.
Neplatné rychlosti, nerovné kanály, NaN/nekonečno, prázdný vstup, nadlimitní
obsah a příliš krátký klip mají vlastní chyby. Jde o interní C++ API;
budoucí C ABI musí chyby převést a nesmí propustit výjimku do C#.
Pro delší nahrávky bude potřeba blokové zpracování/cache v rámci transportu.

## Automatické kontroly

103 kontrol skutečných dekodérů a DSP: formát, délka a 440 Hz, šest rychlostí
na WAV/MP3/Opus, přesná 1x kopie, nízké E, stereo izolace, ticho, poslední
impuls po flush, opakované náběhy a chybové/paměťové hranice. JSON report
obsahuje naměřené výšky a délky; nevyvozuje z nich subjektivní kvalitu hry.
Quarter-speed na syntetickém WAV dosáhl v Linuxu 440,004 Hz při vstupních
440 Hz (tolerance tohoto testu je 3 Hz). Výrobce doporučuje nejkvalitnější
časové změny přibližně 0,75–1,5x; extrémní zpomalení vyžaduje poslech.
Reálná kytara, výkon při dlouhé nahrávce a hardwarová odezva nejsou tímto
testem ověřené. Ruční poslech je odložený do přejímky po W43.

Linux bez zvukového zařízení, CMake 3.31.10 + Ninja + GCC:

```sh
cmake -S windows -B /tmp/rocker-media -G Ninja -DCMAKE_BUILD_TYPE=Release -DROCKER_WITH_JUCE=OFF
cmake --build /tmp/rocker-media --parallel 2
ctest --test-dir /tmp/rocker-media --output-on-failure
```

Pro lokální odpovídající zdroje lze při konfiguraci přidat
`-DFETCHCONTENT_SOURCE_DIR_JUCE=/path/JUCE`,
`-DFETCHCONTENT_SOURCE_DIR_SIGNALSMITH-STRETCH=/path/Signalsmith-Stretch`
a `-DFETCHCONTENT_SOURCE_DIR_SIGNALSMITH-LINEAR=/path/Signalsmith-Linear`.
Bez voleb CMake stáhne připnuté revize. Samotné kontraktové testy bez
JUCE/Signalsmith vyžadují i `-DROCKER_WITH_MEDIA=OFF`.

Windows `build.ps1` provede stejné CTest kontroly a uchová `media-checks.json`
spolu s notami, C# propojením a WinUI smoke testem. Průběžné artefakty slouží
vývoji; uživatel dostane k ručnímu testu až ucelený kandidát.

## Výsledek Windows sestavení

[Windows běh 38142313330](https://github.com/JendaNDT/rocker/actions/runs/38142313330)
na revizi `6df3fd0ec952f0b49d1c9c11058015281a3f2d62` prošel: 103/103 mediálních
kontrol, 2/2 CTest sady, C# propojení a odmítnutí nesprávného ABI, 22/22
notových kontrol, otevření okna s vykreslenými notami a ukončení s kódem 0.

Interní balíky byly stažené z Actions; prošly SHA-256, CRC obou ZIPů i šesti
zdrojových archivů a shodou jejich Git revizí. Testovací backend není v balíku,
EXE a nativní DLL jsou x64. Licence Signalsmith jsou přibalené. Fyzický zvuk
se v CI nepřehrával. Nové vydání pro uživatele se nevytvářelo ani nenabízelo
k předčasnému ručnímu testování.

Úplná měření, nástroje a identifikace balíků: [media-validation.json](media-validation.json).
