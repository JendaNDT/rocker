# Přechod z RC6 na desktop

Hudební databáze, nahrávky, projekty, značky, sestavy a jejich schémata se
nemění. Desktop přidává `settings/desktop-layout.json` verze 1. Původní
`settings/window.json` zůstává kompatibilní. Nový soubor neobsahuje klíče,
zvuková data ani povel ke spuštění přehrávání.

Před instalací zavři všechna okna Rockeru běžným ukončením. Ulož nahrávku,
looper a rozpracované projekty. Instalátor vytvoří úplnou zálohu profilu
včetně raw SQLite kopie a ponechá předchozí binární složku. Neinstaluje se
SDK; odpovídající zdroje jsou samostatný volitelný balík.

## Rozložení a hudební záloha

Hudbu zálohuj přes Nastavení → Zálohy a soubory do `.rockerbackup`, včetně
nahrávek a projektů. Cesty k externím skladbám nejsou kopií jejich zvuku;
původní soubory zálohuj také.

Rozložení oken se předává samostatně přes **Uložit rozložení oken** jako
`.rockerlayout`. Je to výslovná volba; soubor obsahuje i místní cesty.
**Načíst rozložení oken** nejprve ověří schéma a limity, požádá o vyřešení
rozpracované práce a potom otevře dokumenty. Zvuk a monitoring se nespustí.
Chybné nebo budoucí rozložení nepoškodí hudební databázi. Chybějící dokument
má srozumitelné hlášení; okno z odpojeného monitoru se vrátí do work area.

## Bezpečný návrat

Předchozí RC6 nezná desktopový layout a ignoruje jej. Neotevírej současně
obě verze nad jedním profilem. Pokud se vracíš po nových změnách, nejprve
vytvoř novou úplnou zálohu; obnova předinstalační kopie by tyto změny
odložila. Vrácení obnovy a cold recovery uchovávají celý původní profil.

Instalační automatizace ověřuje zachování dat, předchozí binární složky,
asociace a zástupce při neúspěšném update, přesnou opravu DLL a zachování
dat po odinstalaci. Syntetické version-transition štítky se výslovně
rozlišují od spuštění historických binárek. Skutečné použití RC6 a nové
verze na uživatelském počítači patří do závěrečné ruční přejímky.
