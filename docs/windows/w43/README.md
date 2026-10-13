# W43 — kompletní Windows kandidát

Rozsah W04–W43 je nativní přepis současného Rockeru, nikoli další rozšiřování původního nedokončeného backlogu. Celkem 32 pracovních pohledů používá WinUI 3 a společný Vintage systém. YouTube je jediný vyhrazený webový přehrávač. Zvuk běží v odděleném C++20/JUCE jádru; cvičení a vlastní soubory fungují místně.

`windows/verify-candidate.ps1` uspěl nad skutečným Windows sestavením revize `b550d64` v [běhu 38361957245](https://github.com/JendaNDT/rocker/actions/runs/38361957245): nativní testy, doménové a datové kontroly, převod PWA v Chromium, všech 32 skutečných pohledů/35 snímků, UI Automation, instalační cyklus a oddělené odpovídající zdroje. Konkrétní testovanou revizi, běh a výsledek obsahuje `windows-validation.json`; chybějící protokol není úspěšné ověření. Instalační EXE i oba ZIPy mají SHA256. [Soukromé vydání s hotovým instalátorem](https://github.com/JendaNDT/rocker/releases/tag/windows-0.2.0-rc4-20261013).

- [Návod ke spuštění a běžnému používání](user-guide.md)
- [Matice implementace a přejímky](completeness.md)
- [Připravený protokol testu s bratrem](brother-test.md)
- [Skutečné snímky všech obrazovek](../w36/native-gallery/index.html)
- [Roadmapa včetně odložené přejímky W44–W48](../../../WINDOWS_ROADMAP.md)

## Hranice tohoto kandidáta

Windows 11 x64 od 24H2 (build 26100). .NET a Windows App SDK jsou přibalené, ovladač M-Audio ani Evergreen WebView2 nejsou. YouTube potřebuje WebView2 a internet; ostatní hlavní nástroje jej nepotřebují. Na Windows N může AAC vyžadovat Media Feature Pack.

Balík je nepodepsaný. Automatické ověření na Windows runneru nepotvrzuje instalaci s aktivním Smart App Control na uživatelově počítači; globální vypínání ochrany není součástí postupu. Zdrojové kódy jsou volitelně ke stažení zvlášť; bratr nic nesestavuje.

Snímky na runneru ověřují menší/širší rozložení; požadované velikosti 1280/1680 omezila jeho obrazovka, takže nejde o přejímku skutečného velkého monitoru.

Poslech, skutečná odezva od kytary ke sluchátkům, buffer/ASIO M-Track Solo, fyzické uspání a odpojení, skutečný Narrator/vysoký kontrast/DPI, čistá offline VM a online YouTube/OpenAI nebyly během automatického vývoje potvrzené. Performance protokol renderuje 60 minut zvuku zrychleně; nejde o hodinu reálného hraní. Přechod verzí v instalačních kontrolách používá výslovně označené verze stejného kandidáta; netvrdí kompatibilitu se všemi historickými binárkami.

Noty používají vlastní strunnou/bicí syntézu, nikoli celou banku General MIDI. Detektor hodnotí jednotlivé tóny; neslibuje polyfonní vyhodnocení akordu ani obecné přesné skóre rytmu. Palm mute je kalibrovaná pomůcka. Nové DSP není bitově totožné s webovým aparátem. AI je volitelná s vlastním OpenAI klíčem; původní Gemini klíč se zachová odděleně a osobní Gemini cesta je podle zaznamenaných podmínek blokovaná. Skutečný API účet nebyl použit.

Zálohy mají limit 2 GiB; vstup dekódování 1 GiB, PCM cache 8 GiB na zdroj a Looper 8 vrstev/64 MiB. Studio limituje počet stop na 256; výkonový scénář ověřuje 8 souběžných stop, ne 256 na reálném zařízení. Dlouhé WebM/AAC se dekódují blokově přes disk; dočasné raw a roztažené cache mohou současně vyžadovat přibližně 16 GiB. Rozšíření nad dnešní rozsah patří do odděleného backlogu.
