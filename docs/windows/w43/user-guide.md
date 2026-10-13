# Rocker pro Windows — návod

## Instalace

Na stránce soukromého GitHub vydání stáhni `Rocker-Windows-Setup-0.3.0-x64.exe`. Musíš být přihlášený účtem s přístupem k repozitáři. Spusť instalátor a potom **Rocker** z nabídky Start. Nejsou potřeba vývojové nástroje ani zdrojové soubory. Vyžaduje Windows 11 x64 od 24H2.

Volitelný `Rocker-Windows-x64.zip` je přenosná alternativa: **Extrahovat vše**, potom `Rocker.Probe.exe`; soubory musí zůstat pohromadě. GitHub **Code → Download ZIP** obsahuje projekt, nikoli hotovou aplikaci. Odpovídající zdroje jsou samostatný `Rocker-Windows-sources.zip`.

## Pracovní plocha, dokumenty a okna

Vlevo je knihovna, uprostřed otevřené dokumenty v záložkách a vpravo inspektor a připojené nástroje. Knihovna obsahuje konkrétní lekce, tréninky, skladby, projekty i nahrávky. Hledání a oblíbené jsou dostupné přes Ctrl+K. Panely mají posuvné děliče; nabídka Okno je umí skrýt a obnovit.

Záložku přes pravé tlačítko **Otevřít v samostatném okně** odpoj na další monitor. **Připojit do pracovní plochy** ji vrátí. Stejné ovládání je v nabídce Okno; záložky lze přetahovat mezi okny. Dokument si zachová výběr a rozpracovanou práci. Znovu otevřený stejný soubor či projekt aktivuje existující záložku. Ctrl+Tab přepíná záložky daného okna, Ctrl+W zavře aktivní dokument.

Ladička, metronom, aparát a looper mají jedinou instanci. Jejich X pouze skryje nástroj; zvuk zůstává společný. Live je vlastní okno; F11 přepne celou obrazovku, Esc ji opustí. **Vždy navrchu** je výslovná volba v nabídce pomocného okna. Hlavní X ukončuje celou aplikaci po vyřešení rozpracované práce.

Otevření skladby či nahrávky zobrazí obsah a průběh bez přepnutí přehrávání. Spodní transport uvádí jeho skutečný **Zdroj**. Tlačítko Přehrát v konkrétním editoru převezme společný zdroj; při nahrávání nebo aktivním looperu nejprve dokonči tento záznam. Vypnutý monitoring kytary neztlumí skladbu ani metronom. Když za aparátem vidíš signál a kytara v mixu chybí, zkontroluj **Poslech kytary** v Live.

Lekce mají kroky vlevo a úlohu uprostřed. Studio má stopy, časovou osu a vlastnosti; Ctrl+S, Ctrl+Z a Ctrl+Y pracují s aktivním projektem. V úzkém editoru jsou vlastnosti přes tlačítko Vlastnosti. Průběh nahrávky lze vybírat i při přehrávání jiného dokumentu; kliknutí na pozici nebo zapnutí smyčky zdroj výslovně převezme.

Rozměry oken, skupiny a výběr záložek se vrátí při dalším spuštění. Zvuk se s nimi nespustí. **Nastavení → Zálohy a soubory → Uložit / Načíst rozložení oken** předává samostatný `.rockerlayout`. Hudbu a média zálohuj také do `.rockerbackup`.

## První nastavení a M-Track Solo

1. Průvodce projdi s profilem kytary a svou orientací rukou. Import i zvukovou kartu můžeš vynechat a později otevřít v Nastavení.
2. Připoj kytaru k M-Track Solo, nastav nástrojový vstup podle označení karty a sluchátka připoj ke stejné kartě. Ovladač ber z oficiálního webu M-Audio.
3. Ve **Zvuková zařízení** vyber kartu, výstup a právě jeden vstup s kytarou. Druhý kanál se automaticky nepřimíchá. Pro efekty vyzkoušej dostupný ASIO ovladač výrobce; WASAPI je alternativa. Karta nabídne své podporované frekvence a buffery, nikoli vynucené minimum.
4. Klikni **Ověřit a použít**. V Live zapni monitoring výslovně, začni malou hlasitostí. Pro poslech softwarových efektů nepřimíchávej zároveň přímý monitoring karty.
5. Pokud zvuk praská, zvětši buffer. Údaj bufferu a odhad ovladače nejsou měření celé odezvy. Vyladění pro bratrův počítač se zaznamená v přejímce.

Bez kytary zůstávají použitelné lekce, knihovny, hry a soubory; ukázkový signál je výslovně označený. Ticho se nevydává za naměřený tón.

## Běžné hraní

Návod je dostupný přímo v nabídce **Nápověda → Návod k používání** a místně ve složce aplikace `help`.

Přehled otevře doporučenou konkrétní úlohu. Live sdružuje ladičku, tempo, aparát a nahrávání. Aktivní cvičení spusť, pozastav a ukonči společným ovládáním; otevřené okno samo nepřičítá minuty. Přepínání pohledů nevytváří další zvukový host. Zen v Nastavení skryje odměny.

Lekce mají uložený krok a návaznost; přesný trénink hodnotí jednotlivé tóny. Power chord, Gallop a Palm mute obsahují doprovodné hudební cykly; kalibraci palm mute dělej s připojenou kytarou. Knihovny ukazují hmaty a umožňují poslech přes stejné jádro. Levou ruku nastav v profilu.

Vlastní skladbu lze otevřít systémovým dialogem nebo podporovaným přetažením. Ulož A/B úsek, uprav rychlost nebo nastav zrychlování. Noty podporují stopy, zoom, vybrané takty a společné přehrávání. Mezerník ovládá příslušný transport mimo textové editory.

Nahrávej suchý vstup, zvuk po aparátu nebo mix. Dokončený take najdeš v Nahrávkách; lze jej pojmenovat, označit časovými značkami, porovnat a exportovat WAV. Looper má až osm vrstev; rozpracované vrstvy jsou do výslovného uložení v paměti. Studio ukládá vícestopý projekt. K souboru `.rockerproj` patří sousední složka médií; pro přenos použij export projektu, nikoli osamocený manifest. Ctrl+Z vrací definovanou editaci Studia.

## Data, přechod z webu a zálohy

**Zálohy a soubory** umí vytvořit úplnou `.rockerbackup`, ukázat její obsah před obnovou a převést staré webové nastavení i nahrávky. Převodový pomocník pracuje v původním prohlížeči na původním webu; původní data nemaže. Postup je v [W37](../w37/README.md). Klíče se nezapisují do běžných exportů.

Data jsou v `%LOCALAPPDATA%\Rocker`, aplikace v `%LOCALAPPDATA%\Programs\Rocker`. Odinstalace data ponechá. Kopie z profilu sama nenahrazuje pravidelnou zálohu na jiné místo.

Před aktualizací zavři Rocker a ponech dost místa pro úplnou zálohu. Instalátor uloží databázi i `.rockerbackup` a ponechá předchozí binárky. Novější schéma starší aplikace odmítne. Případný návrat vyžaduje kompatibilní zálohu; obnova připraví náhradu zvlášť a celý původní profil zachová v sousední složce. Po obnově aplikaci spusť znovu.

## Volitelné online části a hlášení chyby

YouTube používá oficiální přehrávač a jeho pravidla; živý souběh s ASIO závisí na ovladači. AI vyžaduje vlastní OpenAI API klíč a může mít náklady u poskytovatele. Na vyžádání se odesílá zvolená historie a vybrané statistiky/profil, nikoli audio. Gemini je pro tento osobní profil blokovaný; původní klíč se nepoužije jinde.

Při potížích napiš verzi, obrazovku, provedené kroky a chybovou hlášku. Diagnostiku ukládej a sdílej ručně ze zvukových zařízení; nedávej do hlášení API klíče. [Přejímací protokol](brother-test.md) je připravený pro skutečné hraní.

## Pohodlnější každodenní hraní

- **Jen hraju:** na přehledu nebo přes Zobrazení → Jen hraju / F11. Otevře samostatné Live na celé obrazovce. Esc vrátí běžné okno Live. Samotné přepnutí nezapíná zvuk.
- **Sestavy:** připrav pohled, aparát, ladění a metronom. U skladby nastav rychlost a A/B smyčku. Nástroje → Uložit sestavu cvičení. Otevření přes přehled, hledání nebo Moje sestavy připraví všechny uložené hodnoty; přehrávání a monitoring spustíš sám. Chybějící skladbu nejdřív přemísti přes Vlastní skladbu. Sestavy lze přejmenovat a odstranění potvrdit druhým kliknutím. Úplná záloha je obsahuje.
- **Hledání a oblíbené:** Ctrl+K nebo Hledat na přehledu hledá obrazovky, jednotlivé lekce, akordy, stupnice, riffy, trenažéry, skladby, nahrávky a sestavy. Hledání rozumí českým názvům bez diakritiky. Pravé tlačítko na záložce přidá nebo odebere její úlohu z oblíbených; v hledání lze uložit i nahrávku nebo sestavu. Filtr Pouze oblíbené ukáže připnuté dostupné položky.
- **Průběh nahrávky:** v Nahrávkách vyber položku a Zobrazit průběh bez přehrávání. Kliknutí do zvukové vlny změní pozici, přetažení vybere A/B úsek. Stejné hodnoty nastavíš přes číselná pole; opakování zapneš tlačítkem. Značka na aktuální pozici se uloží k nahrávce, její popis upravíš přes Značky a poznámky. Průběh vychází z dekódovaného souboru; nejde o hodnocení hry. Výběr a smyčka zvukový soubor neupravují.
- **Aktualizace:** Nápověda → Aktualizace otevře kontrolu vydání v Nastavení. Instalace se nikdy nespustí sama; volitelně lze povolit nabídku při spuštění. Repozitář je soukromý: přímá kontrola/stažení potřebuje vlastní GitHub token s Contents: read pro JendaNDT/rocker, uložený přes chráněné pole v aplikaci. Token se chrání DPAPI a není v zálohách. Bez něj použij tlačítko pro přihlášený systémový prohlížeč a stáhni instalátor z vydání. Nativní stažení ověřuje SHA-256 a dokončený Windows balík. Tlačítko Nainstalovat a zavřít Rocker po potvrzení a případném uložení rozpracované práce zastaví zvuk a otevře instalátor.

## Viditelná vizualizace metronomu

Otevři **Okno → Metronom**. Nahoře je přepínač **Vizualizace: Doby / Kyvadlo**, přímo u přístroje a nastavení tempa. Tlačítkem Spustit metronom zapneš zvuk i časování. Kyvadlo je skutečná pohybující se páčka; pod ní svítí aktuální očíslovaná doba. Připravený přístroj je vidět i před spuštěním, se zastavenou páčkou.

Stejný přístroj najdeš v samostatném okně **Live** a v režimu **Jen hraju (F11)**. Tempo zadej v číselném poli; vedle něj přepni Doby nebo Kyvadlo. Volba platí ve všech pohledech a zůstane uložená. Podrobné rytmy, zvuky, akcenty, odpočet a tréninkové vrstvy jsou v sekci **Rytmus, zvuky a trénink** samostatného metronomu.

Při odpočtu nebo tichém taktu se stále ukazují doby; text odlišuje, co právě probíhá. Vypnutí metronomu vrátí kyvadlo doprostřed. Pokud máš ve Windows vypnuté animace, páčka se nehýbe a aktuální doba se nadále mění.
