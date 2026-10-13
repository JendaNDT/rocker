# Vizualizace metronomu

Náprava zadání Jendy z 13. 10. 2026: vizualizace musí být viditelná u hlavního ovládání, také v Live a Jen hraju.

- Nativní přístroj s opravdovou otočnou páčkou kyvadla, kovovým závažím odpovídajícím aktuálnímu BPM, krémovou stupnicí a očíslovanými světly dob. Není to původní pohybující se ProgressBar.
- Samostatný metronom ukazuje vizualizaci a její přepínač nahoře vedle tempa/presetu. V úzkém okně má vizualizace pořadí před editory. Doplňkové parametry zůstávají v rozbalovací sekci Rytmus, zvuky a trénink.
- Live i F11 / Jen hraju obsahují kompaktní přístroj nahoře, rychlou přesnou volbu BPM a nativní přepínač Doby / Kyvadlo. Světla dob zůstávají i pod kyvadlem. Bez uložené volby je výchozí kyvadlo; původní explicitní Doby se zachovají.
- Otáčení čte společný nativní zvukový sample clock; absolutní parita dob zachovává návaznost i při lichém taktu a odpočtu. Žádný druhý audio host, vlastní animační časovač ani generování kliků na UI vlákně.
- Zastavení a neplatný nebo nedostupný stav vrátí páčku do středu. Odpočet, tiché takty a tlumené doby mají jasné texty. Omezení animací ve Windows ponechá nehybné kyvadlo a aktualizovaná čísla dob; vysoký kontrast používá systémové barvy.
- Volba vizualizace se ukládá do existujícího dokumentu metronomu a synchronizuje se mezi pohledy. Přepnutí vizualizace neodesílá nový zvukový příkaz a nespouští přehrávání.

Kontroly zahrnují skutečnou geometrii plného, kompaktního, F11 a úzkého okna, nativní přepínač, opětovné otevření databáze, hranice dob, odpočet/ticho, návaznost lichého taktu a determinismus stejného snímku hodin. Galerie použije skutečné Windows snímky; animovaná ukázka bude z výslovně označených kontrolních snímků hodin a nikdy se nepřipojí k živému zvuku. Skutečný poslech a fyzická odezva M-Track Solo zůstávají závěrečnou přejímkou.

Stav: implementováno; čeká na aktuální Windows build a ověření instalátoru.
