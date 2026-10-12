# W20 — Diagnostika zvuku a opakovatelné zatížení

Callback změří monotónní čas před/po vlastní práci grafu a kopírování dat.
Publikuje omezené atomické čítače; řídicí worker je mimo callback kopíruje
do cache pro UI: počet bloků/vzorků, poslední,
maximální a průměrný čas, rozsah velikostí bloků a počet překročených časových
rozpočtů. Rozpočet je skutečný počet vzorků / sample rate, nikoli UI timer.
Nezahrnuje práci před/po vstupu do callbacku u výrobce a naměřené překročení
není potvrzený ASIO/WASAPI underrun. Výpočet nepotřebuje logování, paměťové
alokace ani čekání; realtime audit aktivního aparátu/metronomu/reference dál
ověřuje callback bez new/delete.

Analytická fronta má čítač ztracených bloků a nejvyšší obsazenost pozorovanou
producentem. Neobnovují se při každém startu; společně s lifecycle čítači mají
scope host lifetime. Callback čítače se obnoví po joined prepare v nové epoše.
Host eviduje skutečně spuštěná zařízení včetně zrušených startů, stop requests
(včetně příprav/rekonfigurací), chyby zařízení, odmítnuté konfigurace a chyby
dekódování. Poslední důvod chyby: 1 neočekávané zastavení, 2 driver error,
3 neočekávaný format restart, 4 neplatné vzorkování, 5 start failure; 0 neznámý.

JUCE `AudioIODevice.getXRunCount()` se načte výslovně z řídicího workeru,
ne z audio/UI vlákna. -1 se exportuje jako null / nedostupný, nikoli 0.
Stav zastaveného zařízení nedává nový vendor counter. U callback chyb se
výstup ihned vynuluje; neočekávaný druhý AboutToStart nealokuje/neresetuje
graf a nepřevezme nový formát. Karta se sama neobnovuje ani nepřepíná.
Fyzické odpojení a chování konkrétního driveru stále vyžadují závěrečnou kartu.

Nastavení má nativní sekci Diagnostika. Export nejdřív ukáže JSON k prohlédnutí,
pak použije systémový FileSavePicker. Pevné DTO obsahuje build commit/verze,
OS/runtime/ABI, zařízení a reportované latence, čítače a analytické stáří/okno.
Nezískává nahrávky, waveform, uživatelské cesty, chat, profil ani API klíče.
Ovladačová package verze je unknown, pokud ji rozhraní neposkytuje. Žádné
buffer/FIR číslo se nepřevádí na měření celé kytarové roundtrip odezvy.

App-build manifest vznikne ze skutečného Git HEAD po Windows publish; chybějící
nebo poškozený volitelný manifest nezabrání exportu, source commit je null.
Callback epocha může po rekonfiguraci patřit předchozímu zařízení; device
block znamená aktuální/poslední ověřenou volbu a má zvláštní sample rate.

Linux 11/11 CTest včetně dlouhého simulovaného grafu s 5 755 000 vzorky,
maximálním aparátem, oběma vrstvami metronomu, referencí a 127/1024 sample
bloky. Fronta se odčítá, nečeká a neztrácí bloky; všechna data zůstanou konečná.
Test reportuje skutečný wall time/over-budget, nemá přenosný práh CPU ani
nevydává syntetické tempo za fyzický hardware nebo měření ovladače.
160b diagnostika byla zavedena v ABI 10 (současné společné ABI je 15); C# PInvoke kontroluje skutečné callback čítače, unknown -1 i
výslovný simulovaný vendor count 7. Deset report schema testů chrání pravdivost
latence, oddělené čítače a pevný export bez soukromých dat. Windows sestavení a formulář/report [prošly](windows-validation.json); export picker, zatížení fyzické karty/UI při hraní a skutečné
underruny jsou neprovedené do přejímky.
