# W17 — Nativní aparát a Vintage ovladače

Aparát zpracovává vybranou mono kytaru ve společném input grafu. Řetězec:
komprese bez lookaheadu → gain/drive → overdrive/distortion/fuzz → třípásmový
EQ → moderní/vintage/open kabinet → paralelní delay/reverb/chorus → level.
Ovladače zachovávají rozsahy a význam původních presetů; drive 0 znamená
původní mírný drive, nikoli vypnutí celého zkreslení. Kabinety mají původní
frekvence/Q/gain z `src/audio/ampChain.ts`. Reverb je vlastní deterministická
síť osmi zpoždění s útlumem, původní web používal náhodný syntetický impulse.
Nový DSP neslibuje bitovou ani zvukovou shodu s Web Audio.

Zkreslení má 2× oversampling: dvě 31tap halfband FIR, známá skupinová
prodleva 15 vstupních vzorků. Hodiny ji uvádějí samostatně; filtr kabinetu
má navíc frekvenčně závislou fázi a echo/reverb hudební zpoždění.
Tato hodnota není měření roundtrip odezvy karty. Komprese na nule nevkládá
lookahead. Vypnutý aparát je přesný unity bypass bez FIR prodlevy.

Parametry, charakter zkreslení, EQ a kabinet se mění plynule. Vypnutí celého
aparátu provede 20ms crossfade na raw, pak změnou generace vyprázdní historii
zpoždění bez velkého mazání bufferů v callbacku. Opětovné zapnutí na tichu
nevrací staré efekty. Nulový mix paralelního efektu nepřidává jeho wet signál.
Při neplatném DSP stavu se graf obnoví a přizná chybu v diagnostickém čítači.
Předalokované ring buffery a 32 parametrů ve frontě nealokují ani nečekají
v callbacku; plná fronta přizná chybu. UI slučuje změny ovladačů.

WinUI stránka Aparát má všech devět Vintage knobů s přesným číslem a sliderem,
volbu zkreslení/kabinetu, tovární i vlastní presety, uložení a potvrzené
odstranění vlastního presetu. Tovární obsah se neodstraňuje. Nastavení,
preset a stav bypassu se ukládají do ověřených dokumentů/autosave a záloh.
Live má společný přepínač/preset; navigace nevytváří další host. Monitoring
zůstává samostatné výslovné zapnutí.

Linux 7/7 CTest ověřuje přesný bypass, impulsovou FIR prodlevu, všech 36
kombinací tří zkreslení × tří kabinetů × čtyř vzorkování při maximálních
parametrech, konečný/omezený signál bez DSP resetů, doznívání a jeho vymazání,
parametrovou frontu a RT alokace aktivního aparátu. C# roundtrip ověřuje
všech 96 bajtů konfigurace, NaN odmítnutí, zachování předchozího nastavení
a publikovanou prodlevu. [Windows build prošel](windows-validation.json): 7/7 CTest,
oba publikované WinUI pohledy, všech devět ovladačů, nativní preset
a opětovné otevření uložených amp/preset dokumentů. Fyzický zvuk se tím netestoval.

Vlastní waveform export a skutečná volba nahrávací sběrnice navazují ve W22.
Fyzický poslech, subjektivní kvalita, parametrické přechody a odezva na M-Track
zůstávají neprovedené do přejímky ucelené aplikace; syntetické testy je
nevydávají za potvrzené.
