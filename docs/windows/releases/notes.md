První sestavený technický prototyp Rockeru pro **Windows 11 x64 od 24H2**.

1. Stáhni **Rocker-Windows-x64.zip** níže.
2. Použij **Extrahovat vše** a spusť **Rocker.Probe.exe** z rozbalené složky. Ostatní soubory ponech vedle něj.
3. Začni s nízkou hlasitostí. Tlačítko **Spustit tón** zapne testovací tón; **Zastavit** jej vypne.

Vývojové prostředí ani sestavování nejsou potřeba; .NET a Windows App SDK jsou přibalené. Jde o technický prototyp s testovacím tónem, nikoli kompletní aplikaci s Vintage obrazovkami.

Na Windows runneru prošlo sestavení C++/WinUI, nativní test, skutečné C# volání do testovací DLL, odmítnutí nekompatibilní DLL a automatické otevření/zavření publikovaného okna (návratový kód 0). [Výsledek sestavení](https://github.com/JendaNDT/rocker/actions/runs/38129738997). Poslech, ovládání a zvuková zařízení ještě vyžadují ruční přejímku; celková latence není změřená.

**Zdroje jsou samostatně:** Rocker-Windows-sources.zip obsahuje odpovídající zdroje Rockeru a úplné zdroje připnutého JUCE včetně návodu sestavení. Pro spuštění aplikace tento archiv nepotřebuješ. Licence a oznámení jsou ve spustitelném balíku. SHA256SUMS.txt obsahuje kontrolní součty obou ZIPů.

Sestavený commit: `79fb06bcb535fd457e6620da5ea5265711ef0001`.
