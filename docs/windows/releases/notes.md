První část W04 přidává nativní notový náhled k dosavadnímu testovacímu tónu. Obsahuje vlastní ukázky MusicXML a Guitar Pro 7, otevření souboru systémovým dialogem, výběr taktu kliknutím a uložení MIDI. Noty se vykreslují přes .NET AlphaTab / nativní AlphaSkia bez webového rozhraní.

Stáhni **Rocker-Windows-x64.zip**, rozbal celý archiv a spusť **Rocker.Probe.exe**. Vyžaduje Windows 11 x64 od 24H2; vývojové nástroje nejsou potřeba. Zdrojový ZIP je samostatný a pro spuštění jej nepotřebuješ.

Windows runner úspěšně sestavil aplikaci a JUCE DLL, provedl původní ABI/životnostní kontroly a všech 22 notových ověření. Publikované WinUI okno načetlo a dekódovalo notový obrázek a normálně se zavřelo. Balík má ověřený SHA-256, ZIP integritu a x64 architekturu; čtyři zdrojové archivy odpovídají připnutým revizím.

Prototyp ukazuje první stopu a nejvýše 12 taktů; import omezuje na 4 MB, 16 stop a 128 taktů. Důkaz importu je pro GP7 a nekomprimované MusicXML; další formáty nabízené parserem zatím nemají vlastní přejímku. Ruční klikání a systémové dialogy čekají na ověření Jendou.

**W04 není celý hotový.** Přehrávání not přes společné C++ jádro, dekódování nahrávek, zpomalování a YouTube budou navazovat. Kompletní aplikace a finální Vintage design zatím nejsou implementované. Automatický test nepřehrával zvuk; fyzická zvukovka, ASIO a latence tímto vydáním nejsou ověřené.

Balík je nepodepsaný. Zapnutý Smart App Control může blokovat rocker_audio.dll i libalphaskia.dll; toto vydání nemá ověření podepsaných knihoven se zapnutou ochranou. Odpovídající zdroje Rockeru, JUCE, AlphaTab a AlphaSkia jsou v samostatném **Rocker-Windows-sources.zip**; licenční texty a oznámení jsou přibalené k aplikaci.
