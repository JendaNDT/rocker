# W18 — Metronom ve společném audio grafu

Nativní metronom používá vzorkové hodiny W16, nikoli UI timer. Pět původních
charakterů kliků má vlastní syntézu: sine/triangle, filtrování deterministického
šumu, frekvence, úrovně a délky podle `src/audio/metronome.ts`. Nejde o bitovou
shodu s Web Audio. Pevný pool 16 hlasů a 32 parametrů ve frontě nealokuje v
callbacku; případné přetečení se přizná ve snapshotu.

Podporuje 1–16 dob, 1–4 subdivize, akcent/ticho každé doby, přepis zvuku doby
a mezidoby, swing 50–75 % pro osminy/šestnáctiny, odpočet 0–2 taktů, rampu,
tiché takty a samostatné 2–7 pulzy za dobu. Triplet swing se nemění. Zvuky
mají původní význam a druhá vrstva původní headroom 0.5. Tiché takty ztlumí
obě vrstvy, odpočet pouze hlavní vrstvu. První tempo-rampa je až na navazující
bar boundary, odpočítané takty se nepočítají. Změna taktu/dělení začne na další
době, ostatní parametry na začátku následujícího audio bloku. Rampy mění tempo
přímo na vzorku hranice taktu bez zaokrouhlování na blok.

Výslovné spuštění otevře výstup i bez kytary; při běžícím zdroji přidá kliky
bez resetu zařízení, PCM kurzoru a epochy. Kliky se přičítají do stereo mixu
mimo kytarové efekty. Vypnutí samotného metronomu ponechá Live/doprovod běžet;
úplné Stop ho odzbrojí, takže následující přehrávání neobnoví staré kliky.
Parametr sám neotvírá zařízení. EOF doprovodu nezastaví aktivní monitoring
ani metronom. Ke společnému startu nahrávání/Looperu se odpočet připojí až ve
W22/W23; současný odpočet nezdržuje běžící doprovod.

WinUI má Vintage BPM ovladač i číslo, per-beat tlačítka a zvuky, celý tréninkový
formulář, tap z posledních šesti úderů s resetem po 2 s, tovární a vlastní
presety s potvrzeným odstraněním, uložené konfigurace a společný panel Live.
Vizualizace čte native snapshot každých 50 ms a nijak neplánuje zvuk.

Linux CTest 8/8 včetně metronomu, RT alokačního auditu a proměnlivých bloků.
Managed test skutečně volá 240b konfiguraci a 80b snapshot C++ přes P/Invoke,
kontroluje chybný vstup, start bez kytary, odzbrojení, epochu/kurzor a zachování
ostatních zdrojů. [Windows sestavení prošlo](windows-validation.json): 8/8 CTest,
oba publikované WinUI pohledy, 23 společných ovladačů a nativní/persistentní
roundtrip jednoho ze šesti továrních presetů. Fyzický poslech,
subjektivní přechody, ruční preset dialogy a karta M-Track nejsou otestované.
