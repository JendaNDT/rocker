# Rocker pro Windows — návrhy Vintage

Tato sada rozpracovává celé rozhraní Rockeru pro Windows. Jde o vizuální návrhy a popis zamýšleného ovládání, nikoli o hotovou nativní aplikaci. Obsah obrazovek je ukázkový.

Součást [úplné implementační roadmapy](../../../WINDOWS_ROADMAP.md).
[Všechny listy](vsechny-navrhy.md) · [Interaktivní galerie](index.html).

## Dohodnutý směr

- Nativní rozhraní Windows; vhodným základem je WinUI 3. Běžné ovládací prvky, klávesnice, fokus, škálování, přístupnost a systémové dialogy se mají chovat podle zvyklostí Windows.
- Vintage tvoří barevnost, jemné materiály a hudební přístroje. Nepřebíjí čitelnost běžných seznamů, formulářů a nabídek.
- Zvuk má samostatné nativní jádro; instalátor s webovým zvukem sám o sobě nezajistí menší prodlevu.
- Průběžné ověřování bez kytary použije připravené signály a nahrávky. Bratr následně otestuje ucelenou verzi na svém Windows počítači a zvukovce.
- Návrhy jsou uložené v repozitáři společně s roadmapou. Funkce nejsou těmito dokumenty ani obrázky implementované.

## Společné chování

- Menu Soubor / Úpravy / Zobrazení / Nástroje / Nápověda a kontextové příkazy podle aktuálního pohledu.
- Skupiny navigace Přehled, Live, Cvičení, Zvuk, Knihovna, AI tutor, Nastavení. Podsekce se otevřou v pracovním prostoru.
- Respektovat změnu velikosti okna, maximalizaci, přichycení a škálování Windows; menší okno zjednoduší rozložení panelů.
- Použít skutečné systémové dialogy otevírání a ukládání, přetažení souborů, čitelné stavy fokusu a přístupné popisky.
- Klávesové zkratky nesmějí přebírat psaní v textových polích. Mezerník ovládá transport pouze v odpovídajícím kontextu.
- Transport má stabilní umístění; vždy je zřejmé, zda se přehrává, nahrává, nebo čeká na zařízení.
- Odpojení zvukovky, neúspěšné uložení a zaplněný disk se zobrazí s konkrétním postupem. Zachovat dostupná neuložená data.
- Uvedené hodnoty na obrázcích jsou demonstrační. Latenci nelze označit za změřenou bez skutečného měření celé zvukové cesty.
- YouTube vyžaduje vložený webový přehrávač v omezené části okna; nativní obal a jeho ovládání z něj neudělají nativní video službu. Integraci je nutné technicky ověřit.
- Notový prohlížeč AlphaTab se dnes používá jako webová komponenta. Projekt nabízí také .NET API; nativní renderer, integraci do WinUI a společné přehrávání je potřeba ověřit v kroku W04 roadmapy. Další webová výjimka se nepředpokládá bez samostatného rozhodnutí.

## Úplný seznam návrhů

### 01 — Každodenní hraní

**01 Přehled** — Denní plán otevře konkrétní úlohu; poslední cvičení má přímé pokračování.

**02 Live** — Velké ovladače; mezerník přehrává či zastavuje, zkratky nahrávání platí mimo textová pole. Aktivní hraní se započítává do cvičení.

**03 Ladička** — Vedený a chromatický režim, referenční tón, vlastní ladění a vstupní citlivost. Bez signálu se zobrazí pomlčka.

**04 Metronom** — Tempo lze psát i krokovat; presety, swing, předtakt, postupné zrychlování a polyrytmus mají vlastní nastavení.

### 02 — Lekce a skladby

**05 Výběr lekce** — Výběr lekce zachová pozici a postup; denní plán otevře přímo doporučenou lekci.

**06 Průběh lekce** — Kroky se ovládají klávesnicí; poslech zobrazuje skutečný stav vstupu. Dokončení lekce uloží pokrok.

**07 Vlastní skladba** — Otevření i přetažení zvukového souboru; rychlost, A/B smyčka a opakovaně použitelné úseky.

**08 YouTube cvičení** — Internetové video bude vložené pouze do vyhrazeného přehrávače; obsluha smyček a okolní rozhraní zůstanou nativní.

### 03 — Technika a trenažéry

**09 Přesný trénink** — Zachovat původní tónový drill: průchod cvičením, chyby na jednotlivých pozicích, problémové okno a tři čistá opakování. Obrázek s waveformou ukazuje druhý režim cvičení úseku skladby: A/B, opakování a řízené zrychlování. Tento režim nenahrazuje tónové hodnocení.

**10 Power chordy** — Výběr akordů, jejich poloh a tempa; průběh tréninku zůstává čitelný z odstupu.

**11 Gallop** — Rytmický vzor, akcenty a postupné tempo; stejná pravidla transportu jako u metronomu.

**12 Palm mute** — Kalibrace podle konkrétní kytary a vstupu; graf rozlišuje ukázkový signál, živý vstup a chybějící zařízení.

### 04 — Zvuk a nahrávání

**13 Aparát** — Presety a jasně označené aktivní efekty; vypnutý efekt se skutečně obchází. Výstupní úroveň je vždy dostupná.

**14 Nahrávky** — Výběr více položek, kontextové nabídky, přejmenování, export a porovnání A/B. Mazání má potvrzení nebo možnost vrácení.

**15 Studio** — Více stop, hlasitost a panorama, poslech vybraných stop a export mixu. Časová osa podporuje zoom a přesný posun.

Trvalé projekty, panorama a solo jsou desktopová doplnění. Současná PWA není tímto obrázkem prohlášena za stejně vybavené Studio.

**16 Looper** — První smyčka, další vrstvy, vrácení poslední vrstvy a bezpečné vymazání. Velká tlačítka a jednoznačné stavy.

Vrácení vrstvy a trvalé uložení session je nutné dopracovat podle roadmapy, nikoli odvodit z pouhé přítomnosti ovladače na obrázku.

### 05 — Hudební knihovna

**17 Akordy** — Hledání, kategorie a zrcadlení pro leváky. Detail akordu má prstoklad, poslech a vazbu na cvičení.

**18 Stupnice** — Tonika, typ stupnice, poloha na hmatníku a ladění; kořenové tóny se odlišují i tvarem, nejen barvou.

**19 Riffy** — Výběr riffu otevře tabulaturu, poslech a přímé spuštění tréninku s tempem a smyčkou.

**20 Noty a tabulatury** — Soubory Guitar Pro a MusicXML; výběr stopy, zoom, přehrávání a smyčka taktů. Otevírání přes systémový dialog.

### 06 — Hry a AI tutor

**21 Hmatník** — Odpovědi lze vybírat myší i klávesnicí; skóre a nápověda nenarušují zobrazení hmatníku.

**22 Sluch** — Opakovaný poslech, výběr intervalu a vysvětlení odpovědi; přehrávání vyžaduje pouze zvukový výstup.

**23 AI — rozhovor** — Konverzace používá text a odsouhlasený kontext. Klíč se zadává v nastavení; chybějící připojení nebo klíč mají jasný stav.

**24 AI — plán** — AI návrh si uživatel prohlédne a upraví před použitím; uložené položky vedou na konkrétní cvičení.

### 07 — Profil a nastavení

**25 AI — zpětná vazba** — Zpětná vazba výslovně vychází ze statistik a poznámek, nepředstírá rozbor zvuku.

**26 Moje kytara** — Profil nástroje je součástí nastavení; změny ladění jsou propojené s ladičkou, knihovnou i doporučeními.

**27 Pokrok** — Přehled odlišuje aktivní cvičení od pouhého otevření obrazovky; zahrnuje i hraní v režimu Live.

**28 Nastavení** — Systémové škálování, čitelné kontrasty, klávesnice a čtečky obrazovky. Nastavení nepřebírá webové přepínače mobilního rozložení.

### 08 — Zařízení a systémová okna

**29 Zvuková zařízení** — Výběr ovladače, vstupů a výstupů; velikost bufferu se nastavuje podle zařízení. Měření odezvy je oddělené od odhadu ovladače.

**30 Zálohy a soubory** — Navržená desktopová záloha může zahrnout nahrávky; API klíče se do ní automaticky nepřidávají. Obnova předem ukáže rozsah změn.

**31 První spuštění** — Průvodce umožňuje dokončit nastavení bez připojené kytary; každý krok lze později změnit.

**32 Export nahrávky** — Příprava exportu nabídne formát a rozsah; cílovou složku vybere skutečný systémový dialog. Přepsání existujícího souboru vyžaduje potvrzení.

## Pokrytí současné aplikace

Současný repozitář obsahuje 13 hlavních položek obrazovek a čtyři staré adresy s přesměrováním. Ty nejsou další nezávislé funkce:

| Současná část | Pohledy v návrhu |
| --- | --- |
| Domů | 01 |
| Live | 02 |
| Ladička | 03 |
| Metronom | 04 |
| Cvičení a původní YouTube | 05–09 |
| Metal | 10–12 |
| Aparát | 13 |
| Nahrávání včetně původního Studia a Looperu | 14–16 |
| Knihovna | 17–19 |
| Noty | 20 |
| Hry | 21–22 |
| AI tutor | 23–25 |
| Profil včetně původní Kytary | 26–30 |
| První spuštění | 31 |
| Systémový export | 32 |

Výběrové přepínače, přesné chybové stavy a všechny varianty dialogů nejsou samostatné obrázky. Řídí se společnými pravidly a budou se dopracovávat při implementaci.

## Záměrné změny oproti současné PWA

- Denní plán vede přímo na konkrétní úlohu; návrh nevrací uživatele do obecného seznamu.
- Aktivní hraní v Live se má zahrnout do pokroku; čas na otevřené obrazovce sám o sobě není důkaz hraní.
- Nahrávky se ukládají do zvolené složky a návrh záloh zahrnuje jejich volitelné přibalení.
- Export zálohy standardně neobsahuje AI klíč. Způsob bezpečného uchování klíče v systému bude řešen při implementaci.
- Nastavení zvuku rozlišuje ovladač, vstup a výstup. Dostupné hodnoty bufferu a vzorkování určuje skutečné zařízení.
- Vzhled zůstává přístupný i v systémovém vysokém kontrastu; ozdobné materiály nesmějí zakrýt fokus a význam stavu.

## Co obrázky nepotvrzují

Obrázky jsou koncepční vizualizace. Nejsou důkazem běžícího WinUI programu, dostupnosti ASIO ovladače, funkčního exportu ani dosažené latence. Typografie, drobné popisky a přesné systémové dialogy se při implementaci musí řídit touto specifikací a skutečnými komponentami Windows.

Podklad pro volbu Windows rozhraní: [Microsoft WinUI](https://github.com/microsoft/microsoft-ui-xaml).


## Native implementation differences (W36)

The implemented Windows views use real WinUI controls and service data. Live uses adaptive columns; the common session/plan footer remains visible. AI plan task selection uses human names. The notation renderer is native AlphaSkia, with procedural MIDI sound. YouTube remains the isolated official WebView2 player. The [32-view matrix](../w36/view-matrix.json) and Windows artifact gallery distinguish implemented behavior from unperformed manual/hardware acceptance.
