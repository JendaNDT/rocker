# W14 — Výslovný výběr zvukového zařízení

JUCE poskytuje skutečný seznam backendů, vstupních/výstupních zařízení,
fyzických kanálů, vzorkování a bufferů. Formulář ve WinUI používá tyto údaje,
ne předem vymyšlené možnosti pro M-Track. „Výchozí ovladače“ je samostatná
volba a otevření vrátí skutečný formát. ASIO, sdílené WASAPI, nízkolatenční
sdílené WASAPI a výhradní WASAPI jsou backendy z připnutého JUCE buildu.
Dostupnost ASIO zařízení závisí na nainstalovaném ovladači výrobce.

Jediný host otevírá AudioIODevice přímo. AudioDeviceManager slouží pouze
k enumeraci a nemá aktuální otevřené zařízení; jeho automatický hotplug
fallback tak nemůže přepnout živý zvuk na reproduktory. První výslovný test
výchozího výstupu ho vyřeší podle jména jednou; další starty používají tutéž
identitu backend + název. JUCE API neposkytuje univerzální trvalé GUID napříč
backendy: přejmenované či chybějící zařízení vyžaduje novou výslovnou volbu.
Shodně pojmenované fyzické karty nelze tímto rozhraním spolehlivě rozlišit.

Potvrzení zastaví zvuk, otevře požadovaný formát bez callbacku/monitoringu,
zkontroluje kanály a skutečnou konfiguraci, pak zařízení uvolní. Není-li
formát dostupný nebo ovladač změnil výslovně požadované hodnoty, skončí
chybou a předchozí volba zůstane. První neúspěšná výslovná volba nezpůsobí
pozdější použití náhodného výchozího zařízení. Nastavení se ukládá v běžných
validovaných nastaveních a zálohách; po restartu se ověří, ale zvuk se nespustí.
Při nedostupné uložené kartě UI vyžaduje výběr náhrady.

Aktivuje se právě jeden fyzický vstup, JUCE ho sbalí do callback indexu 0;
druhý vstup se nemíchá. Výstup je vybraná dvojice, u mono zařízení jediný
výstup. Vstupní monitoring a sběrnice se připojí ve W15. Panel ASIO ovladače
je výslovná operace po zastavení; cizí ovladač se neinstaluje ani nebalí.
UI rozlišuje odhad latence vstupu/výstupu v počtech vzorků od skutečného
měření celé odezvy. Přímý hardwarový monitoring je samostatná cesta bez DSP.

Linux ověřuje C ABI 4 (32b konfigurace), skutečné C# volání fake ovladače,
seznam/kanály/formáty, malé JSON buffery, nepodporovaný formát, chybějící
zařízení, zachování poslední volby a výstup bez automatického spuštění.
Připnuté JUCE hlavičky ověřují kompilaci adaptéru. Windows runner musí
potvrdit produkční DLL, WinUI formulář a enumeraci sestavených backendů.
CI enumerace nevydává prázdný seznam za úspěšný poslech nebo funkční ASIO kartu.
M-Track, obsazené zařízení, mikrofonní oprávnění, hotplug a spánek/probuzení
vyžadují pozdější skutečnou přejímku W44; nejsou označené jako provedené.
