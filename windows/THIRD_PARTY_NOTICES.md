# Licence Windows aplikace

Nové vlastní soubory v této složce jsou pod AGPL-3.0-only; viz [LICENSE](LICENSE).
Původní PWA a její kořenová MIT licence se nemění.

- JUCE 9.0.3: vybrána AGPLv3, původní copyright a licence ve zdrojovém vydání.
- ASIO hlavičky přibalené k JUCE: vybrána GPLv3; nepřebírá se licence zvukového ovladače M-Audio.
- JUCE obsahuje další komponenty s vlastními oznámeními; referencí je SPDX soupis tohoto vydání.
- .NET a Windows App SDK mají vlastní licence a podmínky distribuovaných součástí.
- AlphaTab .NET 1.8.3 (MPL-2.0), AlphaSkia 3.4.135 a jeho Windows knihovna
  (BSD-3-Clause, oznámení Skia a HarfBuzz); používají se nezměněné NuGet balíčky.
  Přesné zdroje AlphaTab a AlphaSkia jsou v samostatném zdrojovém balíku.
- Bravura zabudovaná do AlphaTab a Noto Sans zabudované do Rocker.Notation
  mají SIL Open Font License; úplné texty jsou přibalené v `licenses`.
- Offline mediální modul používá Signalsmith Stretch 1.4.0 a Linear 0.6.4
  pod MIT; přesné revize a úplná oznámení jsou ve zdrojovém balíku a `licenses`.
  Modul je připojený ke společnému C++ přehrávači v okně. Syntéza not je vlastní
  procedurální kód pod AGPLv3 bez přibaleného SoundFontu.
- libwebm 1.0.0.31: parser pod BSD-3-Clause, včetně samostatného patentového
  grantu. Přesné zdroje a oba texty jsou přibalené. Opus používá existující
  implementaci z JUCE; systémové AAC/Media Foundation DLL se nepřibalují.
  Úplná oznámení Ogg, Opus, Opusfile a libopusenc a patentové informace Opus
  jsou přibalené samostatně v `licenses`.

Přesné revize a původ jsou v `build-info.json` a `component-manifest.json` skutečného balíku. Manifest uvádí obnovované NuGet závislosti, licence a SHA256 publikovaných binárek. Úplné originální licenční texty a oznámení jsou ve složce `licenses`, včetně .NET runtime, Windows App SDK, SQLite/SQLitePCLRaw, MIT migračního exportéru PWA a Inno Setup 6.7.3. Systémové kodeky Media Foundation, M-Audio ovladač a Evergreen WebView2 runtime se nepřibalují.

Odpovídající zdroje Rockeru, JUCE, Signalsmith Stretch/Linear, libwebm, AlphaTab a AlphaSkia jsou samostatný `Rocker-Windows-sources.zip` vedle instalačního EXE u stejné revize soukromého vydání. Bratr je nemusí stahovat ani kompilovat. Původní vendor zdroje nejsou upravené. Podklady jsou v [rozhodnutí W02](../docs/windows/decisions/0001-platform-and-distribution.md) a [auditu W40](../docs/windows/w40/README.md). Audit musí projít nad skutečně sestaveným balíkem; samotný tento přehled nenahrazuje přibalená úplná oznámení.
