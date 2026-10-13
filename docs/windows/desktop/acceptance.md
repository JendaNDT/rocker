# Desktopová přejímka — význam důkazů

Referenční kompletní Windows běh
[38477297083](https://github.com/JendaNDT/rocker/actions/runs/38477297083),
revize `69f6c8896593c48b6c43d011bf442f4c1d737a6b`, uspěl včetně instalátoru.
[Galerie](gallery.html) i [candidate-validation.json](candidate-validation.json) uvádějí stejnou revizi a úspěšný běh.

WinUI diagnostika používá skutečná okna, SQLite a skutečné připravené
syntetické soubory. 22 native sad používá označené testovací signály/backendy.
Produkční balík testovací audio backend neobsahuje. Sloupec „hranice“ uvádí,
co z automatické kontroly nelze dovozovat.

| Scénář plánu | Automatický důkaz | Hranice / závěrečná přejímka |
|---|---|---|
| 1. Přepínání lekce/not/skladby | desktop-runtime, desktop-interactions: zachování obrázku, graderu, relací a native ticketu | Skutečná hra na kartě |
| 2. Odpojit / připojit dokument | actual XamlRoot transfer, image/selection identity, dvě nezávislá project zoom nastavení | Fyzické přetažení a další monitor |
| 3. Jedna instance nástroje | runtime singleton/hide/reopen; registry používá stejný klíč pro všechny čtyři nástroje | Výslovné opakované vyvolání metronomu při hraní |
| 4. Přesun při metronomu | sample_clock, metronome_dsp, realtime_contract; přesun sám nesahá na audio | Poslech a native epoch během fyzického přesunu |
| 5. Live a metronom | metronome-visualization: 20 scénářů, native clock, sdílený host, oba pohledy | Skutečný count-in z karty |
| 6. Monitoring a mix | input_routing, amplifier_dsp, audio_contract; viditelná možnost zapnout poslech | Bratrův skutečný signál a poslech |
| 7. Jiný zdroj bez monitoringu | common_transport, input_routing; samostatné ovládání přehrávače | Sluchátka a mix na M-Track Solo |
| 8. Záznam při skrytí Live | recording_writer/contract; X nástroje pouze skrývá UI | Živý záznam při zavření a znovuotevření Live |
| 9. Save / cancel / failure při ukončení | 39 application-services kontrol, dvoufázová rozhodnutí, recording recovery | Skutečný aktivní take a chyba fyzického úložiště |
| 10. Dva projekty | skutečné Ctrl+S-equivalent command/undo/redo, repo readback, oddělené historie | Současná práce s reálným overdubem |
| 11. Druhý zdroj | native ticket se při čtení druhého waveformu nezmění; capture konflikt zachová vlastníka | Přepínání při fyzickém poslechu |
| 12. Trénink a čas | jeden grader/PracticeSession, practice-profile, note-gate/rhythm/palm referenční stavy | Reálné měření kytary |
| 13. Noty / video přes HWND | produkční score image/XamlRoot transfer; D03 experiment lokálního WebView2 bridge | Skutečný YouTube/ASIO, pointer drag |
| 14. AI / export a fokus | scoped callbacky, AI HTTP mock/cancel/timeout, operation leases a model/context snapshot | Online poskytovatel a velký uživatelský export |
| 15. Vlastník dialogu | skutečný detached HWND/XamlRoot a otevřený ContentDialog | Ruční systémový picker, fokus při cancel |
| 16. Obnova oken | skutečná skupina záložek a zoom po obnově; work-area fitting kontrakty | Odpojení fyzického monitoru |
| 17. Chybný layout | 10 layout kontraktů včetně budoucího schématu, cancel a neplatných preferencí | Uživatelská recovery zkušenost |
| 18. Sleep / hotplug | skutečná WM_POWERBROADCAST simulace, jedno zastavení; native fault kontrakty | Fyzický sleep a ovladač |
| 19. Hledání / soubor / instance | workspace search/legacy fixtures, šest Windows file identity kontrol, Unicode project activation, instance-handoff | Drag souboru z Průzkumníka |
| 20. Update / restore | backup/update/incompatible-profile testy, globální bariéra, instalátor zachová asociaci při selhání | Uživatelský GitHub účet a skutečný upgrade |
| 21. Opakované cykly | 12 actual open/detach/attach/close cyklů; počty oken, dokumentů a hostů se vrátí | Dlouhodobá paměťová stopa a GPU ovladač |
| 22. Hlavní X | shutdown guard kontrakty, běžné zavření všech oken ve třech profilech a po instalaci | Ruční cancel více dialogů při živé práci |

Všech 32 funkcí musí mít čerstvý skutečný snímek ve tmavém i světlém
Vintage, odpovídající zdrojovou revizi a úspěšné předchozí funkční kontrakty.
`verify-windows-desktop-current.mjs` ověřuje seznam ID, PNG, počty
skutečných assertions a shodu revize; `verify-candidate.ps1` navíc kontroluje
native/datové/UI reporty a instalační cyklus. Finální gallery export bez
úspěšného `candidate-validation.json` odmítne vzniknout.

Ruční sloupec zůstává přiznaně neprovedený a pokračuje v
[přejímce s bratrem](../w43/brother-test.md). Nemění se na „passed“ tím,
že lze vytvořit nebo vyfotit ovládací prvek.
