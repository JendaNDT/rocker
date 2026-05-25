# Plán sloučení obrazovek — Rocker

Plán konsolidace informační architektury aplikace. Vznikl 2026-05-25 ze
zpětné vazby reálného uživatele a navazující úvahy o UX.

## Jak plán číst

- Platí vibecoder workflow (`AGENTS.md`, `SKILL.md`): diskuze před kódem,
  krok po kroku, čekat na „piš". Tenhle plán je podklad — kroky se před
  psaním ještě domluví.
- Stavět po fázích A → E. Po každém kroku `npx tsc --noEmit` + `npx vitest
  run`, report výsledku.
- Po dokončení fáze: aktualizovat `PROJECT_STATUS.md`, připravit commit+push.
- Build se v sandboxu neověřuje (EPERM quirk — viz `AGENTS.md`).
- **Pozor na routing.** Appka je PWA s offline cache a service workerem.
  Staré routy se neruší natvrdo — nahradí se přesměrováním, aby uložené
  odkazy a cache nevedly do prázdna.

## Kontext — proč

Appka má kolem 16 položek v hlavní navigaci. Pro začátečnickou PWA je to
hodně. Plán slučuje příbuzné obrazovky a tříštěné činnosti — méně položek
v navigaci, soudržnější celky. Nic se neztrácí, jen přeskupuje; navíc
přibývá jedna nová funkce (záznam v Aparátu).

---

## Fáze A — Profil nástroje do Profilu ✅ HOTOVO (2026-05-25)

„Kytara" (`/instrument`) je konfigurační karta — vyplníš jednou, pak skoro
nesaháš. Není to denní nástroj. Přesune se jako sekce do Profilu, který
podobné věci už sdružuje (Vzhled, Záloha dat, Zvuk a vstup). Položka
„Kytara" zmizí z hlavní navigace; route `/instrument` se nechá jako
přesměrování na Profil.

**Riziko:** nízké — přesun obsahu obrazovky, logika profilu nástroje beze
změny.

**Hotovo (2026-05-25):** položka `/instrument` odebrána ze `screens.ts`
(zdroj navigace); v `profile/page.tsx` sekce „Kytara" (`ProfileCard` +
`ToneAdvisor`) na plnou šířku nad nastaveními; `instrument/page.tsx`
klientské přesměrování na `/profile`.

## Fáze B — Metronom: kontextová dostupnost ✅ HOTOVO (2026-05-25)

Metronom **zůstává** samostatným nástrojem (vlastní obrazovka) — používá se
i mimo lekce. Navíc se zpřístupní přímo na cvičebních obrazovkách: kompaktní
vyvolatelný metronomový panel, ať uživatel nemusí odcházet z cvičení.

**Riziko:** střední — nová sdílená komponenta; metronomový engine se nemění.

**Hotovo (2026-05-25):** komponenta `MetronomeWidget` — sbalovací kompaktní
metronom (BPM ±, naťukání tempa, `BeatIndicator`, přehrát/stop) na obrazovce
Cvičení. Sdílí engine i uložené nastavení s plnou obrazovkou Metronom.
Rozhodnuto: widget metronom **nezastavuje** při odchodu z Cvičení — stejně
jako plný metronom, klik běží dál napříč navigací (je to tentýž metronom).
Zatím jen na Cvičení; Metal vynechán (Power Chord trainer engine řídí sám).

## Fáze C — Riff trainer + YouTube → „Hraj se skladbou" ✅ HOTOVO (2026-05-25)

Riff trainer (přehrávání vlastního MP3/WAV se zpomalením a smyčkou) a
YouTube přehrávač dělají **tutéž činnost ze dvou zdrojů**. Sloučí se do
jednoho nástroje „Hraj se skladbou" s přepínačem zdroje (vlastní soubor /
YouTube).

**Riziko:** střední–vyšší — dva různé přehrávací enginy (souborový vs.
YouTube IFrame API) pod jedním UI; sahá se na dvě funkční věci.

**Hotovo (2026-05-25):** YouTube přehrávač vytažen do komponenty
`YouTubePlayer`. Záložka „Riff trainer" v Cvičení přejmenována na „Hraj se
skladbou" + přepínač zdroje (Vlastní soubor / YouTube). `/youtube` odebrán
ze `screens.ts`, route přesměrovává na `/practice`. Enginy zůstaly oddělené
(souborový vs. YouTube) — sloučeno na úrovni jednoho místa a přepínače,
ne hluboká fúze. Trade-off: přepnutí zdroje resetuje druhý přehrávač.

## Fáze D — Nahrávací nástroje do jedné sekce ✅ HOTOVO (2026-05-25)

Nahrávky (`/recorder`), Studio (multi-track) a Looper jsou tři samostatné
obrazovky, které všechny „zachytávají a vrství zvuk". Sloučí se do jedné
sekce „Nahrávání" se záložkami. Tři položky v navigaci → jedna; tři funkční
komponenty zůstanou, jen je zastřeší jedna obrazovka s přepínačem.

**Riziko:** střední — routing a navigace; samotné funkce zůstávají.

**Hotovo (2026-05-25):** `/recorder` je sjednocená obrazovka „Nahrávání" se
záložkami Nahrávky / Studio / Looper. `/studio` a `/looper` odebrány ze
`screens.ts`, route přesměrovávají na `/recorder`. Funkční komponenty
(`Studio`, `LoopPedal`, recorder) beze změny.

## Fáze E — Nahrávání v Aparátu ✅ HOTOVO (2026-05-25)

Nová funkce: záznam **zaaparátovaného** zvuku. Dnešní Nahrávky zaznamenávají
suchý vstup; v Aparátu chce uživatel nahrát to, co slyší po efektech.
Odbočka z výstupu efektového řetězce (`MediaStreamAudioDestinationNode`) →
`MediaRecorder` → take se uloží do existujícího úložiště nahrávek
(IndexedDB, Bod 11) a objeví se v sekci Nahrávání.

**Riziko:** střední — nová audio cesta, ale staví na existující nahrávací
infrastruktuře.

**Hotovo (2026-05-25):** `AmpChain` má `MediaStreamAudioDestinationNode`
na výstupu, zpřístupněný jako `recordingStream`; konstruktor bere
`AudioContext`. `AmpStudio` má tlačítko Nahrát/Zastavit s počítadlem času,
take ukládá přes sdílený `recorder` a `recordingsStore` (název „Aparát N").
Nahrávání se zastaví při vypnutí efektu i odchodu z obrazovky.

---

## Pořadí, velikosti, riziko

| Fáze | Co | Velikost | Riziko |
|---|---|---|---|
| A | Kytara → Profil | S | ✅ hotovo |
| B | Metronom kontextově | M | ✅ hotovo |
| C | Riff trainer + YouTube | L | ✅ hotovo |
| D | Nahrávací nástroje do jedné sekce | M–L | ✅ hotovo |
| E | Nahrávání v Aparátu | M | ✅ hotovo |

Návrh: stavět A → E (od nejmenšího/nejméně rizikového). A je rozehřátí,
E je jako poslední, protože nová nahrávka logicky zapadne do sjednocené
sekce Nahrávání z fáze D.

## Otevřené otázky

1. **Fáze B** — přesná podoba kontextového metronomu (sbalovací panel,
   na kterých obrazovkách).
2. **Routy** — zrušené obrazovky nechat jako přesměrování (návrh: ano,
   kvůli PWA cache a uloženým odkazům).
3. **Pořadí** — A → E, nebo některou fázi přednostně (uživatel nejvíc
   chtěl záznam v Aparátu — fáze E)?
