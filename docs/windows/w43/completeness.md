# Matice úplnosti Windows kandidáta

Všechny pohledy mají skutečnou nativní implementaci. Automatické sestavení/konstrukce, persistence a doménové scénáře jsou uvedené v protokolech W04–W42 a společné přejímce `windows-validation.json`. Snímky a UI Automation nejsou náhradou skutečného hraní ani online služeb.

| Pohled | Funkce a implementace | Ruční přejímka |
|---|---|---|
| 01 — Přehled | [MainWindow.Overview.cs](../../../windows/Rocker.Probe/MainWindow.Overview.cs) | Odložená W44 |
| 02 — Live | [MainWindow.Live.cs](../../../windows/Rocker.Probe/MainWindow.Live.cs) | Odložená W44 |
| 03 — Ladička | [MainWindow.Tuner.cs](../../../windows/Rocker.Probe/MainWindow.Tuner.cs) | Odložená W44 |
| 04 — Metronom | [MainWindow.Metronome.cs](../../../windows/Rocker.Probe/MainWindow.Metronome.cs) | Odložená W44 |
| 05 — Výběr lekce | [MainWindow.Lessons.cs](../../../windows/Rocker.Probe/MainWindow.Lessons.cs) | Odložená W44 |
| 06 — Průběh lekce | [MainWindow.Lessons.cs](../../../windows/Rocker.Probe/MainWindow.Lessons.cs) | Odložená W44 |
| 07 — Vlastní skladba | [MainWindow.Songs.cs](../../../windows/Rocker.Probe/MainWindow.Songs.cs) | Odložená W44 |
| 08 — YouTube cvičení | [MainWindow.VideoPractice.cs](../../../windows/Rocker.Probe/MainWindow.VideoPractice.cs) | Odložená W44 |
| 09 — Přesný trénink | [MainWindow.Surgical.cs](../../../windows/Rocker.Probe/MainWindow.Surgical.cs) | Odložená W44 |
| 10 — Power chordy | [MainWindow.Rhythm.cs](../../../windows/Rocker.Probe/MainWindow.Rhythm.cs) | Odložená W44 |
| 11 — Gallop | [MainWindow.Rhythm.cs](../../../windows/Rocker.Probe/MainWindow.Rhythm.cs) | Odložená W44 |
| 12 — Palm mute | [MainWindow.PalmMute.cs](../../../windows/Rocker.Probe/MainWindow.PalmMute.cs) | Odložená W44 |
| 13 — Aparát | [MainWindow.Amp.cs](../../../windows/Rocker.Probe/MainWindow.Amp.cs) | Odložená W44 |
| 14 — Nahrávky | [MainWindow.Recordings.cs](../../../windows/Rocker.Probe/MainWindow.Recordings.cs) | Odložená W44 |
| 15 — Studio | [MainWindow.Studio.cs](../../../windows/Rocker.Probe/MainWindow.Studio.cs) | Odložená W44 |
| 16 — Looper | [MainWindow.Looper.cs](../../../windows/Rocker.Probe/MainWindow.Looper.cs) | Odložená W44 |
| 17 — Akordy | [MainWindow.Library.cs](../../../windows/Rocker.Probe/MainWindow.Library.cs) | Odložená W44 |
| 18 — Stupnice | [MainWindow.Library.cs](../../../windows/Rocker.Probe/MainWindow.Library.cs) | Odložená W44 |
| 19 — Riffy | [MainWindow.Riffs.cs](../../../windows/Rocker.Probe/MainWindow.Riffs.cs) | Odložená W44 |
| 20 — Noty a tabulatury | [MainWindow.Notation.cs](../../../windows/Rocker.Probe/MainWindow.Notation.cs) | Odložená W44 |
| 21 — Hmatník | [MainWindow.Games.cs](../../../windows/Rocker.Probe/MainWindow.Games.cs) | Odložená W44 |
| 22 — Sluch | [MainWindow.Games.cs](../../../windows/Rocker.Probe/MainWindow.Games.cs) | Odložená W44 |
| 23 — AI — rozhovor | [MainWindow.AiChat.cs](../../../windows/Rocker.Probe/MainWindow.AiChat.cs) | Odložená W44 |
| 24 — AI — plán | [MainWindow.AiPlan.cs](../../../windows/Rocker.Probe/MainWindow.AiPlan.cs) | Odložená W44 |
| 25 — AI — zpětná vazba | [MainWindow.AiFeedback.cs](../../../windows/Rocker.Probe/MainWindow.AiFeedback.cs) | Odložená W44 |
| 26 — Moje kytara | [MainWindow.Instrument.cs](../../../windows/Rocker.Probe/MainWindow.Instrument.cs) | Odložená W44 |
| 27 — Pokrok | [MainWindow.Progress.cs](../../../windows/Rocker.Probe/MainWindow.Progress.cs) | Odložená W44 |
| 28 — Nastavení | [MainWindow.Settings.cs](../../../windows/Rocker.Probe/MainWindow.Settings.cs) | Odložená W44 |
| 29 — Zvuková zařízení | [MainWindow.Devices.cs](../../../windows/Rocker.Probe/MainWindow.Devices.cs) | Odložená W44 |
| 30 — Zálohy a soubory | [MainWindow.Backups.cs](../../../windows/Rocker.Probe/MainWindow.Backups.cs) | Odložená W44 |
| 31 — První spuštění | [MainWindow.FirstRun.cs](../../../windows/Rocker.Probe/MainWindow.FirstRun.cs) | Odložená W44 |
| 32 — Export nahrávky | [MainWindow.Recordings.cs](../../../windows/Rocker.Probe/MainWindow.Recordings.cs) | Odložená W44 |

## Společné kontrakty

- Obsah: 18 lekcí, 26 akordů, 6 stupnic, 14 riffů; dva původní cizí riffy nahrazeny vlastními se stabilním převodem ID. Doména porovnána s původními referenčními daty.
- Zvuk: jeden host, asynchronní řízení/zrušení, vybraný mono kanál, stereo výstup, oddělené sběrnice, efekty/metronom/ladička/analýza/záznam/Looper/Studio a společný hudební čas. Buffer není celková latence.
- Soubory: systémové dialogy, podporované přetažení, kontextové příkazy, historie, jedna instance a instalovaná projektová asociace.
- Data: všech 11 původních webových úložišť, původní nahrávky v pěti formátech a markery; náhled/deduplikace/report, nikoli přímé čtení cizího localStorage. SQLite/DPAPI, autosave, úplné zálohy a kompatibilní obnova.
- Windows: adaptivní nativní Vintage prvky, systémový vzhled/kontrastové tokeny, klávesové příkazy, instalace pro uživatele, samostatné zdroje a soupis komponent.
- Odolnost: zrušení načítání, nesoulad ABI/formátu, neplatné importy, syntetické uspání a přerušení výměny profilu. Fyzické chyby zařízení/disku jsou přejímka, nikoli tvrzený automatický výsledek.

## Vědomé hranice

Původní nedokončený songbook/sdílení riffů, VST, separace nástrojů, AR, cloudová synchronizace, mobilní port a nové polyfonní/rytmické hodnocení nejsou dnešní funkce a nejsou součástí tohoto přepisu. PWA instalace/service worker, mobilní vibrace a tři webová témata jsou nahrazené desktopovým chováním a vybraným Vintage. Gemini pro osobní použití je blokovaný; OpenAI je volitelná náhrada s odděleným klíčem. Omezení kandidáta jsou v [README](README.md).
