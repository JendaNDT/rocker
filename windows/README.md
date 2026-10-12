# Rocker pro Windows

Nativní WinUI 3 aplikace ve Vintage designu a oddělené C++20/JUCE zvukové jádro. Implementace zahrnuje 32 pohledů a rozsah W04–W43. WinUI/C# není webový obal; WebView2 slouží pouze pro oficiální YouTube přehrávač. Vyžaduje Windows 11 x64 od 24H2.

## Spuštění

Uživatel stáhne už sestavený `Rocker-Windows-Setup-0.2.0-x64.exe` z odpovídajícího soukromého GitHub vydání, nainstaluje jej a spustí **Rocker** ze Start. Neinstaluje vývojové nástroje. Přenosná alternativa je `Rocker-Windows-x64.zip` → Extrahovat vše → `Rocker.Probe.exe`. GitHub Code → Download ZIP je pouze projekt. Zdroje jsou samostatný volitelný `Rocker-Windows-sources.zip`.

[Návod](../docs/windows/w43/user-guide.md), [úplnost a omezení](../docs/windows/w43/README.md), [roadmapa](../WINDOWS_ROADMAP.md) a [licenční oznámení](THIRD_PARTY_NOTICES.md).

## Automatické kontroly a balíky

Windows workflow sestavuje produkční i testovací C++ knihovnu, skutečné C# P/Invoke, doménu/úložiště/noty/média, Chromium migrační export a publikovanou WinUI aplikaci. Otevře všech 32 pracovních pohledů a zachytí 35 snímků, ověří UI Automation a syntetické zotavení, pak připraví balík se soupisem komponent. Instalátor se kontroluje instalací, přechodem verzí, zálohou, opravou DLL, aktivací Unicode projektu a odinstalací s ponecháním dat.

`verify-candidate.ps1` vyžaduje úspěšné protokoly, skutečný instalátor, binární ZIP, samostatné odpovídající zdroje a součty. Vydání se váže na konkrétní úspěšnou revizi/běh. Vývojové artefakty nejsou automaticky uživatelským vydáním. Zdrojové archivy JUCE, Signalsmith, libwebm, AlphaTab a AlphaSkia jsou zvlášť; úplná oznámení zůstávají v aplikaci.

Zvukový callback nevolá C#, UI, síť ani disk a nealokuje; řízení má fronty a jedno vlastnictví. Analýza, dekódování, příprava a záznam mají pracovní cesty. Skutečný ovladač, ASIO/WASAPI souběh a celková odezva se ověří na M-Track Solo ve W44. Automatické testy a zrychlený hodinový render nejsou totožné s reálným hraním.

## Sestavení pro vývojáře

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

Vznikne adresář `out/probe` s sestavenou aplikací. Skript provede
nativní testy a skutečné P/Invoke kontroly proti testovacímu zařízení; ty samy
neověřují zvukový ovladač. Sestavení hlavního DLL navíc kompiluje skutečný JUCE
backend, včetně povoleného ASIO. CMake používá Ninja a již aktivované MSVC;
nepotřebuje generátor Visual Studia novější než zvolený CMake.


## Kontroly bez Windows

```sh
cmake -S windows -B /tmp/rocker-native-test -DROCKER_WITH_JUCE=OFF -DROCKER_WITH_MEDIA=OFF -DCMAKE_BUILD_TYPE=Release
cmake --build /tmp/rocker-native-test
ctest --test-dir /tmp/rocker-native-test --output-on-failure
cd windows
dotnet run --project Rocker.Domain.Checks -- ../docs/windows/baseline/fixtures/domain-cases.json content/catalog.json
dotnet run --project Rocker.Infrastructure.Checks
dotnet run --project Rocker.Interop.Checks -- /tmp/rocker-native-test/librocker_audio_test.so
```

Testovací knihovna se nikdy nepřibaluje do aplikace. Linux kontroly neověřují Windows okno ani zvukový ovladač. Přímí C/C++ volající musejí dodržet vlastnictví handle; C# SafeHandle a řízení hostu brání souběhu zničení s voláním. Audio stav se čte jako kopírovaný snímek, nikoli spravovanými callbacky.
