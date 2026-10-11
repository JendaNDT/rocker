# W19 — Ladička a analýza mimo audio callback

Samostatný nativní worker odebírá syrový vybraný vstup z omezené SPSC fronty.
Audio callback dál pouze kopíruje bloky; nepočítá FFT ani pitch a nepřidává
analytické čekání do monitoringu. Epochy, souvislé vstupní frame a stáří měření
jsou součástí 144b ABI 9 snapshotu. Ztracený blok nebo změna zařízení obnoví
měřicí okno. Bez aktivního vstupu nebo při stáří přes 250 ms je frequency 0.

Detektor je vlastní implementace matematického YIN principu: normalized
squared differences, první jasná perioda, windowed-sinc fractional-period refinement.
Nejde o převzetí zdrojů Pitchy ani tvrzení o identickém výsledku s webovým MPM.
Pracovní vstup projde dvěma lowpass filtry a resamplingem na max. 12 kHz;
2048 vzorků znamená 171 ms okno, při 8 kHz 256 ms. Hop 256 omezuje výpočet
přibližně na 47 aktualizací/s. Podporovaný mono rozsah 30–1400 Hz pokrývá
původní šestistrunnou kytaru včetně E6, confidence gate je 0.9. Smoothing 0.8
odpovídá původnímu významu. Ticho/slabý vstup měření ruší, poslední tón se
nevydává za aktuální. Délka okna není roundtrip zpoždění monitoringu.

Práh šumu používá původní RMS × 3 stupnici. RMS okno 20 ms zabraňuje falešnému
tichu u krátkého posledního bloku na průchodu sinusovky nulou. Dvousekundová
kalibrace používá skutečné vzorky: peak RMS × 3 + 0.03, max. 0.5, krok 0.01.
Každá kalibrace má token; nová žádost nepřebere starý hotový výsledek. Změna
epochy nebo mezera ji přeruší. Samotné nastavení gate nepouští zařízení.

Vlastní 2048bodová Hann FFT zachovává původní raw sample rate pro centroid.
DC se před spektrem odstraní. Palm mute má původní onset RMS 0.04, refractory
320 ms a průměr centroidů těla struny mezi 40–170 ms od úhozu. Kalibrace
open/muted a trénink navážou ve W28 na tyto skutečné události; centroid není
univerzální rozpoznávač techniky a FFT/filtry nemají bitovou shodu s Web Audio.

WinUI ladička má vedený/chromatický režim, automatický i ruční výběr struny,
H/B značení, A 430–450 Hz, tovární/vlastní ladění, referenční hlasitost,
práh/kalibraci šumu, věrohodný stav ticha/warm-up/nejistoty a dobrovolné
potvrzení do 5 centů po 400 ms. Vlastní ladění se ukládají a obnovují,
odstranění vyžaduje potvrzení. Referenční A je volitelné pole, původní data
bez něj zůstávají 440 Hz a původní default JSON se nemění.

Referenční sinus + 0.3 druhé / 0.15 třetí harmonické a dvě potvrzovací chimes
1320/1980 Hz hrají v jednom grafu mimo aparát, bez dalšího zařízení. Attack
30 ms, release 50 ms, cap 5 s přehrané audio doby přežije restart/rate změnu.
Chimes mají attack 4 ms, decay 120 ms a odstup 130 ms. Pointer hold se uvolní
po ztrátě capture, klávesnice/UIA spustí omezený náhled. Samostatné rezervace
reference i celého grafu brání opožděnému startu nebo starému release po nové
žádosti; úplné Stop zruší i frontu referencí. Bez vstupu se nikdy nedetekuje
referenční tón jako měřená kytara. Souběh neresetuje hodiny ani doprovod.

Linux CTest 10/10 zahrnuje čisté/harmonické tóny, detuning, 8/44.1/48/96/192 kHz do MIDI 88 (E6),
noise/silence, FFT sinus, tři úhozy, worker calibration/epochy, referenční obálky
/cap/chimes a RT audit aktivních hlasů. C# kontroluje celý ABI snapshot, gate
hranice, neproveditelnou kalibraci bez vstupu a společnou cestu reference.
Windows UI/persistence se ověřují. Fyzická kytara, subjektivní přesnost,
ruční vlastní tuning dialogy a M-Track nejsou otestované.
