# Předání sestavené Windows verze

Uživatel dostává `Rocker-Windows-x64.zip`, který rozbalí a spustí přes
`Rocker.Probe.exe`. Zdrojové soubory nevyžadovat pro spuštění; nabízejí se zvlášť
podle W02. Balíky i zdroje zůstávají v soukromém repozitáři, ke stažení je potřeba
přihlášení účtem s přístupem.

## Postup pro další verzi

1. Commitnout změny Windows části do větve `windows-native-prototype`. Workflow
   `Windows prototype` sestaví C++ a WinUI, spustí nativní/C# testy a kontrolu okna.
2. Po úspěchu stáhnout artefakt `Rocker-Windows-downloads` a ověřit SHA-256,
   obsah ZIPů, x64 EXE/DLL a shodu commitu v `build-info.json` se zdroji.
   Zaznamenat i dosud neprovedené ruční kontroly. Prototyp nepředávat po neúspěšném testu.
3. Aktualizovat `current.json`: ID úspěšného běhu, sestavený commit, nový jedinečný
   tag a název vydání. Přepsat `notes.md` podle skutečného výsledku. Commitnout a pushnout
   tyto dva soubory; spustí se `Publish verified Windows prototype`.
4. Publikační workflow zkontroluje úspěch vybraného Windows běhu, příslušnost
   k tomuto repozitáři/větvi/workflow, revize zdrojů, SHA-256 a obsah ZIPu.
   Použije standardní krátkodobé oprávnění GitHub Actions k vytvoření předběžného
   vydání a nahrání již ověřených souborů. Nové uživatelské tokeny nejsou potřeba.
5. Po úspěchu ověřit vydání přes API, součty příloh a stáhnout publikovaný
   spustitelný ZIP. Teprve tento odkaz předat Jendovi. Uložit výsledek do stavu projektu.

Publikace nic nesestavuje znovu a původní přílohy nepřepisuje. Pro další balík
použít nový tag. Vydání odkazuje na skutečně sestavený commit, který může být starší
než následná dokumentace výsledků. Zdrojový ZIP obsahuje celý Rocker a připnuté JUCE;
archiv vytvořený Gitem na Windows může mít textové soubory převedené na CRLF.
Při porovnávání obsahu zohlednit jen tuto změnu konců řádků; binární soubory porovnávat přesně.

První úspěšné vydání:
[W03 z 11. 10. 2026](https://github.com/JendaNDT/rocker/releases/tag/windows-w03-preview-20261011).
Podklady a meze ověření: [protokol](../w03-windows-validation.json).
