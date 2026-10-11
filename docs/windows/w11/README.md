# W11 — Desktopová záloha a obnova

Záloha `.rockerbackup` je ZIP s verzovaným `manifest.json`, validovanými
`documents.json` a explicitně odkazovanými nahrávkami a projekty. Každý soubor
má velikost a SHA-256; chybějící nebo změněné médium export zastaví. Volby
zahrnutí nahrávek/projektů určují i typy dokumentů, nikoli jen kopírované bajty.
Tajné úložiště, cache, dočasné soubory, neodkazovaná média a diagnostika jsou
mimo whitelist. Zašifrovaný AI klíč ani jiná část `secrets/` se nepřibalují.

Náhled ověří celý archiv před změnou profilu: verzi, cesty, duplicity bez ohledu
na velikost písmen, počet a velikost souborů, rozbalené bajty a součty. Metadata
nahrávky a projektové stopy musejí odpovídat skutečným souborům. Neznámé přílohy
se odmítnou. Limity: 2 GiB celkem, 1 GiB na soubor, 20 000 souborů, 10 000
záznamů; JSON manifestu 4 MiB a dokumentů 32 MiB. Čtení manifestu je omezené i
podle skutečných dekomprimovaných bajtů. Kontroluje se prostor na cílovém disku.

Nativní dialog ukáže počty, velikost a konflikty. Výchozí sloučení ponechá
existující ID; nahrazení vyčistí pouze zahrnuté typy, vyloučená média/projekty
zachová. Archiv změněný po náhledu se odmítne. Obnova vytváří celý nový datový
profil mimo aktuální složky. Původní databáze a média se odloží do
`backups/restore-{id}`. Krátkou výměnu adresářů chrání záznam v `settings/`;
start aplikace před otevřením DB vrátí nedokončenou výměnu. Po potvrzeném
commit zůstanou nové soubory a vratná kopie. Nejde o jeden atomický souborový
rename tří složek, a netvrdíme zaručenou odolnost proti výpadku napájení.

Tlačítko „Vrátit poslední obnovu“ převezme poslední vratnou kopii stejnou
validovanou cestou. Původní AI klíč a cache zůstávají samostatné. UI během
přenosu brání zavření a před ním flushuje autosave. Selhání uprostřed výměny
vyvolá recovery; záznam na disku je i pro další start. Vratné kopie se automaticky
nemažou. Při souběžném zásahu cizího procesu nelze zaručit dostupnost souborů.

Automatické kontroly: plný export → čistý profil → import, zvuk a projekty,
konflikty, sloučení a nahrazení, skutečný návrat předchozího nastavení,
částečná záloha, zrušení, invalidní cesty/checksum/verze/duplicity/přílohy,
simulace přerušené výměny databázové složky a opakované startup recovery.
Windows ověření skutečných nativních formulářů a systémových pickerů je
součástí pozdější W37/W42 přejímky; CI nevydává ruční klikání za provedené.
