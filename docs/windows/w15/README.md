# W15 — Mono vstup, monitoring a měřené sběrnice

Live je skutečný C++ vstup ve společném hostu. AudioIODevice otevře jen
vybraný fyzický kanál; callback dostává tento mono kanál na indexu 0.
Zvolenou masku po otevření ověřuje, takže neočekávané změny ovladače
skončí chybou. Nezvolený druhý vstup se nikdy nesčítá do signálu kytary.

Spuštění Live a zapnutí monitoringu jsou oddělené příkazy. Monitoring má
plynulou 10ms změnu úrovně; výchozí hlasitost 15 %. Stop zneplatní callback
bránu a vypne monitoring. Při přepnutí obrazovky se host nemění. Mono
kytara zní v obou vybraných výstupech; přehrávač zůstává další zdroj mixu.
Suchá sběrnice se bere před monitoringem; zpracovaná sběrnice je nyní
výslovný unity bypass, plný aparát navazuje ve W17.

Měřáky ukazují skutečný peak/RMS vstupu, zpracovaného signálu a mixu.
Není-li vstup otevřený, zobrazují „Bez signálu“, při nulových vzorcích
„Ticho“; po zastavení se nenechává poslední signál. Neplatné hodnoty se
nahrazují nulou, úrovně se omezují a přebuzení i zahozené analytické bloky
se počítají. Analytické bloky dostávají pre-monitor PCM, epochu, vzorkování
a přesnou první pozici vzorku. Analytický consumer navazuje ve W19.

C ABI 5 přidává 112b meter snapshot a explicitní Live/monitor příkazy.
Nativní a C# kontrakty kontrolují skutečné layouty, spuštění bez monitoringu,
vypnutí při stop a výslovnou změnu. Linux 5/5 CTest zahrnuje mono/stereo
routing, izolaci dalšího vstupu/výstupu, rampu, předávání PCM, ticho, NaN,
přebuzení, restart epochy a přeplnění analytické fronty. Selhání řídicího workeru uvolní čekající příkazy a odmítne další práci,
neponechá je nekonečně čekat. Rezervovaný Stop před workerem také nemůže
zrušit novější Start; C# ověřuje 20 takových okamžitých dvojic.

Audit vlastní
realtime cesty zahrnuje i zpracování vstupu bez běžných new/delete.

Skutečné nahrávání vybrané sběrnice naváže ve W22; tímto krokem se nevydává
za dokončené. DSP aparát, analytický worker a kompletní Live transport se
připojí samostatně. Fyzické kanály, poslech, odezva a chování M-Track nebyly
v Linux/CI testech ručně ověřené. Windows build a nativní formulář navazují.

Windows [38160896192](https://github.com/JendaNDT/rocker/actions/runs/38160896192)
prošel všemi 5 CTest, C# kontrakty a dvěma WinUI starty včetně Live stránky.
[Důkaz](windows-validation.json) odděluje tuto revizi od pozdějších hodin a DSP.
