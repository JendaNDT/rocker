# W05 — Řešení, reprodukovatelné sestavení a základ vrstev

Zdrojový kořen nativní aplikace zůstává `windows/`, aby se zachovaly dosavadní
sestavovací příkazy a protokoly W03/W04. Původní návrh `desktop/` byl orientační;
kód se nepřesouvá jen kvůli přejmenování. `Rocker.slnx` spojuje WinUI, doménu,
infrastrukturu, interop a mediální adaptéry s jejich kontrolami. C++ má CMake.
PWA ani větev `main` se tímto přepisem nemění.

## Vrstvy a pravidla

- `Rocker.Probe`: aktuální nativní pracovní okno; plný shell a produktové
  pohledy jsou následující W07/W08. Třídy WinUI nepatří do domény ani DSP.
- `Rocker.Domain`: hudební výpočty, čistá pravidla a verzovaný obsah bez UI/audio.
  Aktuální řez portuje všech 69 případů W01, 37 stavů Surgical a pravidla dokončení lekcí; další automaty W06 navazují.
- `Rocker.Infrastructure`: uživatelské cesty, vlastnictví souborů a bezpečná
  výměna textového souboru. SQLite, projekty a migrace jsou W09–W12.
- `Rocker.Interop`: verze a životnost C ABI, SafeHandle a worker příkazy.
  ABI 7 nyní odmítá starší DLL ještě před vytvořením hostu; rozšíření o média
  nebude zjišťováno až chybou chybějícího exportu při přehrávání.
- `Rocker.Notation`: AlphaTab/AlphaSkia import, vykreslení, hit testing a MIDI.
- `Rocker.Video`: typovaná komunikace s oficiálním YouTube, oddělená od WinUI.
- `native`: jediný C++ host, callback, dekodéry, MIDI syntéza a stretch.
- `*.Checks`: skutečné konzolové kontrakty. C++ scénáře používají CTest.

Sdílená verze je v `Directory.Build.props`; načítá ji i CMake. Build ověřuje
shodu manifestu, zaznamenává verzi produktu a přesné použité nástroje.
NuGet graf je v `packages.lock.json`; AlphaSkia native kontroly mají zvlášť
Windows/Linux zámek kvůli skutečně odlišným nativním balíčkům. Windows build
používá `dotnet restore Rocker.slnx --locked-mode`; potom běh/publish bez
nové obnovy balíčků. CMake zdroje JUCE, Signalsmith a libwebm zůstávají připnuté
na přesné revize. Nepoužívají se plovoucí verze ani vypnuté ověření balíčků.

Optimalizovaný native build vytváří PDB i pro DSP/dekodéry. Symboly aplikace
se uchovávají v samostatném CI artefaktu `Windows-debug-symbols`; výsledek,
notové náhledy, verze a testy jsou v `Windows-build-reports`. Interní runtime
balík a odpovídající zdroje zůstávají oddělené. Další download či release se
uživateli nenabízí před funkčně uceleným kandidátem po W43.

## První doménový řez W06

`node scripts/export-windows-content.mjs` převádí současné důvěryhodné TS
konstanty do `windows/content/catalog.json` se schématem 1 a SHA256 zdrojů.
Obsah se neopisuje a W01 reference se nepřepisuje. `--check` porovná živý export;
`--check-frozen` ověřuje všech 15 skupin vůči W01 a hashům bez PWA vývojových
závislostí, takže jej může provést Windows build. Hash textových TS zdrojů používá
kanonické LF konce řádků; ostatní obsah se nezmění. První uzamčená Windows
obnova v běhu 38150103154 prošla, kontrola obsahu však odhalila CRLF checkout;
opravuje se kanonizace konců řádků, nikoli vypnutí kontroly.

Doménový loader hlídá verzi, velikost, ID, duplicity, vazby lekcí na akordy
a cílové noty. Plán kromě původního zobrazovaného chování uchovává konkrétní ID
lekce/trenažéru pro přímou nativní navigaci. Vynucené neplatné či nečíselné
vstupy mají bezpečný výsledek; jejich zlepšené chování vůči neplatným extrémům
PWA je výslovné, ne vydávané za nové měření hry. Žádný uložený API klíč
není součástí katalogu ani referenčních dat.

Před W43 zůstává kontrola obsahu dvou převzatých riffů podle W02, všechny
zbývající trainer automaty, nástroje W29 a kompletní persistence. Přenesené
referenční případy samy neoznačují celou aplikaci ani celé W06 za hotové.

## Lokální kontroly

Linux s .NET SDK z `global.json` může spouštět doménu, infrastrukturu, nativní
fake-device kontrakty, offline média a notový renderer. Windows UI vždy
vyžaduje skutečný Windows runner. Použít vlastní výstupní složky buildů.

```sh
dotnet run --project windows/Rocker.Domain.Checks -c Release -- \
  docs/windows/baseline/fixtures/domain-cases.json windows/content/catalog.json
dotnet run --project windows/Rocker.Infrastructure.Checks -c Release
node scripts/export-windows-content.mjs --check-frozen
```

[Windows protokol](windows-validation.json) potvrzuje čistý build
38150968516: uzamčenou obnovu všech projektů, 69 referenčních případů a 37
Surgical stavů, 7 souborových kontraktů, 167 médií, 39 transportních kontrol,
22 notových kontrol, skutečné otevření/zavření WinUI a lokální WebView2 bridge.
Stažený runtime a samostatné zdroje mají ověřené SHA256, CRC všech sedmi vnitřních
archivů a přesné revize; samostatné nativní a managed PDB mají ověřený formát.
Další rozšíření domény má vlastní [W06 podklady](../w06/README.md).
