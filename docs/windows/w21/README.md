# W21 — Skladby a úseky

WinUI pracovní pohled používá systémový výběr/přetažení souboru, zvukovou
časovou osu z 1024 peaků, klávesnicový seek ve společném přehrávači, A/B
značky, pojmenované úseky s potvrzeným odstraněním, rychlost 0,25–2×,
předtakt a připravené postupné zrychlování na hranicích opakování.
SQLite uchová zdrojovou cestu, pozici v původní skladbě, sekce a nastavení.
Chybějící zdroj lze dohledat; záloha obsahuje metadata připojené skladby,
nikoli její externí soubor. Média nahrávek mají vlastní řízené úložiště W22.

JUCE čte WAV/MP3/Ogg/Opus postupně. PCM a zpomalení se zapisují po blocích
do dočasné cache, maximálně 8 GiB / UINT32_MAX frames; zakódovaný soubor
má limit 1 GiB. Signalsmith outputSeek/process/flush zachovají přesný počet
výstupních vzorků, před/za běžným čtením neběží ve zvukovém callbacku.
Cache připraví read-ahead worker s 64 stránkami po 4096 stereo float frames,
2 MiB PCM. Callback nevstupuje do souboru ani nečeká. Při chybějící stránce
hraje ticho a neposouvá skladbu; monitoring a společné hodiny pokračují.
UI odděluje buffering od trvalé chyby cache. Legacy WebM a Windows MP4/AAC
zatím používají dřívější výslovně omezenou 64 MiB offline cestu; rozšíření
jejich readerů a obnova cache po pádu naváží před kompletním kandidátem.

Příkazy seek, smyčka, pauza a nový zdroj používají bounded SPSC queue.
Monitor/metronom/analýza ani epocha zařízení se kvůli nim nezastavují.
Novou PCM/cache vlastní coordinator; callback předá jen pointer a na konci
bloku publikuje current/pending/consumed generace. Coordinator uvolní staré
zdroje až z konzistentního lease snapshotu. Nikdy se neuvolňuje shared_ptr,
nezavírá soubor ani nejoinuje worker ve zvukovém callbacku. Starší dekódovací
úlohy jsou zrušené čítačem generace mezi bloky; po zrušení nepřepíšou novou
skladbu ani její chybový stav. Native ABI 11 chrání celé nové propojení.

Pauza skladby ponechá ostatní zdroje. Escape i zavření zastaví vše. Předtakt
se odpočítá ze vzorků společného zařízení a začne na nejbližší době;
metronom i backing používají stejné BPM. Další rychlost rampy se připraví
na workeru a předá až na A/B hranici; source-time pozice a endpointy se
přepočítají z původního tempa bez obnovy společných hodin.

Offline kontroly čtou syntetickou 40min WAV (76,8 MB decoded mono PCM),
vzdálený seek na poslední sample, všech šest rychlostí A4, waveform a
samostatný monitoring při pauze/seek. Windows formulář a přechody se dále
ověřují; skutečná karta, poslech, file pickery a drag/drop jsou neprovedené
scénáře závěrečné přejímky.
