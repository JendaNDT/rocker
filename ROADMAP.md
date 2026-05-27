# Roadmapa — Rocker

Číslovaná roadmapa stavby aplikace. Technické pozadí je v `ARCHITECTURE.md`.

## Jak roadmapu číst

- Aplikace se staví **shora dolů po bodech**. Aktuální bod = nejnižší
  nedokončené číslo, pokud se nedohodneme jinak.
- Každý **bod** je samostatná funkční jednotka. Při vibecodingu se ještě
  rozseká na malé kroky — po každém kroku ti popíšu co je hotové, spustím
  kontrolu typů a testy, a počkám na tvoje "piš".
- Stav: ⬜ čeká · 🔨 rozpracováno · ✅ hotovo
- **Cíl prvního milníku ("Široké MVP")** = body 1–13. Po nich je aplikace
  plně použitelná na denní cvičení. AI a pokročilý zvuk jsou až potom.

---

## Fáze 0 — Základ projektu

### ✅ Bod 1 — PWA skořápka
Next.js (App Router) + TypeScript + Tailwind. Statický export. `manifest.json`,
ručně psaný service worker, instalovatelnost na PC i Android, offline cache.
Nasazení na Vercel/Cloudflare Pages s HTTPS. Spodní/boční navigace, dark mode
jako výchozí, prázdné stránky pro všechny routes.
*Výsledek:* prázdná, ale instalovatelná appka, kterou jde otevřít na mobilu.
*Stav:* hotovo — 9 obrazovek proklikatelných, build i `tsc` ověřené.

### ✅ Bod 1b — Přepínání zobrazení a tlačítko nastavení
Doplněno na žádost: třístavový režim zobrazení (Automaticky / Počítač / Mobil)
uložený v prohlížeči, přepínač na obrazovce Profil, trvalé tlačítko nastavení
(ozubené kolo) vpravo nahoře na všech obrazovkách.
*Stav:* hotovo.

---

## Fáze 1 — Audio jádro a nástroje (MVP, část A)

### ✅ Bod 2 — Audio engine a výběr zařízení
Centrální `AudioEngine`: `AudioContext`, `getUserMedia`, výběr vstupního
zařízení (M-Track Solo na PC / mikrofon na mobilu), kontextová žádost
o oprávnění, změření a uložení latence, kalibrace vstupní úrovně.
*Výsledek:* aplikace "slyší" kytaru a v nastavení jde vybrat M-Track Solo.
*Stav:* hotovo — engine, výběr zařízení, indikátor úrovně a latence; build
i `tsc` ověřené. Reálný test s kytarou je na Jendovi.

### ✅ Bod 3 — Ladička
Detekce výšky tónu přes Pitchy (na hlavním vlákně, ne AudioWorklet — viz
poznámka níže). Vedený režim podle ladění i chromatický režim. Displej
s odchylkou v centech, confidence gate. Předvolby ladění: standard,
Drop D/C/B, Eb standard, D standard.
*Výsledek:* funkční ladička — první věc, kterou appka denně otevřeš.
*Stav:* hotovo — build, `tsc` i testy (19) ověřené. Reálný test s kytarou
je na Jendovi.
*Poznámka:* detekce běží na hlavním vlákně přes `AnalyserNode` + Pitchy
(ne v AudioWorkletu, jak původně počítala architektura) — pro ladičku je to
ověřený standard a obchází to balicí potíže s Turbopackem.

### ✅ Bod 4 — Metronom
Sample-accurate scheduler (lookahead, žádný `setTimeout`). 40–240 BPM, takty
4/4, 3/4, 6/8, akcent na první dobu, subdivize, tap tempo, haptická odezva
(Web Vibration API na Androidu).
*Výsledek:* spolehlivý metronom bez driftu.
*Stav:* hotovo — build, `tsc` i testy (28) ověřené. Reálný test je na Jendovi.
*Pozn.:* metronom byl později rozšířen ve dvou vlnách nad rámec
původního zadání — viz sekce „Mimo roadmapu" níže (Rozšířený metronom
a Šestifázový upgrade s drum modem, swingem, count-in, polyrytmem,
kyvadlem a továrními presety).

### ✅ Bod 5 — Knihovna akordů a stupnic
SVG diagramy akordů (open, barré, power), prstoklad, barré indikátor, audio
preview. Verze pro pravou/levou ruku. Stupnice: pentatonika, dur/moll,
harmonická moll, frygická dominantní.
*Výsledek:* referenční knihovna, ve které si uživatel najde, co hrát.
*Stav:* hotovo — 26 akordů, 6 stupnic, build/`tsc`/testy (39) ověřené.
Tvary v drop ladění zatím vynechány (pozdější doplněk) — viz Backlog.

---

## Fáze 2 — Učení a cvičení (MVP, část B)

### ✅ Bod 6 — Lekční engine a Stage 1–2 (rozšířeno o Stage 3)
Datově řízený engine lekcí. Obsah Stage 1 (open akordy + strumming) a Stage 2
(power chordy + palm mute), 12 krátkých lekcí. "Try it" režim s detekcí přes
Pitchy (kontrola tónu); u akordů režim „hraj se mnou".
*Výsledek:* strukturovaná cesta pro úplného začátečníka.
*Stav:* hotovo — build/`tsc`/testy (47) ověřené.

### ✅ Bod 7 — Riff trainer (slow-down + loop)
Přehrávač uživatelových MP3/WAV se zpomalením bez změny výšky (nativní
`preservesPitch`, ne SoundTouch.js — viz poznámka). A/B loop, count-in,
postupné zrychlování tempa (+5 % každé 2 opakování), zobrazení waveformy.
*Výsledek:* nástroj na pilování těžkých pasáží z vlastních nahrávek.
*Stav:* hotovo — build/`tsc`/testy (51) ověřené.
*Poznámka:* zpomalení používá nativní `preservesPitch` prohlížeče místo
knihovny SoundTouch.js — jednodušší a pro cvičení dostačující.

### ✅ Bod 8 — Surgical Practice Mode (Přesný trénink)
Chytrá tréninková logika z rešerše: průchod cvičením tón po tónu → detekce
chybového místa → izolace 2–4 tónů do drillu → 3 čisté repetice → návrat.
Zabraňuje fixaci chyb. Verze je „self-paced" (ve vlastním tempu) — viz poznámka.
*Výsledek:* appka se chová jako trenér, ne jen hodnotící skóre.
*Stav:* hotovo — build/`tsc`/testy (59) ověřené.
*Poznámka:* místo rytmického hodnocení v plném tempu (v prohlížeči nespolehlivé)
jde o self-paced detekci jednotlivých tónů — zachovává jádro nápadu spolehlivě.

### ✅ Bod 9 — YouTube Practice Player
YouTube IFrame API: vložení videa přes URL, A/B loop (hlídáním času),
zpomalení `setPlaybackRate`, štítkování sekcí a uložení smyček.
*Výsledek:* cvičení podle libovolného YouTube tutoriálu/skladby.
*Stav:* hotovo — build/`tsc`/testy (65) ověřené.
*Poznámka:* uložené smyčky jdou do localStorage (malá JSON data), ne do
IndexedDB; vlastní stručné typy YT API místo `@types/youtube`.

---

## Fáze 3 — Pokrok, nahrávání, doladění (MVP, část C)

### ✅ Bod 10 — Sledování pokroku
Odcvičené minuty (automatické měření času na cvičebních obrazovkách),
streaky a 11 achievementů. Denní ~15min plán cvičení a dashboard na úvodní
obrazovce. Detail pokroku v Profilu.
*Výsledek:* důvod vracet se každý den.
*Stav:* hotovo — `tsc` i testy (118) ověřené.
*Poznámka:* pokrok je uložený v localStorage (Zustand persist), ne
v IndexedDB — jde o malá strukturovaná data; IndexedDB zůstává pro velké
bloby (nahrávky, Bod 11). BPM rekordy přesunuty do Bodu 12: teprve Power
Chord / Gallop Trainer proti metronomu vyrobí dvojici cvičení + tempo.
Plumbing (`recordBpm`, achievement „Rychlé prsty") je připravený.

### ✅ Bod 11 — Nahrávání
MediaRecorder, nahrání take-u, přehrání, A/B srovnání dvou take-ů, uložení
blobů do IndexedDB, export. Označování míst v nahrávce.
*Výsledek:* sebehodnocení — slyšíš svůj pokrok.
*Stav:* hotovo — `tsc` i testy (137) ověřené. Reálné nahrávání je na Jendovi.
*Poznámka:* IndexedDB přes knihovnu `idb`; vrstva v `src/lib/recordingsDb.ts`,
jeden object store `recordings` (blob i metadata pohromadě). Délka nahrávky
se měří wall-clock, protože webm/opus bloby nemají v hlavičce délku.

### ✅ Bod 12 — Rock/metal moduly
Power Chord Trainer (přesouvání pozic s metronomem), Palm Mute Studio
(detektor utlumení podle harmonického obsahu), Gallop Trainer (Iron Maiden
rytmus), knihovna metalových riffů s tabem a slow-down.
*Výsledek:* to, čím se appka odlišuje — rock/metal-first pro začátečníky.
*Stav:* hotovo — obrazovka Metal se třemi trenažéry (Power Chord, Gallop,
Palm Mute Studio) a sekce Riffy v Knihovně (14 riffů, tab + zpomalené
přehrávání). `tsc` i testy ověřené. Reálný test s kytarou je na Jendovi.
*Pozn.:* knihovna riffů je hybridní — 2 ikonické riffy + 12 originálních
ve stylech rock/metal (vědomá odchylka od původního seznamu pojmenovaných
skladeb kvůli ověřitelnosti tabů a autorským právům). Power Chord a Gallop
Trainer napojily BPM rekordy z Bodu 10 (`recordBpm` → achievement „Rychlé
prsty"). Palm mute detektor: prahy v `useMuteDetector.ts` můžou po reálném
testu s kytarou potřebovat doladit.

### ✅ Bod 13 — UX dokončení MVP
Zen režim (skrytí gamifikace), onboarding ("první riff za 5 minut"), audio
kalibrace v úvodu, Wake Lock, landscape "stojan" režim, kompletní česká
lokalizace, přepínač notace H/B, upozornění na přestávku po 45 min,
bezpečné fallbacky pro slabá zařízení.
*Výsledek:* **konec Širokého MVP — aplikace připravená pro beta testery.**
*Stav:* hotovo — Wake Lock na cvičebních obrazovkách, upozornění na
přestávku po 45 min, Zen režim, přepínač notace H/B (ladička, knihovna,
trenažéry), Stojan režim (celoobrazovkový tab riffu), onboarding s úvodní
audio kalibrací. Audit lokalizace (čistá) a fallbacků: `webkitAudioContext`
fallback, ošetření výpadku IndexedDB. `tsc` i 230 testů ověřené.

> Po Bodu 13 doporučuju beta test s 5–10 lidmi (začátečníci + středně
> pokročilí) a teprve podle zpětné vazby pokračovat dál.

---

## Fáze 4 — AI vrstva (po MVP)

### ✅ Bod 14 — AI tutor
AI chat "zeptej se", generátor denních cvičení, zpětná vazba na cvičení.
Režim BYOK — uživatel zadá vlastní bezplatný Gemini API klíč.
*Stav:* hotovo. Obrazovka AI tutor (`/tutor`) má tři režimy: **chat „zeptej
se"** (navržené otázky, ukládaná historie, streamování odpovědí), **generátor
cvičení** (`planGenerator` + `PlanPanel` — plán na míru) a **zpětná vazba
z dat** (`feedback.ts` + `FeedbackPanel` — AI poradna nad pokrokem). AI
vrstva v `src/lib/ai/`, stores `aiStore` a `chatStore`. `tsc` i testy
ověřené; chat ověřen reálným testem.
*Rozhodnutí:* poskytovatel **Google Gemini** (free tier, bez platební
karty), **BYOK**, voláno **přímo z prohlížeče bez serveru** — nativní
Gemini endpoint posílá CORS hlavičky, proxy není potřeba; appka zůstává
čistá statická PWA. „Zpětná vazba na nahrávku" pojata jako rada nad
strukturovanými daty o cvičení, ne jako analýza surového zvuku.

---

## Fáze 5 — Pokročilý zvuk

### ✅ Bod 15 — AlphaTab integrace
Prohlížeč Guitar Pro / MusicXML souborů s přehráváním, scroll s kurzorem,
loopem a zvýrazněním na hmatníku. Uživatel nahrává vlastní soubory.
*Stav:* hotovo. Obrazovka `/notation` („Noty"): nahrání vlastního Guitar
Pro / MusicXML souboru, vykreslení tabulatury i not (`@coderline/alphatab`),
přehrávání přes MIDI syntezátor (SONiVOX soundfont) s kurzorem, A/B smyčka,
zpomalení tempa. `tsc` i testy ověřené; reálný test v prohlížeči je na
Jendovi. Zvýraznění běží v samotném zápisu (kurzor) — samostatný hmatníkový
vizualizér součástí není.
*Rozhodnutí:* vykreslování i audio na hlavním vlákně (`core.useWorkers:
false`, player ScriptProcessor výstup) — obchází bundlování AlphaTab Web
Workeru a Audio Workletu, jejichž oficiální webpack plugin s Turbopackem
nesedí. Font (Bravura) a soundfont (SONiVOX) ručně v `public/alphatab/`.

### ✅ Bod 16 — Ampsim a efekty
Kytarové efekty a simulace aparátu ve Web Audio (JS-Rocks / Pedalboard.js).
Generické názvy presetů. Primárně pro PC + M-Track Solo; na Androidu jen
"tone preview", ne live monitoring (latence).
*Stav:* kód hotový. Obrazovka `/amp` („Aparát"): živý vstup kytary →
efektový řetězec `AmpChain` (pre-gain, zkreslení, 3-pásmový EQ, filtrová
simulace reproboxu, delay) → výstup. Presety s generickými názvy (Čistý /
Crunch / Lead / Metal), posuvníky všech parametrů. `tsc` i testy ověřené.
**POZOR: zvuk ani latence nejsou ověřené** — postaveno vědomě naslepo na
žádost (Jenda zatím nemohl testovat). Až bude moct, otevře „Aparát" na
Macu s kytarou a sluchátky; podle výsledku se zvuk doladí, nebo přehodnotí
živý monitoring.
*Rozhodnutí:* efektový řetězec přímo na uzlech Web Audio, žádná knihovna
(JS-Rocks/Pedalboard.js jen jako inspirace). Simulace reproboxu filtrová,
ne přes IR soubor (bez externí závislosti a licence). Audio na hlavním
vlákně (ScriptProcessor výstup) — obchází audio worklet, konzistentní
s AlphaTabem.

### ✅ Bod 17 — Multi-track nahrávání a loop pedál
Nahrávání více stop s overdubem, mixdown, emulátor loop pedálu.
*Stav:* kód hotový. Obrazovka `/studio` („Studio") — multi-track recorder:
nahrávání stop přes sebe (overdub), přehrání všech, u stopy ztlumení /
hlasitost / smazání, mixdown a export do WAV. Obrazovka `/looper` — loop
pedál: nahraj smyčku, bezešvě se opakuje, vrstvi přes ni. `tsc` i testy
ověřené. **POZOR: zvuk a overdub nejsou ověřené** — postaveno vědomě
naslepo, čeká test s kytarou (latence způsobí drobný posun vrstev).
*Rozhodnutí:* engine staví na recorderu z Bodu 11. Smyčka se opakuje přes
`loop=true` (bezešvé), WAV export přes `OfflineAudioContext`. Multi-track
projekt žije v paměti session — uložením je export.

### ⬜ Bod 18 — Stem separation
Rozdělení skladby na stopy (zpěv/bicí/basa/kytara) přes externí API
(Moises / LALAL.AI) v režimu BYOK. Nikdy v prohlížeči lokálně.
*Stav:* vědomě odložený. Stem separation API jsou placené (žádný free
tier — na rozdíl od Gemini u Bodu 14) a serverově orientované (nahrání
souboru → async job → stažení stop) — z prohlížeče nejspíš nejdou volat
a vyžadovaly by proxy server. Špatně sedí principu „zdarma, lokálně, bez
serveru". Lze se k němu vrátit, kdyby projekt někdy server získal.

---

## Fáze 6 — Rozšíření

### ✅ Bod 19 — Vizuální učení
3D vizualizér hmatníku (Three.js), hry na zapamatování hmatníku, ear training
(intervaly, kvalita akordů).
*Stav:* hotovo. Obrazovka `/games` („Hry") se dvěma režimy: hmatníková hra
(appka pojmenuje tón, hráč klikne na hmatník) a ear training (rozpoznávání
intervalů a kvality akordů sluchem, přes syntézu z Bodu 5). `tsc` i testy
ověřené. **3D Three.js vizualizér vědomě vynechán** — appka už 2D hmatník
má (Bod 5), 3D verze by přidala velkou závislost s malou učební hodnotou.

### ⬜ Bod 20 — Mirror-AR vizualizér
Z prvního rešeršního dokumentu — kamera + MediaPipe Hands promítá barevné
body prstokladu na zrcadlený obraz. Experimentální, volitelná vrstva.
*Stav:* vědomě vynechaný. Experimentální volitelná vrstva s velkou
závislostí (MediaPipe Hands) a nejistou učební hodnotou — appka už má 2D
hmatník (Bod 5) i hry (Bod 19). Lze se k němu vrátit později.

### ✅ Bod 21 — Profil nástroje Solton Double Cut
"Digitální pas" kytary, tónový poradce (krk/kobylka, gain, EQ tipy).
Profil je poradní, ne dogmatický — uživatel modifikace ručně potvrzuje.
*Stav:* hotovo. Obrazovka `/instrument` („Kytara"): editovatelná karta
profilu (název, tělo, snímače krk/střed/kobylka × humbucker/single/P90,
menzura, ladění, struny, poznámky; Zustand persist) a tónový poradce —
vybereš styl (7 stylů) a appka poradí snímač, gain a EQ přizpůsobené
snímačům profilu. Logika v `src/lib/instrument.ts` (s testy). Poradce jen
radí, nic sám nemění. `tsc` i testy ověřené.
*Rozhodnutí:* profil pojatý jako obecná editovatelná šablona (varianta B)
— žádné specifikace Soltonu se nepředvyplňují (neznámá značka, nešlo
ověřit), uživatel si profil vyplní sám. Výchozí hodnoty jsou jen rozumný
základ k přepsání.

### ⬜ Bod 22 — Sdílení a organizace
Share-a-riff (export riffu jako odkaz), songbook organizér se složkami a štítky.
*Pozn.:* záloha dat (viz „Mimo roadmapu" níže) část „organizace" už pokrývá —
zbývá hlavně share-a-riff a songbook.

---

## Mimo roadmapu — vylepšení po Bodu 21

Práce, která vznikla nad rámec původní číslované roadmapy. Detaily
a rozhodnutí jsou v `PROJECT_STATUS.md`.

### ✅ 3 přepínatelné vzhledy
Témata Studio, Vintage a Glassy, každé ve světlém / tmavém / auto režimu.
Volba v `/profile → Vzhled`. CSS proměnné v `src/app/theme-tokens.css`,
`ThemeProvider` synchronizuje `[data-theme]`/`[data-mode]` na `<html>`.
Všech 58 obrazovek migrováno z `bg-neutral-*` na theme-aware Tailwind třídy.

### ✅ Audit vnitřní logiky + opravy
Kompletní audit audio vrstvy, čisté logiky, AI vrstvy, stores a komponent.
Žádný kritický bug; nalezené drobnosti opraveny ve 4 dávkách.

### ✅ Šumová brána + autokalibrace
Detekce tónů ignoruje vstup tišší než nastavený práh (filtruje okolní ruch).
Citlivost posuvníkem nebo tlačítkem „Změřit pozadí" v Profilu → Zvuk a vstup.

### ✅ Upozornění na novou verzi
Po nasazení appka nabídne lištou načtení nové verze. `sw.js` se při buildu
razítkuje unikátní verzí (`scripts/stamp-sw.mjs`), jinak by prohlížeč změnu
nepoznal.

### ✅ Krok výběru vzhledu v úvodu
Úvodní průvodce má 4 kroky — po uvítání přibyl výběr tématu.

### ✅ Záloha dat
Export/import nastavení a pokroku do JSON souboru (`/profile → Záloha dat`).
Nahrávky (IndexedDB) nejsou součástí — soubor zůstává malý.

### ✅ Nasazení na Vercel
Aplikace je živě na `rocker-rust.vercel.app`, s automatickým deployem
z větve `main`.

### ✅ Rozšířený metronom
Metronom (Bod 4) rozšířen o: 7 taktových měr (2/4, 3/4, 4/4, 5/4, 6/8, 7/8,
12/8), klikací akcentový vzor (každá doba: přízvuk / normální / ztišená),
5 syntetizovaných zvuků (Klasik, Dřevo, Pípnutí, Cvak, Hi-hat), oddělenou
hlasitost metronomu a přízvuku, trenažér tempa (progresivní zrychlování
a „cvičné ticho" — gap trainer) a presety s persistencí nastavení mezi
spuštěními. Detaily v `PROJECT_STATUS.md`.

### ✅ Metronom — šestifázový upgrade (2026-05-27)
Druhá vlna rozšíření metronomu nad rámec první, postavená přes šest
fází po sobě. Detaily a důvody v `PROJECT_STATUS.md`, tady jen
seznam toho, co přibylo:

- **F1 — Per-beat zvuk (drum mode):** každá doba může mít jiný zvuk
  (kick/snare/hi-hat), plus samostatný zvuk pro subdivize. Skrytý pod
  toggle „Pokročilé" v sekci Přízvuky, aby základní UI zůstalo
  jednoduché. `MetronomeConfig.soundPattern` a `subdivisionSound`,
  oboje volitelná pole se zpětnou kompatibilitou.
- **F2 — Swing / Shuffle:** slider 50–75 % posouvá vnitřní subdivize
  beatu (osminy a šestnáctky); beat sám zůstává v čase. Klasický
  Wittner cyklus: 50 % rovné, 67 % jazz triolový, 75 % dotted shuffle.
  Trojky a čtvrtky se nemění.
- **F3 — Count-in (předtakt):** 0, 1 nebo 2 takty „naprázdno" před
  spuštěním. Ramp a gap trainer ignorují záporné takty. UI countdown
  „Předtakt: X/N takt(y)" pod tlačítkem Spustit.
- **F4 — Polyrytmus (sekundární vrstva):** druhá nezávislá smyčka
  v `scheduler()` plánuje N ticks/beat s vlastním zvukem a hlasitostí.
  Klasické 3-proti-4 cvičení. Tichá během count-in a gap silent bar.
- **F5 — Vizuální kyvadlo (Pendulum):** alternativa k BeatIndicatoru,
  polymorfní (Studio chrom, Vintage Solton brass, Glassy neon, default).
  Sinusoidální Wittner easing, jedna sekvence = 2 doby, ±45°. Polish:
  brightness flash na každou akcentní dobu (260 ms, univerzální CSS
  keyframe). Globální preference `visualMode` v
  `metronomeSettingsStore` mimo `config`.
- **F6 — Tovární presety:** 6 read-only presetů využívajících všechny
  předchozí fáze (Blues shuffle s swingem a kick/snare, Metal gallop
  s rampem, Funk groove se snare na 2+4, Waltz 3/4, Jazz swing,
  Polyrytmus 3 proti 4). `PresetPanel` rozdělen na sekce „Tovární"
  a „Moje".

Plus drobný fix `accentVolume` na extra-gain model
(0 % = bez přízvuku, 100 % = max).

### ✅ Responzivní PC rozvržení
Všech 16 obrazovek dostalo rozvržení pro PC — dosud měla appka na počítači
stejný úzký sloupec jako na mobilu. Řízeno stejným přepínačem
Počítač/Mobil/Automaticky jako navigace. Obrazovky bohaté na obsah jsou
na PC dvousloupcové, fokusované nástroje mají umírněnou šířku, knihovna
a noty využijí plnou šířku. Mobil zůstává jednosloupcový. Detaily
v `PROJECT_STATUS.md`.

### ✅ Polymorfní widgety vzhledů (inspirované Soltonem)
Pět widgetů, které dosud vypadaly stejně ve všech vzhledech, dostalo
formu odpovídající aktivnímu tématu. Sdílený hook `useActiveTheme()`
(`src/lib/useActiveTheme.ts`) vrátí téma až po hydrataci, jinak `null` —
widgety mají SSR-safe default fallback.

Fáze 1: **CentsBar** (ladička) — Studio chromová ručka v brass rámu,
Vintage papírová stupnice s inkoustovou tečkou, Glassy frosted track
s neon orb halo. Fáze 2: **InputLevelMeter** — Studio LED ladder
s pulsujícími top reds při clippingu, Vintage analogový VU dial
s ručkou, Glassy gradient s halo. Fáze 3: **BeatIndicator** (metronom)
— Studio LED v mosazném prstenci, Vintage mosazné speed-knob mince
s vyrytým číslem doby, Glassy neon orb. Fáze 4: nový widget
**KnobControl** (`src/components/widgets/`) — vertikální pointer drag,
plná klávesnice, ARIA `role="slider"`, 270° sweep; nasazený do Aparátu
(9 parametrů) a metronomu (2 hlasitosti). Fáze 5: **NotationViewer**
rám okolo bílého listu — Studio brass bezel s warm gold glow, Vintage
brass-edged papír s vinětou, Glassy frosted neon conic ring.

Souběžně: **Vintage dark paleta překopaná podle reálné kytary Solton
Double Cut** (mahogany surfacy, brass akcent místo oranžové, cream
text jako pickup bobbins, dark velvet pozadí). Detaily a hex hodnoty
v `PROJECT_STATUS.md`.

### ✅ AmpWidget na obrazovkách Cvičení a Metal
Kompaktní aparát karta vedle MetronomeWidget na Cvičení a nad záložkami
trenažérů na Metalu. ON/OFF tlačítko, 4 vestavěné presety (Čistý /
Crunch / Lead / Metal), live `InputLevelMeter` jen když aktivní, link
„Otevřít plný Aparát →" na `/amp`. Sdílí settings + presetId s plným
Aparátem přes `useAmpStore` — tone tweak na jedné obrazovce se projeví
všude.

Architekturně: každá obrazovka, která hostuje aparát, drží **vlastní
krátkodobou instanci `AmpChain`** přes hook `useLocalAmpChain`
(`src/components/amp/useLocalAmpChain.ts`). Chain se dispose při
unmount. Sdílený singleton AmpEngine se ukázal jako fyzicky nemožný
kvůli `output: 'export'` (každá `<Link>` navigace je plný document
reload, AudioContext zhasne). Sdílený je tedy jen state (settings,
presetId), ne audio engine. Detaily v `PROJECT_STATUS.md`.

---

## Shrnutí fází

| Fáze | Body | Co vznikne |
|---|---|---|
| 0 | 1 | instalovatelná skořápka |
| 1 | 2–5 | audio jádro, ladička, metronom, knihovna |
| 2 | 6–9 | lekce, riff trainer, YouTube player |
| 3 | 10–13 | pokrok, nahrávání, metal moduly → **konec MVP** |
| 4 | 14 | AI tutor |
| 5 | 15–18 | AlphaTab, ampsim, multi-track, stem separation |
| 6 | 19–22 | AR, vizualizéry, profil kytary, sdílení |

---

## Co NEděláme

Vlastní databáze tabů · vlastní stem separation v prohlížeči · online
multiplayer jam · iOS-specifické funkce · rozpoznávání emocí. Důvody
v `ARCHITECTURE.md`, sekce 8.
