# W03 — Nativní okno a zvukový technický řez

První implementace pro ověření C# / WinUI a C++ / JUCE. Není to ještě hlavní
obrazovka Rockeru, hotové zvukové jádro ani verze určená bratrovi.
Platí [rozhodnutí W02](../docs/windows/decisions/0001-platform-and-distribution.md).

## Spuštění předaného balíku

Jenda dostává už sestavenou aplikaci; vývojové nástroje nejsou potřeba.
Z příslušného ověřeného vydání stáhne `Rocker-Windows-x64.zip`, použije
**Extrahovat vše** a spustí `Rocker.Probe.exe` z rozbalené složky. Ostatní
soubory musí zůstat vedle EXE. Vyžaduje Windows 11 x64 od 24H2.
Zdrojový ZIP z tlačítka GitHub **Code → Download ZIP** spustitelnou aplikaci
neobsahuje. `Rocker-Windows-sources.zip` je samostatná volitelná nabídka zdrojů.

Balík zůstává technickým prototypem s testovacím tónem. Plná aplikace a Vintage
obrazovky ještě nejsou implementované. Automatická kontrola otevření a zavření
okna nenahrazuje ruční přejímku ani poslech skutečného zařízení.

## Automatické sestavení před předáním

[Windows workflow](../.github/workflows/windows-prototype.yml) na změny Windows
části ve větvi `windows-native-prototype` spustí sestavení na `windows-2025`,
nativní a C# integrační testy, publikování WinUI a kontrolu spuštění přes
`smoke.ps1`. Teprve po úspěchu `package.ps1` připraví samostatný spustitelný ZIP,
odpovídající zdroje (včetně připnutého JUCE) a SHA-256 součty.

CI používá dostupný MSVC x64 z obrazu runneru, CMake 3.31.10 a přesné .NET SDK
z `global.json`. Skutečná verze MSVC, Windows SDK, operačního systému a nástrojů
se zapisuje do `out/reports/toolchain.json` a `build-info.json` v balíku.
Volba dostupného MSVC pro CI není tvrzení, že runner obsahuje dříve zvažované
Visual Studio 18.10.0. Obraz runneru a výsledek se evidují pro konkrétní běh.

Artefakt `Rocker-Windows-downloads` obsahuje oba ZIPy a součty. Před odkazem
uživateli musí agent stáhnout artefakt z úspěšného běhu, ověřit jeho obsah a
součty a zveřejnit soubory jako soukromé GitHub předběžné vydání stejného commitu.
Zdrojový archiv musí zůstat dostupný vedle binárního balíku; do něj se nepřibaluje.
Veřejné vydání ani předání bratrovi tento postup nezavádí.

## Co kód obsahuje

- Skutečné WinUI okno, standardní tlačítka s přístupovými klávesami, stav a systémový titulek.
- Testovací sinus 440 Hz, úroveň nejvýše 0,02 pro nastavení z UI, shodně v obou výstupech.
- Žádný automatický start ani mikrofonní vstup. Ve Windows se pro tento test vybírá WASAPI a výchozí výstup; výběr ASIO a kytarového vstupu patří do W14/W15.
- Verzi C ABI, kontrolu velikostí struktur, kopírování UTF-8 chyb a vlastnictví neprůhledného handle přes C# SafeHandle.
- Serializované řídicí volání; JUCE má vlastní řídicí vlákno a message loop. Zvukový callback nevolá C#, UI, disk ani síť a nevytváří objekty.
- Zavření okna zastaví časovač, uvolní host a čeká na ukončení jeho zvukových callbacků a řídicího vlákna.
- Samostatnou testovací knihovnu se simulovaným zařízením. Její název se liší od produkční DLL a do UI balíku se nekopíruje.

První host obsluhuje jedinou skutečnou instanci audio prostředí v procesu.
Řídicí volání jsou synchronní, takže otevření zařízení může krátce zablokovat
testovací UI; plný asynchronní stavový automat a zrušení požadavků patří do W13.
Ukazatel bufferu nepředstavuje celkovou latenci kytary. Při skutečném odpojení
zařízení se zatím hlásí obecná chyba; finální zotavení patří do W14.

## Ověření na Windows

Windows 11 x64 od 24H2, .NET SDK podle `global.json`, Visual Studio
s C++20 build nástroji a Windows SDK 26100, CMake 3.31.10, Ninja a Git. Použít x64
Developer PowerShell. První sestavení potřebuje internet pro JUCE a NuGet.
Kompilátor a skutečné verze se uloží do protokolu; tento skript nebyl na Linuxu
vydáván za provedený Windows build.

```powershell
cd windows
.\build.ps1 -Run
```

Při sestavování ze samostatně nabídnutých zdrojů lze předat cestu k rozbalenému
JUCE: `.\build.ps1 -JuceSource ..\..\JUCE`. Tím se použijí odpovídající místní
zdroje místo nového stažení JUCE.

Vznikne adresář `out/probe` s interním spustitelným prototypem. Skript provede
nativní testy a skutečné P/Invoke kontroly proti testovacímu zařízení; ty samy
neověřují zvukový ovladač. Sestavení hlavního DLL navíc kompiluje skutečný JUCE
backend, včetně povoleného ASIO. CMake používá Ninja a již aktivované MSVC;
nepotřebuje generátor Visual Studia novější než zvolený CMake.

Potom ručně ověřit a zaznamenat:

1. Start bez zvuku, běžné ovládání okna, Tab a přístupové klávesy.
2. Po tlačítku Spustit tón zní v obou výstupech; začít nízkou hlasitostí sluchátek.
3. Zastavení a alespoň 20 opakování start/stop, bez zbylého tónu či chyby.
4. Zavření během přehrávání skutečně ukončí proces, tón i vlákna a uvolní zařízení.
5. Chybějící DLL a neodpovídající ABI skončí čitelnou chybou v okně.
6. Nedostupné/odpojené zařízení nezpůsobí pád, lze bezpečně zastavit a zkusit obnovu.
7. Opětovné spuštění aplikace po každém z těchto stavů funguje.

Zaznamenat OS, SDK, MSVC, backend, zařízení, použité vzorkování a buffer,
výsledky, případné chybové hlášky a snímek okna. Není třeba kytara ani M-Track Solo;
tohle je interní přejímka, nikoli bratrův test.

## Opakovatelné kontroly bez Windows

Testovací backend nevyžaduje JUCE ani skutečné zvukové zařízení:

```sh
cmake -S windows -B /tmp/rocker-native-test -DROCKER_WITH_JUCE=OFF -DCMAKE_BUILD_TYPE=Debug
cmake --build /tmp/rocker-native-test
ctest --test-dir /tmp/rocker-native-test --output-on-failure
cd windows
dotnet run --project Rocker.Interop.Checks -- /tmp/rocker-native-test/librocker_audio_test.so
dotnet run --project Rocker.Interop.Checks -- /tmp/rocker-native-test/librocker_audio_test_incompatible.so --mismatched-abi
```

Kontroluje se skutečný přechod C# → nativní knihovna, rozložení struktur,
UTF-8 chyby, chyba zařízení a obnova, opakovaný start/stop, zneplatnění handle,
souběžné řídicí požadavky a syntetický tón. Nejde o test nativního Windows okna,
ASIO, skutečné odezvy ani stability konkrétního hardwaru.

Pro kontrolu samotného JUCE řídicího vlákna na Linuxu lze navíc sestavit hlavní
knihovnu s vypnutými Linux audio backendy (`JUCE_ALSA=0`, `JUCE_JACK=0`) a spustit
kontrolu `--no-device`. Ta očekává nepřítomnost zařízení, testuje chybu a opakované
uvolnění JUCE. Takový build se nesmí použít jako důkaz fungujícího zvuku ani
jako distribuční knihovna. Výsledky tohoto prostředí jsou v
[protokolu W03](../docs/windows/w03-validation.json).

## Hranice a licence

Načtenou knihovnu vlastní proces; nativní handle vlastní pouze jeho C# wrapper.
Parametry a řetězce přes C ABI se kopírují. Přímí C/C++ volající nesmějí souběžně
zničit handle a pokračovat v jeho používání; wrapper to ošetřuje uzamčením a
SafeHandle. Události z audio callbacku se čtou jako stavové snímky, nepředávají
se jako spravované callbacky.

Při vydání bratrovi se zdroje nabídnou zvlášť, nikoli jako součást instalátoru.
Průběžný prototyp nabízí spustitelný ZIP a samostatný archiv odpovídajících
zdrojů; nejde o finální instalátor ani veřejné vydání.
[Licenční poznámky](THIRD_PARTY_NOTICES.md) nenahrazují finální distribuční kontrolu.
