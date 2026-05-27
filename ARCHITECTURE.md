# Architektura aplikace — Rocker

Kytarový asistent pro učení a hraní na elektrickou kytaru. PWA postavená na
Next.js, cílená na PC (kytara přes zvukovku M-Audio M-Track Solo) i mobil
(přes mikrofon).

Tento dokument popisuje technickou architekturu. Roadmapa (co stavět a v jakém
pořadí) je v `ROADMAP.md`.

---

## 1. Shrnutí klíčových rozhodnutí

| Rozhodnutí | Volba | Proč |
|---|---|---|
| Typ aplikace | PWA (instalovatelná, offline) | Jeden kód pro PC i mobil, žádné app store schvalování |
| Framework | Next.js (App Router) | Zadání uživatele; zralý ekosystém, dobrá podpora ve vibecoding nástrojích |
| Build režim | Statický export (`output: 'export'`) | "Lokální start" — žádný backend, hostovatelné jako statické soubory |
| Jazyk | TypeScript | Typová kontrola = záchytná síť pro vibecoding |
| Styly | Tailwind CSS v4 + CSS proměnné | 3 přepínatelné vzhledy řízené přes `[data-theme]`/`[data-mode]` na `<html>` |
| Stav aplikace | Zustand | Lehké, jednoduché, bez boilerplate |
| Úložiště | IndexedDB (přes `idb`) + localStorage | Vše lokálně v prohlížeči, žádné účty |
| Audio | Web Audio API + getUserMedia | Detekce tónů běží na hlavním vlákně (AnalyserNode + Pitchy) — pro nástroje dostačující a spolehlivé |
| Hosting | Vercel | Živě na `rocker-rust.vercel.app`; HTTPS zdarma (nutné pro mikrofon), auto-deploy z `main` |

> **Poznámka k Next.js:** Třetí rešeršní dokument doporučoval Vite. Next.js je
> plně použitelný a je to tvoje volba — jen pojede v režimu statického exportu,
> protože nepotřebujeme server. Většina stránek je "client component"
> (`'use client'`), protože jádro aplikace je audio běžící v prohlížeči. AI
> tutor (Fáze 4, Bod 14) nakonec server nepotřebuje vůbec — volá Gemini přes
> BYOK přímo z prohlížeče (nativní endpoint posílá CORS hlavičky); aplikace
> zůstala čistě statická PWA.

> **Cena statického exportu:** `output: 'export'` znamená, že každá `<Link>`
> navigace je **plný document reload** (ověřeno přes
> `performance.getEntriesByType("navigation")[0].type === "navigate"`), ne
> client-side route transition. Window se mezi obrazovkami resetuje, JS state
> se zahazuje, AudioContext zhasne. Žádné singletons (modul-level cache,
> `globalThis`, Zustand persist) tuto fyzickou bariéru neobejdou. Důsledek:
> dlouhodobé audio (Aparát, metronom) nemůže žít napříč obrazovkami; když
> chceš sdílet stav mezi obrazovkami, musí to jít přes localStorage/IndexedDB.
> Aparát řeší `useLocalAmpChain` — každá obrazovka, která ho hostuje, má
> vlastní krátkodobou instanci `AmpChain` (viz 2.5 a 2.3). Přechod na
> server-rendered Next.js by problém vyřešil, ale rozbil PWA / Vercel free
> tier setup — vědomě vynecháno.

---

## 2. Vrstvy aplikace

Aplikace je rozdělená do sedmi vrstev. Každá vrstva zná jen vrstvu pod sebou,
nikdy ne nad sebou. Tím zůstane kód přehledný i když poroste.

```
┌─────────────────────────────────────────────────────────┐
│  1. PWA shell        manifest, service worker, offline   │
├─────────────────────────────────────────────────────────┤
│  2. UI vrstva        React komponenty, stránky, Tailwind │
├─────────────────────────────────────────────────────────┤
│  3. Stav             Zustand stores (nastavení, pokrok)  │
├─────────────────────────────────────────────────────────┤
│  4. Doménová logika  lekce, riff trainer, scoring        │
├─────────────────────────────────────────────────────────┤
│  5. Audio engine     AudioContext, worklety, detekce     │
├─────────────────────────────────────────────────────────┤
│  6. Obsah            statické JSON: akordy, lekce, riffy │
├─────────────────────────────────────────────────────────┤
│  7. Perzistence      IndexedDB, localStorage             │
└─────────────────────────────────────────────────────────┘
```

### 2.1 PWA shell

Zajišťuje, že aplikaci jde nainstalovat na plochu / domovskou obrazovku a že
funguje offline.

- **`manifest.json`** — název, ikony, barvy, `display: standalone`, orientace.
- **Service worker** — **ručně psaný `public/sw.js`** (plain JavaScript, bez
  build kroku). Cachuje aplikační skořápku a runtime cachuje statické soubory,
  aby šlo cvičit i bez internetu. Původně byla v plánu knihovna Serwist, ale
  `@serwist/next` nepodporuje Turbopack (výchozí builder Next.js 16) — viz
  rozhodnutí níže. Vlastní SW je navíc průhlednější a odolný vůči změnám
  builderu. Nová verze SW se po nasazení tiše nepřebírá — počká, dokud
  uživatel nepotvrdí lištu „nová verze" (`ServiceWorkerRegister`). Aby
  prohlížeč novou verzi vůbec poznal, build razítkuje `sw.js` unikátní
  verzí (`scripts/stamp-sw.mjs`).
- **Wake Lock API** — drží obrazovku rozsvícenou během cvičení.

### 2.2 UI vrstva

React komponenty a stránky (routes). Žádná byznys logika v komponentách —
ty jen vykreslují stav a volají akce z doménové vrstvy. Všechny texty UI
v češtině (viz sekce 9).

**Vzhledy.** Aplikace má 3 přepínatelné vzhledy (Studio, Vintage, Glassy),
každý ve světlém / tmavém / auto režimu. Barvy, typografie a geometrie jsou
CSS proměnné v `src/app/theme-tokens.css` pro 6 kombinací; Tailwind v4
`@theme` proxy je vystavuje jako utility třídy (`bg-surface`, `text-accent`,
`border-line`…). Komponenty čtou jen tyto proměnné — nevědí, jaké téma je
aktivní. `ThemeProvider` (mountovaný v `AppShell`) synchronizuje volbu ze
`settingsStore` na atributy `data-theme`/`data-mode` na `<html>` a v auto
režimu sleduje `prefers-color-scheme`. Výjimky čtoucí téma přímo:
`VintageFrame` (mosazné cvočky jen ve Vintage) a od 2026-05-26 i šest
polymorfních widgetů, kde se mění **forma** (ne jen barva): `CentsBar`
(ladička), `InputLevelMeter` (VU), `BeatIndicator` (mince/LED/orb),
`KnobControl` (otočné knoby v Aparátu a hlasitosti metronomu),
`NotationViewer` rám displeje not a od 2026-05-27 i `Pendulum` —
alternativní vizualizace metronomu (Wittner kyvadlo) v sekci Přízvuky. Všechny čtou téma přes sdílený hook
`useActiveTheme()` v `src/lib/`, který vrátí téma až po hydrataci (jinak
`null`) — komponenta tak má SSR-safe default fallback.

**Vintage = Solton Double Cut.** Vintage dark paleta je vědomě
inspirovaná reálnou kytarou Solton Double Cut (mahogany surfacy, brass
akcent, cream text). Nové CSS tokeny `--maple-light`, `--mahogany-glow`,
`--wood-grain`, `--gloss-reflex` v `theme-tokens.css`; pravidlo
v `globals.css` aplikuje gloss + wood grain plošně na `.bg-surface*`
ve Vintage dark.

### 2.3 Stav (Zustand stores)

Oddělené malé stores podle domény (aktuální stav):

- `settingsStore` — režim zobrazení, vzhled + barevný režim, ladění,
  handedness, Zen režim, notace H/B, šumová brána, stav onboardingu
  (persist do localStorage). Načtená data prochází validací (`merge`).
- `audioStore` — zrcadlo stavu audio enginu (běží/neběží, latence, zařízení).
- `metronomeStore` — zrcadlo stavu metronomu.
- `metronomeSettingsStore` — persistovaný konfig metronomu + presety
  + `visualMode: "dots" | "pendulum"` (globální preference, mimo
  config — uživatel má typicky jednu oblíbenou vizualizaci napříč
  všemi presety). Sanitizace na rehydrate.
- `gallopPlayerStore` — zrcadlo stavu gallop přehrávače (Bod 12).
- `riffPlayerStore` — zrcadlo stavu přehrávače riffů (Bod 12).
- `lessonProgressStore` — dokončené lekce (persist do localStorage).
- `ytLoopStore` — uložené YouTube smyčky (persist do localStorage).
- `progressStore` — odcvičené minuty, streaky, achievementy, BPM rekordy
  (persist do localStorage).
- `recordingsStore` — zrcadlo seznamu nahrávek; zdrojem pravdy je IndexedDB.
- `aiStore` — nastavení AI tutora: Gemini API klíč a stav (persist do
  localStorage) — Bod 14.
- `chatStore` — historie AI chatu (persist do localStorage) — Bod 14.
- `instrumentStore` — profil nástroje pro obrazovku „Kytara" (persist do
  localStorage, na načtení projde type guardem) — Bod 21.
- `ampPresetsStore` — uživatelovy vlastní amp presety (persist do
  localStorage, na načtení projde type guardem) — Bod 16.
- `ampStore` — sdílené aparát settings + presetId (persist do localStorage).
  **Nedrží aktivní stav** — každá obrazovka, která hostuje aparát, má
  vlastní lokální chain přes hook `useLocalAmpChain`. Důvod: `output:
  'export'` dělá z navigace plný document reload, takže ani sdílený
  singleton AmpEngine by AudioContext napříč obrazovkami neudržel.

Stores se vzorem „zrcadlo" (`audioStore`, `metronomeStore`,
`gallopPlayerStore`, `riffPlayerStore`, `recordingsStore`) jen odrážejí stav
objektu pod sebou (audio engine, přehrávače, resp. IndexedDB). Stores
s `persist` přežijí restart.

### 2.4 Doménová logika

Čistý TypeScript bez Reactu, dobře testovatelný:

- **Lekční engine** — načte definici lekce (JSON), vede uživatele kroky,
  vyhodnocuje splnění.
- **Riff trainer** — loop, count-in, postupné zrychlování tempa.
- **Surgical Practice Mode** — z prvního rešeršního dokumentu: když detekuje
  opakovanou chybu, izoluje 2–4 problémové tóny, sníží tempo o 30 %, vyžádá
  3 čisté repetice, pak zrychlí o 10 % a vrátí do skladby.
- **Scoring** — porovnání zahraného (z detekce výšky/rytmu) s cílem; frázový
  feedback, ne trestání každé noty.
- **Practice scheduler** — sestavuje denní 10–20min plán.
- **Rock/metal trenažéry** (Bod 12) — čisté enginy v `src/lib/`:
  `powerChordTrainer` (přechody power chordů proti metronomu), `gallopTrainer`
  (gallopový rytmus) a `palmMute` (detekce utlumení přes spektrální těžiště
  + kalibrace).
- **Break reminder** (Bod 13) — `breakReminder` hlídá souvislý cvičební čas
  a po 45 minutách doporučí přestávku.
- **AI tutor** (Bod 14) — vrstva `src/lib/ai/`: Gemini klient (čistý fetch,
  bez SDK), prompty, generátor cvičebního plánu (JSON režim), zpětná vazba
  nad strukturovanými daty o pokroku. BYOK, volá Gemini přímo z prohlížeče.
- **Hudební hry** (Bod 19) — `fretboardGame` (hmatníková hra: tón → pozice)
  a `earTraining` (rozpoznávání intervalů a kvality akordů) jako čisté,
  testovatelné enginy.
- **Profil nástroje a tónový poradce** (Bod 21) — `instrument`: typ profilu
  kytary, výchozí hodnoty, type guard a poradce `recommendTone` (styl →
  doporučený snímač, gain, EQ, přizpůsobené snímačům profilu).
- **Pomocná logika pokročilého zvuku** — `ampPresets` (presety aparátu,
  Bod 16), `multitrack` (immutabilní model multi-track projektu) a `loopMix`
  (skládání smyček) z Bodu 17, `wav` (kódování WAV).
- **Záloha dat** — `backup`: sestavení a validace přenosné zálohy
  localStorage dat (export/import nastavení a pokroku do JSON souboru).
- **Metronom — config a presety** — `metronomeConfig` drží celý
  `MetronomeConfig` schéma se sanitizéry (`sanitizeMetronomeConfig`)
  a čistou funkci `swingedTickDuration` pro asymetrické posouvání
  vnitřních subdivizí v scheduleru. `metronomePresets` má pomocníky
  `createPreset` / `sanitizePresetList` plus konstanta
  `FACTORY_PRESETS` se šesti read-only presety (Blues shuffle, Metal
  gallop, Funk groove, Waltz 3/4, Jazz swing, Polyrytmus 3 proti 4)
  a helper `isFactoryPreset(id)`. Tovární presety procházejí přes
  `sanitizeMetronomeConfig`, takže zůstávají validní i po pozdějších
  defaultech.

### 2.5 Audio engine

Srdce aplikace a nejnáročnější část. Podrobně v sekci 3.

### 2.6 Obsah

Statická data dodaná s aplikací jako JSON soubory (žádný backend):

- `chords.json` — tvary akordů (open, barré, power), prsty, SVG data.
- `scales.json` — stupnice (pentatonika, dur/moll, harmonická moll, frygická).
- `tunings.json` — ladění a frekvence strun (standard, Drop D/C/B/A, Eb, D std).
- `lessons/` — definice lekcí po Stage.
- `riffs/` — metalové riffy (tab data + metadata; audio dodá uživatel nebo YouTube).

### 2.7 Perzistence

- **localStorage** (přes Zustand persist) — malá strukturovaná data:
  nastavení, ladění, pokrok (minuty, streaky, achievementy), dokončené lekce,
  uložené YouTube loopy, nastavení a historie AI tutora, profil nástroje.
- **IndexedDB** (přes knihovnu `idb`) — velké bloby: **nahrávky** (od
  Bodu 11). Vrstva `src/lib/recordingsDb.ts`.

Důležité (z vibecoder skillu — "defensive on user's behalf"): vše, co se čte
z úložiště, projde **type guardem**. Poškozený nebo starý záznam se zahodí,
aplikace nespadne.

---

## 3. Audio engine — detailně

Toto je technicky nejtěžší část. Návrh ji proto izoluje do jednoho modulu
s čistým rozhraním, aby zbytek aplikace nemusel řešit nízkoúrovňové audio.

### 3.1 Princip

```
getUserMedia (mikrofon / M-Track Solo)
        │
        ▼
   AudioContext  ──► MediaStreamSource
        │
        ▼
   AnalyserNode  ──► čtení vzorků v requestAnimationFrame smyčce
        │
        ▼
  detekce výšky tónu (Pitchy)  ──► doménová logika ──► UI
```

Detekce výšky tónu běží na **hlavním vlákně** (`AnalyserNode` + knihovna
Pitchy). Architektura původně počítala s AudioWorkletem; pro ladičku a detekci
tónů je ale main-thread přístup ověřený standard, dostatečně plynulý a obchází
balicí potíže s Turbopackem. Metronom je přesný díky sample-accurate plánování
na časové ose `AudioContext` (technika „dvojích hodin"), worklet tedy nepotřebuje
ani on. AudioWorklet zůstává jako možnost pro budoucí náročné funkce.

### 3.2 Centrální `AudioEngine`

Jediný objekt (singleton), který vlastní `AudioContext` a stará se o:

- **Výběr vstupního zařízení** — `navigator.mediaDevices.enumerateDevices()`
  vrátí seznam vstupů. Na PC se mezi nimi objeví **M-Audio M-Track Solo**;
  uživatel ho vybere v nastavení. Na mobilu je to vestavěný mikrofon.
- **Oprávnění** — žádost o mikrofon až **kontextově** (těsně před prvním
  použitím), s lidským vysvětlením proč. Ne hned po startu.
- **Kalibraci latence** — změří `AudioContext.baseLatency` a `outputLatency`,
  uloží a podle toho upraví UX (viz 3.5).
- **Životní cyklus** — spuštění/zastavení, uvolnění zdrojů, ošetření situace
  kdy uživatel odpojí zvukovku za chodu.

### 3.3 Procesory (moduly nad enginem)

Každý nástroj je samostatný procesor zapojený do enginu:

| Procesor | Knihovna / technika | Poznámka |
|---|---|---|
| Ladička | **Pitchy** (McLeod Pitch Method, čisté JS) | monofonní, vrací `[pitch, clarity]`; `clarity < 0.9` zahodit; navíc amplitudová šumová brána proti okolnímu ruchu |
| Metronom | Web Audio scheduler s lookahead | **NE `setTimeout`** — sample-accurate plánování přes `AudioContext.currentTime` |
| Detekce noty/riffu | Pitchy nebo Pitchfinder (YIN) | monofonní, pro single-note riffy spolehlivé |
| Detekce akordů | **Essentia.js** | polyfonní, oficiálně "experimental" — zatím nepoužito, případně až po MVP jako **nápověda** |
| Slow-down přehrávač | nativní `preservesPitch` prohlížeče | změna tempa bez změny výšky; SoundTouch.js se neukázal potřeba (viz 3.1, riff trainer Bod 7) |
| Gallop přehrávač | Web Audio scheduler s lookahead (Bod 12) | přehrává gallop buňku ve smyčce, stejná technika jako metronom |
| Přehrávač riffů | Web Audio scheduler + syntéza tónů (Bod 12) | přehrává tab riffu syntetizovanými tóny v nastavitelném tempu |
| Palm Mute detektor | `AnalyserNode` FFT → spektrální těžiště (Bod 12) | „jas" zvuku; kalibrace na konkrétní sestavu |
| AlphaTab přehrávač | `@coderline/alphatab` (Bod 15) | prohlížeč Guitar Pro / MusicXML; vykreslování i MIDI syntéza na hlavním vlákně (`useWorkers: false`) |
| Ampsim řetězec | Web Audio uzly — `ampChain` (Bod 16) | pre-gain, zkreslení (WaveShaper), 3-pásmový EQ, filtrová simulace reproboxu, delay, reverb, chorus. Životní cyklus per obrazovka přes hook `useLocalAmpChain` (`src/components/amp/`) — chain se vytvoří při zapnutí na dané obrazovce a dispose při unmount (Aparát = lokální, ne sdílený singleton; důvod viz sekce 1) |
| Multi-track engine | MediaRecorder + `OfflineAudioContext` (Bod 17) | nahrávání stop přes sebe, mixdown a export do WAV |
| Loop pedál | `AudioBufferSourceNode` s `loop=true` (Bod 17) | bezešvá smyčka, vrstvení dalších vrstev přes ni |

### 3.4 Metronom — proč pozor

Metronom nesmí používat JavaScript časovače (`setTimeout`/`setInterval`) —
ty na mobilu "ujíždějí". Správné řešení (vzor "A Tale of Two Clocks" od
Chrise Wilsona): plánovat klikání dopředu s ~25 ms lookahead oknem přímo na
audio časové ose. To samé platí pro count-in v riff traineru.

Engine `metronome.ts` má dvě paralelní smyčky ve stejném `scheduler()`
callbacku: **hlavní** (s accent patternem, swingem a drum modem
přes `soundPattern` / `subdivisionSound`) a **sekundární polyrhythm**
(vlastní `ticksPerBeat`, vlastní zvuk a hlasitost). Polyrytmus je
fázově zarovnaný na hlavní downbeat při `start()` a zůstává tichý
během count-in (`bar < 0`) i během silent bar gap traineru. Count-in
je realizovaný záporným `scheduledBar` — ramp i gap se aktivují
až od `bar >= 0`. Swing posouvá vnitřní subdivize (osminy /
šestnáctky); sám beat zůstává v čase. Drum mode hraje per-beat
zvuky přes `soundPattern` (kick/snare na 1/2/3/4) a separátní
`subdivisionSound` na vnitřní ticky (hi-hat). Drum mode + swing
+ polyrhythm kombinují bez vzájemné kolize. Power Chord Trainer
sdílí engine, ale resetuje `soundPattern`, `subdivisionSound`,
`swing`, `countInBars` a `polyrhythm.enabled` při startu, aby
uživatelovo nastavení nepřeteklo do trenažéru.

### 3.5 Latence — realistická očekávání

Web Audio v prohlížeči nedosáhne nativní nízké latence. Realita:

| Prostředí | Očekávaná round-trip latence | Vhodné pro |
|---|---|---|
| PC (Mac) + M-Track Solo | ~10–20 ms | vše včetně monitoringu |
| PC (Windows) + M-Track Solo | ~15–35 ms (prohlížeč nepoužívá ASIO) | ladička, metronom, nahrávání, riff trainer; ampsim na hranici |
| Android + vestavěný mikrofon | ~20–40 ms | ladička, metronom, nahrávání, detekce riffů; **ne** live ampsim |
| iOS Safari | nestabilní | mimo cílové platformy |

Praktické pravidlo: **latence kritická je jen pro live monitoring/ampsim.**
Ladička, metronom, detekce riffu, nahrávání a slow-down přehrávač fungují
dobře i s 30–40 ms. Engine proto změří latenci při startu a pokud je vysoká,
real-time ampsim (Fáze 5) skryje nebo označí jako "preview only".

> **Fakt k ověření:** čísla latencí pocházejí z rešeršního dokumentu (měření
> Sound on Sound, micbuffa). Windows hodnota je můj odhad nahoru — prohlížeč
> nepoužívá ASIO, takže reálně bude horší než nativně naměřených ~9 ms.
> Doporučuju změřit na tvém konkrétním PC hned v Bodu 2.

### 3.6 Vstupní úroveň a profil nástroje

Konkrétní specifikace kytary Solton Double Cut **se nepodařilo ověřit** —
je to neznámá značka a spolehlivé veřejné zdroje k ní nejsou. Engine proto
o snímačích ani výstupní úrovni nic nepředpokládá. (Dřívější verze tohoto
dokumentu uváděla „P.A.F.-orientované snímače" — to byl neověřený údaj
a byl odstraněn.)

Místo pevného předpokladu má aplikace od Bodu 21 obrazovku „Kytara"
(`/instrument`) s **editovatelným profilem nástroje** — uživatel si snímače
a parametry zadá sám. Profil je poradní, ne dogmatický.

Obecné, na konkrétní kytaře nezávislé zásady pro engine:

- Na M-Track Solo nastavit gain tak, aby signál neclipoval — kontrolovat
  clipping LED a vizuální indikátor úrovně.
- Detekce výšky tónu funguje líp na **čistém** signálu než přes zkreslení
  (distorze přidává harmonické, které matou detekci).
- Onboarding (Bod 13) obsahuje krok s nastavením vstupu a vizuálním
  indikátorem úrovně; Palm Mute Studio má navíc vlastní kalibraci jasu.

---

## 4. Datový model

Skutečný stav úložiště. Záměr je: malá strukturovaná data
v localStorage, velké bloby v IndexedDB.

### IndexedDB (přes `idb`) — velké bloby

| Store | Obsah |
|---|---|
| `recordings` | nahrávky: blob + metadata (název, datum, délka, mimeType, markery) |

Vrstva `src/lib/recordingsDb.ts`. Uživatelem nahrané MP3/WAV pro riff trainer
zatím v DB nejsou — riff trainer čte soubor přímo z disku při každém otevření.

### localStorage (přes Zustand persist) — malá data

| Klíč | Obsah |
|---|---|
| `rocker-settings` | režim zobrazení, vzhled + barevný režim, ladění, handedness, Zen režim, notace H/B, šumová brána, stav onboardingu |
| `rocker-lesson-progress` | dokončené lekce |
| `rocker-progress` | odcvičené minuty po dnech, streaky, achievementy, BPM rekordy |
| `rocker-yt-loops` | uložené YouTube loopy: videoId, start/end, rychlost, štítek |
| `rocker-ai` | nastavení AI tutora: Gemini API klíč a stav (Bod 14) |
| `rocker-chat` | historie AI chatu (Bod 14) |
| `rocker-instrument` | profil nástroje: tělo, snímače, menzura, ladění, struny, poznámky (Bod 21) |

Pokrok, streaky a achievementy byly v původním návrhu plánované do IndexedDB;
jsou to ale malá strukturovaná data, takže šly vědomě do localStorage —
IndexedDB zůstává pro bloby.

Vše, co se čte z úložiště, projde **type guardem**. Poškozený nebo starý
záznam se zahodí, aplikace nespadne.

---

## 5. Obrazovky a navigace

Vychází z UX konceptu z rešeršních dokumentů — dva dominantní režimy:
**Cvičení** (bohaté na vedení) a **Live** (minimální, rychlé).

Skutečný stav navigace — 17 obrazovek (zdroj pravdy je `src/lib/screens.ts`):

| Route | Obrazovka | Účel |
|---|---|---|
| `/` | Domů | denní plán, streak, "Naladit", "Pokračovat", rychlý vstup do Live |
| `/tuner` | Ladička | chromatická + vedená podle ladění, displej v centech |
| `/metronome` | Metronom | BPM, takty, akcenty, tap tempo |
| `/practice` | Cvičení | lekce + riff trainer + Surgical mode |
| `/tutor` | AI tutor | chat, generátor cvičení, zpětná vazba (Bod 14) |
| `/games` | Hry | hmatníková hra a ear training (Bod 19) |
| `/metal` | Metal | rock/metal trenažéry: power chord, gallop, palm mute |
| `/amp` | Aparát | ampsim a efekty, presety (Bod 16) |
| `/instrument` | Kytara | profil nástroje a tónový poradce (Bod 21) |
| `/library` | Knihovna | akordy, stupnice, riffy (tab + zpomalení, Stojan režim) |
| `/notation` | Noty | prohlížeč Guitar Pro / MusicXML souborů (Bod 15) |
| `/youtube` | YouTube player | A/B loop, slow-down, štítky sekcí |
| `/recorder` | Nahrávání | nahrání take-u, přehrání, A/B srovnání |
| `/studio` | Studio | multi-track nahrávání přes sebe (Bod 17) |
| `/looper` | Looper | loop pedál se smyčkováním (Bod 17) |
| `/live` | Live mód | velká tlačítka: ladička, metronom, rec — bez rušení |
| `/profile` | Profil | nastavení, vstupní zařízení, export/smazání dat |

Navigace: spodní lišta na mobilu, boční na PC. Vždy dostupné rychlé tlačítko
ladička + metronom (floating action button).

### Responzivita PC vs mobil

- **Mobil** — na výšku, velká tlačítka (min. 48 px), ovládání palcem.
- **PC** — širší layout, víc informací najednou, výběr M-Track Solo jako vstup.
- **Landscape / "stojan" režim** — pro practice player a vizualizér hmatníku,
  optimalizováno na pohled z ~1 m vzdálenosti.

---

## 6. Struktura složek

Skutečná struktura projektu. Sedm vrstev ze sekce 2 je
**koncepční** — nemá vlastní složku každá. Doménová logika, statický obsah,
IndexedDB vrstva i čisté utility se sloučily do `src/lib/`; bylo to
jednodušší a pro vibecoding přehlednější než řada malých složek.

```
rocker/
├── public/
│   ├── manifest.json
│   ├── sw.js                # ručně psaný service worker
│   ├── alphatab/            # font Bravura + SONiVOX soundfont (Bod 15)
│   └── icons/
├── scripts/
│   └── stamp-sw.mjs         # razítkuje sw.js verzí při buildu
├── src/
│   ├── app/                 # Next.js App Router — stránky (17 obrazovek),
│   │                        #   globals.css, theme-tokens.css (3 vzhledy)
│   ├── components/          # React komponenty (UI vrstva), po doménách
│   ├── audio/               # audio vrstva (5): AudioEngine, metronome,
│   │                        #   gallopPlayer, riffPlayer, pitchDetector,
│   │                        #   muteAnalyzer, chordPlayer, countIn,
│   │                        #   tunings, recorder, ampChain,
│   │                        #   multitrackEngine, loopPedal
│   ├── lib/                 # doménová logika (4), data/obsah (6),
│   │                        #   IndexedDB vrstva (7), čisté utility,
│   │                        #   ai/ (Gemini klient) + testy *.test.ts
│   └── stores/              # Zustand stores (vrstva 3)
├── ARCHITECTURE.md
├── ROADMAP.md
├── AGENTS.md
├── SKILL.md
├── PROJECT_STATUS.md
└── package.json
```

> Statický obsah (akordy, stupnice, lekce, ladění) je zatím přímo v
> `src/lib/` jako TypeScript moduly, ne jako JSON v `public/content/`.

---

## 7. Bezpečnost, soukromí, právní rizika

Z rešeršních dokumentů — tyto body je třeba respektovat od začátku:

- **Žádná vlastní databáze tabů.** Hostovat cizí taby/texty bez licence je
  právní problém. Uživatel si nahrává vlastní soubory; YouTube se jen embeduje
  (to ToS YouTube povoluje).
- **Soukromí by default.** Vše lokálně v prohlížeči, žádné účty, žádné
  odesílání dat. To je velká výhoda — v profilu bude tlačítko "exportovat data"
  a "smazat vše".
- **Oprávnění kontextově.** Mikrofon se žádá až těsně před použitím,
  s vysvětlením.
- **Generické názvy presetů.** Ampsim presety (Bod 16) mají vlastní popisné
  názvy (Čistý / Crunch / Lead / Metal) — žádné cizí značky aparátů bez
  licence.
- **Open-source licence.** Hlídat licence audio knihoven. Pitchy
  a `@coderline/alphatab` jsou v pořádku; pozor na knihovny pod GPL/AGPL,
  pokud bude aplikace někdy komerční.

---

## 8. Co záměrně NEstavíme (v žádné fázi)

Z rešerše — vyhnout se těmto slepým uličkám:

- Vlastní databáze tabů (právní a datová noční můra).
- Vlastní stem separation v prohlížeči (potřebuje GPU/cloud — později jen
  přes externí API).
- Real-time online "jam" více hráčů (latence sítě to zabíjí).
- iOS-specifické funkce (Web MIDI/Bluetooth tam nefungují) — cíl je PC + Android.
- Rozpoznávání emocí z obličeje/hlasu (zakázáno v EU AI Act pro edukaci).

---

## 9. Lokalizace a jazyk

- UI **plně v češtině** od začátku, včetně hudebních termínů.
- Přepínač notace **H/B** (česká konvence) vs **B** (mezinárodní).
- Kód, názvy proměnných a funkcí, komentáře a commit messages **anglicky**
  (kromě doménových termínů jako `palmMute`, `gallop`).

---

## 10. Výkonové cíle

| Funkce | Cíl |
|---|---|
| Ladička | stabilní údaj bez "cukání", confidence gate proti falešné jistotě |
| Metronom | sample-accurate, žádný drift |
| Detekce riffu | odezva pocitově okamžitá, lag 50–100 ms je v pořádku |
| Live monitoring / ampsim | pod ~15 ms ideál; nad ~30 ms funkci omezit nebo skrýt |
| UI | plynulých 60 FPS, žádné sekání během audio zpracování |
| Offline | celá aplikace + obsah funguje bez internetu |

---

## 11. Testovací disciplína

Z vibecoder skillu — po každé netriviální změně:

1. `npx tsc --noEmit` (typová kontrola).
2. Testy doménové logiky (lekce, scoring, surgical mode) — čistý TS, dobře
   testovatelné. Doporučený nástroj: Vitest.
3. Audio a UI se ověřuje ručně — to automatizovat nejde.

Doménová vrstva (vrstva 4) je schválně oddělená od Reactu a audia právě proto,
aby šla testovat bez prohlížeče.
