# W04 — Společné přehrávání médií a not

Nahrávky i MIDI z nativního AlphaTab náhledu procházejí jediným C++ hostem
se stejným výstupním zařízením jako testovací tón. Žádný samostatný přehrávač
ani SoundFont se neotevírá. WinUI používá systémové nabídky, dialogy souborů,
standardní tlačítka, posuvníky a klávesové zkratky. Vintage tokeny mají světlou,
tmavou a systémovou kontrastní variantu; nejde ještě o kompletní W08 design.

## Implementované chování

- Nahrávka se dekóduje a případně zpomalí na workeru. Úspěšná příprava zastaví
  a odpojí callback před výměnou PCM; zvuk nikdy nezačne samotným načtením.
- Přehrávání, opakovaný start, zastavení, návrat na začátek, změna pozice,
  hlasitost a rychlost 0,25×–2× jsou dostupné přes C ABI i WinUI.
  Pozice patří zpomalené časové ose. Změna rychlosti přehrávání zastaví;
  nový start zůstává výslovný. Zastavení uchovává pozici.
- Callback pouze čte vlastněné neměnné PCM, interpoluje pro skutečné vzorkování
  zařízení a zapisuje výstupy. Nedekóduje, nezamyká, nealokuje a neuvolňuje
  paměť. Mono jde do obou sluchátek; stereo kanály zůstávají oddělené.
- Konec skladby přejde do ticha s krátkým dozněním. Stavový dotaz na řídicím
  vlákně následně uvolní zařízení; další Přehrát začne od začátku.
- MIDI se na workeru převede na procedurální strunný zvuk. Respektuje tempo,
  note-on/off, velocity a sustain. Podporuje SMF 0/1, nejvýše 64 stop,
  100 000 událostí, 5 000 not a tři minuty. PCM, souběh hlasů i výpočetní práce
  mají samostatné limity. Bicí, nenulové pitch bendy a SMPTE jsou výslovně
  odmítnuté; nejsou vydávané za dokončenou notovou artikulaci W29.
- C# před zařazením workeru zachová SafeHandle. Zavření okna ihned zastaví
  zvuk; nativní handle se uvolní po doběhnutí přípravy. Worker nevolá WinUI.
  Zastavit během přípravy not zruší i čekající úmysl začít přehrávat.
- Neúspěšná příprava ponechá předchozí PCM. Detail chyby se bezpečně kopíruje
  přes UTF-8. Nativní callback nevolá spravované delegáty.

## Automatické ověření

Linux: 3/3 CTest, včetně 138 mediálních a 36 transportních/syntézních
kontrol. Transport ověřil frekvenci A4, délku a note-off, mono/stereo směrování,
48→44,1 kHz i 48→96 kHz, seek, EOF, ticho, hlasitost, chyby a zastavování během
přípravy. C# přes skutečné P/Invoke ověřilo vlastní MIDI a MIDI obou AlphaTab
ukázek, parametry a deset zavření handle během workeru. Nativní testovací
zařízení je samostatná knihovna, která není v produkčním balíku.

Windows sestavení, publikovaný WinUI náhled a příprava MIDI přes skutečnou
produkční DLL se ověřují v navazujícím CI běhu. Dokud výsledek není zapsaný
v protokolu, nejde o potvrzený Windows build. Fyzický poslech a měření odezvy
se v CI neprovádějí; ruční přejímka je podle pokynu uživatele odložená po W43.

PWA reference: 561/561 testů, TypeScript a lint prošly 11. 10. 2026.
Souběh YouTube a zařízení, finální notové nástroje a další produktové pohledy
jsou další implementace. Průběžný interní balík se nenabízí k ruční přejímce.
