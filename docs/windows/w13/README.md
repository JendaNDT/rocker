# W13 — Řízení jediného hostu a realtime kontrakty

C++ host má samostatné řízení zařízení: zastaveno, spouštění, běží,
rekonfigurace, chyba, zastavování a ukončování. Sériová omezená fronta
64 operací a vlastní vlákno nikdy neběží v audio callbacku. JUCE nadále
vlastní svůj message loop. Stav, transport a chyby se čtou z krátké kopie;
UI nečeká na pomalý ovladač. Operace C# běží na workerech se SafeHandle
lease; zavření WinUI čeká asynchronně na dokončení a uvolnění hostu.

Start rezervuje identitu ještě před naplánováním workeru. Stop ji zneplatní
a okamžitě zavře zvukovou bránu; uvolnění zařízení doběhne mimo UI. Callback
kontroluje také platnost identity, takže ani pozdější enable po zrušení
nezapne zvuk. Pomalé spuštění, opakované start/stop a dispose při přípravě
jsou skutečné automatické kontrakty, nikoli ruční poslech.

Předalokovaná SPSC fronta má omezenou kapacitu, pořadí a čítač ztrát.
Analytický blok obsahuje mono PCM, epochu, vzorkování a pozici vzorku.
Vstup, analytický worker a diskový writer se připojí ve W15/W19/W22;
existence fronty sama neznamená hotové nahrávání. Renderer používá stejné
immutable PCM a tónový DSP pro headless kontroly i zařízení. Generátor se
spouští výslovně; nezastupuje měření vstupu kytary.

Linux: 4/4 CTest, 25 realtime assertions (včetně detekujícího canary auditu,
200 000 přenosů přes obalení fronty, nulových běžných C++ new/delete v
rendereru a blokování pozdního enable). Kontrakt s ovladačem zpožděným o
sekundu ověřuje rychlé čtení stavu a ztišení zrušeného startu. C# přímo
volá C++ pro běžný životní cyklus, 20 okamžitých start/stop, pomalý start,
chybné ABI, média, MIDI a dispose během přípravy.

Audit pokrývá vlastní renderer a SPSC, nikoli všechny interní alokace JUCE
nebo skutečného ovladače. Ověření Windows sestavení navazuje samostatně.
Fyzická latence, hotplug, spánek a hardware jsou odložené do závěrečné
přejímky; skutečný vstup se tímto krokem zatím neotevírá.
