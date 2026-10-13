# Vintage přístroje — druhé propracování

Zadání Jendy z 13. 10. 2026: více retro materiálů a detailů při zachování skutečné nativní aplikace Windows.

- Ořechové bočnice a jemná textura koženky na přístrojových panelech. Materiály jsou vlastní deterministické vektory; nepoužívají cizí fotografie ani externí assety. Počet dekorací je omezený, žádný nový časovač.
- Aparát má viditelnou tkanou mřížku komba, měděný štítek Rocker a drobné zapuštěné šroubky. Metronom má vlastní přístrojový štítek; kompaktní Live zachovává původní výšku panelů.
- Bakelitové knoflíky mají vroubkovaný obvod a kontrastní rysku. Přesný NumberBox a Slider nadále zajišťují klávesnici, fokus i automatizační rozhraní.
- Dekorativní kovová páčka a jantarová kontrolka aparátu sledují skutečný stav původního nativního CheckBoxu; nezachytávají vstup a nevytvářejí druhou zvukovou cestu.
- Podsvícené ručkové měřáky v ladičce a podrobnostech Live mají stupnici −60 až 0 dBFS a kontrolku přebuzení. Zobrazují sample peak, nikoli kalibrované VU nebo latenci. Samostatná ladička nyní čte skutečný InputPeak místo původního pomocného RMS × 3; zvukové zpracování není změněné.
- Běžné nabídky, seznamy, dialogy a navigace si ponechávají nativní chování Windows. Světlý vzhled zachovává tmavý bakelit. Vysoký kontrast nahrazuje materiály systémovými barvami a vypíná ornamentální kresbu; fyzické ověření vysokého kontrastu a Narratoru zůstává ruční.

Automatická kontrola zkouší hranice měřáku, přebuzení, neplatný/chybějící vstup a zachování nativního přepínače. Ukázka měřáků v galerii je výslovně označená: připravené hodnoty nikdy nevstupují do živého zvukového jádra. Windows build zároveň ověřuje původních 32 pohledů, nové pracovní funkce, rozložení Live, přístupné nativní ovladače a celý instalační cyklus.

Stav: implementováno; aktuální Windows ověření a sestavený instalátor čekají na běh CI. Skutečná odezva a poslech na M-Track Solo nejsou tímto grafickým ověřením potvrzené.
