# D02 — specifikace desktopového UX a Vintage grafiky

Stav: návrh rozložení. [Společná galerie 32 pohledů](design-gallery.html)
obsahuje ukázkové údaje a nehraje zvuk. Runtime Windows se ověří v D03.
Vychází ze všech 32 funkcí z D01, nikoli z nového seznamu funkcí.

## Pracovní plocha a hustota

Výchozí pracovní okno obsahuje systémový rám, menu, kompaktní toolbar,
knihovnu, dokumentové záložky, inspektor, volitelné nástroje a transport.
Obsah se skládá z oblastí s vlastní životností. Kontextové nastavení je
vedle práce; související akce jsou u obsahu, nikoli na konci dlouhé stránky.

Návrhový rozměr pracovní plochy je 1440×900 DIPs. Kontrolní malé okno
1280×720 používá kompaktní oblasti; ještě menší work area skryje inspektor
a zúží/skryje knihovnu, s explicitními příkazy pro jejich zobrazení.
To nejsou minimální podporované velikosti fyzické obrazovky; skutečný
fitting se ověří podle monitoru, rámu a DPI v D03/D15.

| Oblast | Návrhová velikost | Chování |
| --- | --- | --- |
| Systémový rám | Podle Windows | Skutečný titulek, Snap, resize, minimalizace a systémové menu |
| Menu / toolbar | Přibližně 32 / 40 DIPs | Jasné příkazy Soubor/Úpravy/Zobrazení/Nástroje/Okno/Nápověda; toolbar podle dokumentu |
| Knihovna | Výchozí 220 DIPs, upravitelná | Kategorie, hledání, položky; vlastní scroll; otevření aktivuje dokument |
| Inspektor | Výchozí 240 DIPs, upravitelný | Výběr aktivního dokumentu; nezaměnit s globálním nastavením zvuku |
| Dokument | Zbytek plochy | TabView, konkrétní editor nebo cvičební relace, vlastní scroll/zoom |
| Nástroje | Volitelná oblast 140–240 DIPs | Výběr nástroje, detach/attach; nesmí vytlačit hlavní akce v malém okně |
| Transport / stav | Kompaktní dvě řádky | Konkrétní zdroj, běžné ovládání, record/stop all a informace o poslechu |

Pro velké projekty/partitury má hlavní dokument přednost. Knihovna a
inspektor se dají skrýt. Nástroj může být plovoucí na vedlejším monitoru.
Přesun nebo změna velikosti nemění hudební nastavení.

## Okna a jejich kompozice

| Rodina | Kompozice | Důležité akce |
| --- | --- | --- |
| Live | Velký tuner, metronom a aparát vedle sebe, viditelný poslech a záznam, detail signálové cesty | Spustit zvuk, zapnout poslech, record, stop all, F11 |
| Ladička | Dominantní nota a gauge, reference/ladění hned vedle, kalibrace jako detail | Vybrat ladění, referenční tón, kalibrace |
| Metronom | Očíslované doby/kyvadlo, velké BPM, tap/play, takt; detail rytmu pod přístrojem | Start/stop, tempo, Doby/Kyvadlo, předtakt |
| Aparát | Kompaktní hlavní štítek, řetězec efektů, knoflíky zvoleného efektu | Bypass, preset, editace/uložení presetu |
| Looper | Stav relace a délka, vrstvy s úrovněmi, record/overdub/play/save | Start/stop kvantizace, vrstvy, uložení |
| Skladba / nahrávka | Timeline a waveform, A/B a značky; source detail v inspektoru | Play/pause, loop, seek, export a práce se značkami |
| Studio | Timeline, hlavičky stop a regiony, mixer dole; vlastnosti výběru vpravo | Import, připojit záznam, editovat stopu, undo, uložit/export |
| Noty | Notová plocha zabírá centrum; track/zoom/bar lišta nahoře | Výběr stopy/taktu, follow, loop, play |
| Lekce / trénink | Kroky nebo cíl vlevo, hlavní úloha uprostřed, konfigurace/result v inspektoru | Přesná návaznost úlohy, start/pause/stop hodnoticí relace |
| Knihovna obsahu | Kategorie a konkrétní položka; diagram/prstoklad/noty v centru | Hledání, otevřít detail nebo trénink |
| Video | Video a compact A/B/rate lišta; saved loops v místním panelu | Načíst, play/pause, loop, v prohlížeči |
| AI | Konverzace / konkrétní plán / source statistics ve vlastním dokumentu | Odeslat, zrušit, výslovně použít plán |
| Nastavení | Samostatné okno s kategoriemi a formuláři, nikoli přístrojový rack | Použít/ověřit změnu, příslušné systémové dialogy |
| Průvodce / export | Malý vlastněný dialog s jasným objektem a výsledkem | Další/zpět/dokončit nebo export/cancel; stav operace nezávislý na dialogu |

Plovoucí nástroj má samostatné minimum podle vlastního obsahu; nepřevzít
jednotných 640×480 z dnešního WindowRectangle.FitTo. Při malé velikosti
zůstává základní ovládání viditelné, detail může mít vlastní scroll.
Galerie ukazuje obsah okna; skutečnou modalitu/Alt+Tab určí ověřený D03.

## Vizuální pravidla

Rocker používá teplou grafitovou pracovní plochu, tlumenou měď pro hlavní
akci a výběr, krémové přístroje, jemné ořechové bočnice a úsporné detaily.
Jeden modul má jeden rám. Velké povrchy knihovny/editoru nenesou textury.

Tituly, toolbar a seznamy používají dostupné systémové písmo Segoe UI;
instrumentální popisky menší kapitálky s rozestupem a číselné readouty.
Dokumentové názvy a hodnoty jsou čitelné; názvy souborů se dají vybrat a
celý zkrácený název je v tooltipu. Číslo/název je doplněním kontrolky,
nikoli pouze barevnou signalizací.

Zachovat skutečné nativní NumberBox/Slider/CheckBox pod retro vizuálním
zpracováním. Dekorativní páčka je uvnitř skutečného aktivního ovladače.
Měřáky mají jednotku peak dBFS, ladička centy a metronom skutečnou fázi.
Návrhová galerie používá přímo označené umělé hodnoty.

Světlo a tmavý režim mají společnou hierarchii. HC odstraní texturu/grain,
použije systémové barvy a jasný fokus. Reduced motion ponechá změny čísel
dob bez kývání. Chování systémových oken se nereplikuje webovým modalem
v produkční aplikaci; modal v galerii pouze zvětšuje návrhový obrázek.

## Monitoring: konkrétní UX oprava

Přímo u ovladače poslechu i výsledného mixu ukázat důvod:

1. Zařízení neběží → „Zvuk je zastavený“ a Spustit zvuk.
2. Vstup běží, poslech vypnutý → „Poslech kytary je vypnutý“ a Zapnout poslech.
3. Poslech zapnutý, gain 0 → „Hlasitost poslechu je na nule“ a skutečný gain.
4. Poslech zapnutý, input chybí → konkrétní vstup/karta a Nastavit zvuk.
5. Poslech zapnutý, tichý fyzický vstup → vysvětlit chybějící signál, ne tvrdit chybu ovladače.
6. Bypass → čistý zvuk, ne vypnutý monitoring.

Signál za aparátem může existovat při vypnutém poslechu. Metronom/skladba
mohou v mixu hrát bez kytary. Text proto popisuje stav kytarové větve,
nikoli bez důkazu ztlumení celého mixu. Direct Monitor na kartě je oddělená
cesta a aplikace nemůže potvrdit jeho fyzický přepínač.

Galerie má přepínač ukázkového stavu „Poslech vypnutý / zapnutý“. Mění
pouze návrh a vysvětlení. Nespouští audio a neukládá nastavení aplikace.

## Úlohy a přechody

| Scénář | Výchozí akce | Zachování práce |
| --- | --- | --- |
| Otevřít lekci a metronom | Dvojklik knihovny otevře konkrétní dokument; Nástroje → Metronom zobrazí stávající nástroj | Změna pohledu nepřeruší otevřenou lekci; start hodnoticí relace výslovný |
| Naladit a hrát | Vyvolat Live; Spustit zvuk, explicitně zapnout poslech | Ladění/presety sdílené, druhý vstup není automaticky přimíchaný |
| Nahrát kytaru | Vybrat dry/processed/mix a record v Live/transportu | Take viditelný v hlavním okně i po zavření Live |
| Noty na druhém monitoru | Kontext záložky → Otevřít v samostatném okně; stejná akce drag/drop | Tatáž relace, takt/zoom/loop; změna hostitele neznamená play |
| Editovat Studio | Vybrat projekt, stopu/region; inspektor míří na tento výběr | Undo a save patří projektu; žádné implicitní přepnutí druhé časové osy |
| Otevřít jinou skladbu při záznamu | Otevřít dokument, výslovné Play požádá o změnu zdroje | Před mediální mutací vyřešit capture/looper konflikt |
| Zavřít aplikaci | Main X nebo Soubor → Ukončit | Jedna přejímka všech dokumentů/operací; Cancel nepřipustí částečné zavření |

## Přístupnost a systémové chování

Klávesnicové alternativy jsou povinné pro přesun záložky/nástroje, resize
panelu, skrývání a obnovu. F6 přechází mezi oblastmi. Ctrl+Tab mění dokument
v aktuálním hostiteli, Ctrl+W jeho zavření. Menu Okno nabízí seznam skutečných
oken s aktivací a „Připojit do hlavního okna“. Fokus po attach/detach jde
do přemístěného dokumentu a po dialogu zpět na vyvolávající ovladač.

Přehrávání neovládat globálním systémovým hookem. Mezerník respektuje
textová pole a nativní ovladače. Toolbar dostupnost odpovídá CommandRouter,
ne domněnce podle poslední stránky. Obnovená topmost okna mají jasný přepínač
a „Obnovit rozložení“ dostupné v hlavním menu i při skrytých oblastech.

## Podmínky dokončení D02

- Všech 32 ID má samostatný návrh a cíl podle inventury.
- Návrhy se dají prohlédnout společně, filtrovat, zvětšit a přepnout theme.
- Live/monitoring mají ukázku vypnutého poslechu a konkrétní akci.
- Galerie zobrazuje prázdný stav, chybějící zařízení a rozpracovaný projekt jako odlišné situace.
- Pracovní plocha, Live, čtyři nástroje a systémová okna používají společná pravidla.
- Označení návrhů/ukázkových údajů je viditelné; galerie neslibuje Windows runtime nebo fyzický zvuk.
- Skutečná okna, přesuny, dialog ownership a callback lifetime se ověří až v D03 a dalších implementačních krocích.
