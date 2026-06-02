# Rocker — komplexní audit

**Datum:** 2026-06-02
**Rozsah:** statická analýza celé kódové báze (~223 TS/TSX/CSS souborů, 20 stores,
21 audio modulů, 17 routes, 496 vitest testů). Audio chování a UI v prohlížeči
nebylo spuštěno — to si Jenda zkontroluje ručně.

---

## Shrnutí

Rocker je **na pozoruhodně dobrém stavu pro vibe-coded projekt**. `npx tsc --noEmit`
projde čistě, všech 496 testů jede zelená, vrstvená architektura se v zásadě
drží, theme-aware disciplína je výborná (672 použití theme tokenů vs 6 raw
barev), žádné `any`, žádný `@ts-ignore`, žádné TODO/FIXME v kódu. Persistence
má systematickou sanitizaci a sanitize-on-read/write u IndexedDB. Polymorfní
widgety, tři vzhledy a struktura komponent po doménách jsou řemeslně poctivé.

**Hlavní rizika jsou tři.** (1) Tři audio přehrávače (`metronome`, `riffPlayer`,
`gallopPlayer`) mají identickou chybu v rAF smyčce — `stop()` cancelne aktuální
frame, ale nechá smyčku se restartovat, takže po několika cyklech start/stop
běží paralelně dvě smyčky a CPU drainuje. (2) Záloha v `backup.ts` nepokrývá
4 perzistentní stores — uživatel ztratí amp settings, vlastní presety, vlastní
ladění a celou konfiguraci metronomu při restoru. (3) `/live` obrazovka je
inzerovaná v README i ARCHITECTURE, ale v kódu je jen `ScreenPlaceholder` —
dokumentace neodpovídá realitě.

**Kam dál:** Před dalším bodem roadmapy (Bod 22) zalátat tyhle tři věci,
nasadit minimalisticky chybějící `/live` (jen velký BPM + tuner + record), pak
dořešit otevřené architektonické otázky (`lib/` vs hooks v lib, `stores/` ↔
`audio/`, sdílený scheduler) v jedné refaktorové dávce.

---

## 1. Vnitřní logika

### Silné stránky

- Typecheck a 496 testů jedou zelená (3.3 s).
- Validace persistovaných dat je systematická — každý store s `persist()` má
  vlastní `merge:` funkci s type-guardem a fallbackem na default
  (`settingsStore.ts:125-195`, `customTuningsStore.ts:148-156`, `aiStore.ts:32-44`,
  `chatStore.ts:36-44`, `ampStore.ts:49-63`).
- IndexedDB sanitizace na **read i write** (`recordingsDb.ts` + `sanitizeRecording`
  v `src/lib/recordings.ts:111-142`) — do storu se nikdy nedostane rozbitý záznam.
- AI vrstva nikdy nehází — vrací diskriminované unioný `{ok:true|false}` se
  srozumitelnými českými hláškami (`geminiClient.ts:38-55`).
- `Recorder` má bezpečnostní zámky (`recorder.ts:83-113`): `mediaRecorder.onerror`
  resetuje stav, `stop()` má `settled` flag proti dvojitému resolve a try/catch
  okolo `mediaRecorder.stop()`.
- `AudioEngine` řeší skutečné edge cases — mono sečtení 2-vstupní zvukovky přes
  splitter, `addEventListener("ended")` na track pro detekci odpojeného
  zařízení, `webkitAudioContext` fallback pro Safari (`AudioEngine.ts:230-252,
  297-307`).

### Nálezy

#### Kritické

- **`metronome.drawLoop` se nikdy spolehlivě nezastaví** (`src/audio/metronome.ts:637-660`).
  `stop()` cancelne aktuální `rafId`, ale nenuluje `this.context`. Příští tick
  `drawLoop` context pořád vidí a vejde do `requestAnimationFrame(...)` znovu.
  Když uživatel zase spustí metronom, jedou dvě rAF smyčky paralelně a `setState`
  se volá dvakrát na frame. Long-term: CPU drain a falešné `currentBeat` updaty.
  Fix: nulovat `this.context` ve `stop()`, nebo přidat `if (!this.state.isPlaying)
  return` na začátek `drawLoop`.
- **Stejný leak v `RiffPlayer.drawLoop`** (`src/audio/riffPlayer.ts:170-182`)
  a **`GallopPlayer.drawLoop`** (`src/audio/gallopPlayer.ts:152-169`) — tři
  kopie té samé chyby. Stojí za jednotný fix přes sdílený `LoopRunner` helper.
- **`YouTubePlayer` může vyrobit nezachycitelný iframe**
  (`src/components/youtube/useYouTubePlayer.ts:29-54`). Cleanup nastaví
  `cancelled = true`, ale `loadYouTubeApi().then(...)` vyrobí nový `yt.Player`
  i poté. V přísném React dev-mode (každý effect běží 2×) je to garantovaná
  duplikace, která v UI vypadá jako duch.

#### Důležité

- **`LoopPedal` nemá try/catch okolo `decodeAudioData`** (`src/audio/loopPedal.ts:53-64,
  76-88`). Při selhání dekódu pedal nastaví UI stav `idle`/`looping`, ale interní
  mapa zůstane v poloviční konzistenci — UI lže.
- **`StreamChat` nereleasuje reader při early-return** (`src/lib/ai/geminiClient.ts:225-271`).
  Chybí `reader.cancel()` / `releaseLock()`. Token quota teče, dokud GC nesebere
  response.
- **`ChatPanel` nemá `AbortController`** (`src/components/tutor/ChatPanel.tsx:70-105`).
  `mountedRef` flag chrání jen `setState`, ne samotný `streamChat`. Unmount
  uprostřed dlouhé odpovědi konzumuje BYOK quotu dál.
- **Race condition v `AudioEngine.start/stop`** (`AudioEngine.ts:271-287`).
  Při rychlém přepnutí mikrofonu může `handleDeviceLost` z prvního streamu
  shodit teprve spuštěný engine.
- **`MultitrackEngine` cache nikdy neinvaliduje** (`src/audio/multitrackEngine.ts:17-37`).
  Cache podle `track.id` → AudioBuffer; přepsání blobu pod stejným ID servíruje
  staré audio. Není dnes bug, ale past pro budoucí rozšíření.

#### Kosmetické

- `countIn.ts` a `referenceTone.ts` mají vlastní AudioContext (záměrné, ale
  3 paralelní contexty na iOS jsou citlivější na background suspend).
- `backup.ts.isStringRecord` vrátí `true` pro prázdný `{}` — restore tichého
  prázdného backupu projde s 0 položkami bez hlášky.

---

## 2. Provázanost funkcí

### Silné stránky

- **Žádný dead code** — rekurzivní import scan (alias `@/` i relativní) nenašel
  ani jeden nereferencovaný modul mimo entry pointy.
- **Žádné runtime kruhové importy.** Jediný formálně cyklický pár
  (`@/lib/ytLoops` ↔ `@/stores/ytLoopStore`) je vyřešený type-only importem
  na straně lib.
- Mirror pattern (audioStore, metronomeStore, gallopPlayerStore, riffPlayerStore,
  recordingsStore, ampEngineStore) je jednoznačně oddělený — žádná duplicita
  zdroje pravdy.
- `AmpHost ↔ ampStore ↔ ampEngine` je push-pattern přes useEffect, jednosměrný.

### Nálezy

#### Důležité

- **`lib/` zná `stores/`** — porušení vrstvení (ARCHITECTURE sekce 2 říká, že
  lib je vrstva 4/6/7 a má stát pod stores):
  - `src/lib/useActiveTheme.ts:4` → `useSettingsStore`
  - `src/lib/layout.ts:4` → `useSettingsStore`
  - `src/lib/resolveTuning.ts:17` → `isCustomTuningId` z `customTuningsStore`
  - `src/lib/ytLoops.ts:5` → type-only import z `ytLoopStore`

  Tyhle čtyři jsou ve skutečnosti React hooks / lookups nad stavem — nepatří
  do „čisté logiky". Buď je přesunout do `src/hooks/`, nebo uznat v ARCHITECTURE,
  že `lib/` obsahuje i sdílené hooks.
- **`stores/` zná `audio/`** pro mirror stores — ARCHITECTURE 2.3 to říká
  explicitně, ale **vrstevní diagram v sekci 2 to nepřiznává**. Drobný rozpor
  v dokumentaci, ne v kódu.
- **Komponenty volají audio singletony napřímo (40+ míst)** — `audioEngine`,
  `metronome`, `ampEngine`, `recorder`, `riffPlayer`, `gallopPlayer`, `loopPedal`,
  `multitrackEngine`. Architektonicky obhájitelné, ale realita má velkou contact
  surface — refaktor audio modulů se dotkne hodně komponent. Stojí za poznámku
  v `ARCHITECTURE.md`.
- **Backup nepokrývá 4 perzistentní stores** (`src/lib/backup.ts:12-20`).
  `BACKUP_KEYS` chybí `rocker-amp`, `rocker-amp-presets`, `rocker-metronome`
  a `rocker-custom-tunings`. Export/import zálohy ztratí: nastavení aparátu,
  vlastní aparátové presety, kompletní nastavení metronomu (BPM, time signaturu,
  accenty, ramp/gap/swing), a všechna vlastní ladění.

#### Kosmetické

- **Duplicitní scheduler kód** ve třech souborech (`metronome.ts:107-109`,
  `riffPlayer.ts:28-30`, `gallopPlayer.ts:26-28`) — `LOOKAHEAD_MS = 25` a
  `SCHEDULE_AHEAD_S = 0.12` plus celá „two clocks" smyčka 3×. `INTERCONNECT_PLAN.md`
  fáze D to eviduje jako otevřenou otázku. Stojí za rozhodnutí: extrahovat
  sdílený `src/audio/scheduler.ts`, nebo plán uzavřít zamítnutím.
- **Duplicitní `clamp01`** — `src/lib/metronomeConfig.ts:42` (fallback = 1)
  vs. `src/lib/multitrack.ts:35` (lokální, fallback = 0). Stejné jméno, různá
  sémantika.

---

## 3. Stabilita

### Silné stránky

- Pádů z poškozených dat se aplikace brání — type-guardy v rehydrate, sanitize
  v IndexedDB. Vibecoder pravidlo „defensive on user's behalf" je dodrženo.
- Service worker má **rozumnou strategii**: network-first pro navigace i RSC
  `.txt` payloady (řeší stale chunky po deploy), cache-first pro hashované
  assety, čistí staré CACHE klíče v `activate`.
- Wake Lock drží obrazovku během cvičení.

### Nálezy

#### Kritické

- **Tři `drawLoop` leaky** (viz sekce 1) — primární zdroj stability rizika.

#### Důležité

- **SW.fetch ukládá i error responses** (`public/sw.js:64-95`). Network-first
  větev kešuje `response` před kontrolou `response.ok`. 404/500 se uloží do
  cache, a offline `caches.match("/")` ji vrátí. Fix: `if (response.ok)
  cache.put(...)`.
- **SW `cache.put` bez `event.waitUntil`** (`public/sw.js:67, 89, 104`) —
  fire-and-forget zápisy mohou být přerušeny.
- **Race condition `AudioEngine.start/stop`** při rychlém přepnutí mikrofonu
  (viz sekce 1).
- **`MultitrackEngine` cache nereaguje na změnu blobu** (viz sekce 1).

#### Kosmetické

- `ampStore.ts:21-32` má zastaralý docstring tvrdící, že „every screen owns
  its own AmpChain" — to už neplatí, AmpEngine je singleton hostovaný
  v `AmpHost`. Matoucí pro budoucí session.
- Top-level `subscribe()` v `audioStore.ts:17` běží při importu modulu.
  Funguje, ale je to závislost, kterou je nutné v `AudioEngine` udržet
  (žádný `window.…` v `subscribe()` cestě).

### Testovací pokrytí — poznámka

496 testů pokrývá `src/lib/` velmi dobře. **Slabě pokryté je nejvíc rizikové
chování:**

- Metronome scheduler / `drawLoop` lifecycle (přesně tam, kde je leak).
- RiffPlayer / GallopPlayer lifecycle.
- LoopPedal a MultitrackEngine async toky, decode chyby.
- AudioEngine start/stop race.
- AmpEngine singleton lifecycle.
- SW.js (žádný unit test).

Doporučuju **alespoň jeden integrační test pro start/stop cyklus** každého
audio singletonu (fake AudioContext, ověřit `rafId === 0` po stopu, žádný
další `setState`). Tyhle testy by chytily i `drawLoop` leak.

---

## 4. Čistota a přehlednost kódu

### Silné stránky

- **Žádné TODO/FIXME/HACK/XXX** v `src/` — neobvyklé a chvályhodné.
- **Žádné `any`** v produkčním kódu. `as unknown as` jen pro `webkitAudioContext`
  shim (legitimní) a v test mockech.
- Pojmenování — anglicky kód, česky UI — důsledně dodržené.
- Single source of truth pro screens (`src/lib/screens.ts`), tunings
  (`resolveTuning.ts`), domain logic v `src/lib/` + testy hned vedle.
- Velké soubory jsou ospravedlnitelné (`metronome.ts` 664 ř. = celý engine
  s polyrhythm + drum mode + count-in + swing; `lessons.ts` 494 ř. = data).
- Theme-aware ratio: **672 použití theme tokenů vs 6 raw barev v JSX**.

### Nálezy

#### Důležité

- **„17 obrazovek"** — dokumentace tvrdí 17, navigace má 12 + 4 redirecty
  + 1 placeholder. `SCREENS` v `src/lib/screens.ts` má jen 12 obrazovek;
  `/instrument`, `/youtube`, `/studio`, `/looper` jsou redirect stránky,
  `/live` je `ScreenPlaceholder`. ARCHITECTURE.md:411,462 a PROJECT_STATUS.md:663
  to popisují zavádějícím způsobem.
- **`/live` mód** je dokumentovaný jako hotová obrazovka (README.md:25,
  ARCHITECTURE.md:430 ho popisuje konkrétně „velká tlačítka: ladička,
  metronom, rec"), ale v UI je `ScreenPlaceholder`. Roadmap o Live mlčí.
- **AGENTS.md** říká „čistá logika v `src/lib/` je TypeScript bez Reactu",
  ale `src/lib/useHydrated.ts`, `useActiveTheme.ts`, `layout.ts` Reactem
  operují. Pravidlo upřesnit nebo hooks přesunout.

#### Kosmetické

- **`AudioEngine.ts` je jediný PascalCase soubor v `src/audio/`** — konvence
  je jinde camelCase soubor + PascalCase třída. Sjednotit na `audioEngine.ts`.
- **Žádný ESLint ani Prettier projektový config** — `npm run lint` jede Next
  defaults. Pro vibecoding by se hodil aspoň `eslint.config.mjs` s pravidly
  proti raw barvám a `any`.
- **`useWakeLock.ts` je v `src/components/`**, ne v `src/lib/` (kde by hook
  zase porušil vrstvení — viz výše). Drobnost.

### Porušení theme-aware a emoji pravidla (z `AGENTS.md`)

- `PalmMuteStudio.tsx:325` — `text-sky-400` (jediný „barevný" Tailwind utility
  v JSX mimo theme tokeny).
- `PalmMuteStudio.tsx:333` — `bg-white` na markeru posuvníku (existuje token
  `bg-text` pro tenhle účel).
- `YouTubePlayer.tsx:166` — `bg-black` na YouTube rámu (záměrné letterbox,
  ale `bg-surface-2` by sedlo).
- `NotationViewer.tsx:263, 315, 328` — `bg-white` jako podklad noty (záměrné
  pro notaci, ale chybí komentář).
- **`bg-accent/N` opacity tricks v 36 souborech** (např. `LoopPedal.tsx:82`,
  `ChatPanel.tsx:185`) — existuje token `--accent-soft` přesně pro tento případ;
  nekonzistence „někde `bg-accent-soft`, jinde `bg-accent/10`".
- Hex barvy v SVG fretboardech (`Fretboard.tsx`, `ChordDiagram.tsx`, `RiffLibrary.tsx`,
  `PowerChordTrainer.tsx`, `FretboardGame.tsx`, `Waveform.tsx`) — záměrně stejný
  look napříč tématy. Doplnit poznámku do `ARCHITECTURE.md` jako vědomou výjimku.
- **Žádné klasické emoji.** Typografické symboly (`✓ ♭ ♯ ♪ ☀ ☾`) v `practice/page.tsx:123`,
  `ThemePicker.tsx:23-24`, `tuner/GuidedTuner.tsx:151,175`, `TunerDisplay.tsx:45,49`,
  `NoteCheck.tsx:52`, `SurgicalTrainer.tsx:91` — pokud má pravidlo platit
  doslova, jsou to porušení; jinak OK.

---

## 5. UX

### Silné stránky

- Konzistentní hierarchická navigace — `AppShell` automaticky překlápí postranní
  rail (≥md) na spodní lištu (mobil), settings cog je ze všech obrazovek jedno
  kliknutí.
- Dashboard 3-do-1 (streak / minuty dnes / minuty celkem + denní plán
  + achievementy) + Zen mode, který Dashboard skryje.
- Onboarding má 4 kroky (vítání → vzhled → mikrofon → „tvůj první riff"),
  s reuse `AudioInputSettings` uvnitř — uživatel je hned funkční.
- Stavová zpětná vazba v audio flow je promyšlená — `RecorderControls.tsx:95-176`
  větví `!supported` → `!storageAvailable` → `!running` → `running && !isRecording`
  → `isRecording`, plus permission=denied + localError + engineError. Tuner
  page (`tuner/page.tsx:194-215`) stejný styl.
- Vedený tuner drží strunu ≥ 400 ms v zóně a zahraje zvukovou + haptickou
  konfirmu (`GuidedTuner.tsx:101-130`) — kytarista nemusí koukat na obrazovku.
- `ChatPanel` má suggested questions, streaming „Přemýšlí…", retry tlačítko
  a „Nová konverzace" (`tutor/ChatPanel.tsx:160-216`).
- `NotationViewer` pokrývá všechny 4 stavy: empty / loading / loaded / error
  (`NotationViewer.tsx:9`).
- `useWakeLock` na metronome, tuner, practice, riff-library, metal — cvičení
  obrazovku nepřeruší tmou.
- `BreakReminder` po 45 min souvislého cvičení.

### Nálezy

#### Kritické

- **Live mode je jen placeholder** (`src/app/live/page.tsx:1-10`). Slíbený
  jako „minimalistický rychlý vstup pro hraní s kytarou v ruce", chybí
  v `screens.ts`, takže není ani v navigaci — uživatel se k němu dostane
  jen přímou URL.
- **Cesta k „naladit" z dashboardu je 3-4 kliky + permission dialog.** Žádná
  zkratka „Nalaď teď" z dashboardu ani z denního plánu pro nejčastější úkon.
  Daily plan item má `href`, ale jen na screen, ne na akci. Pro denního
  uživatele zbytečná dyně.

#### Důležité

- **Recorder nemá test-poslech vstupu před start nahrávání** — uživatel jen
  vidí „Vstup běží" (`RecorderControls.tsx:147`), bez `InputLevelMeter`. Snadno
  se nahraje ticho.
- **`NotationViewer` empty stav neřekne „Soubor zůstane v prohlížeči"**
  (`NotationViewer.tsx:137-141`) — uživatel se logicky bojí o privacy.
  `AudioInputSettings` to říká dobře (řádka 96-97), notation ne.
- **`ChatPanel` nemá „kopírovat odpověď"** (`ChatPanel.tsx:180-191`) — uživatel
  může chtít odpověď AI poslat dál.
- **Reset / mazat ladění a marker prompt používá `window.confirm`/`prompt`**
  (`tuner/page.tsx:71-74`, `RecordingItem.handleDelete`). Konzistentní, ale
  vyskočí ze studiové atmosféry.
- **Žádný „offline" indikátor** (`navigator.onLine` se nepoužívá) — když síť
  spadne při použití AI tutora, uživatel uvidí jen generic error.
- **Onboarding nemá indikátor „přidat na plochu"** (`beforeinstallprompt`
  se nikde nechytá).

#### Kosmetické

- **Daily plan vede do screen-level URL, ne na konkrétní akci** — „lekce X"
  jde na `/practice`, neotevře přímo lekci.
- **`ScreenPlaceholder` říká „čeká na obsah — přidá ho další bod roadmapy"**
  (`ScreenPlaceholder.tsx:16`) — pro koncového uživatele „bod roadmapy"
  nic neznamená.
- **`LoopPedal` žádný empty stav pro vrstvy** — „Smyčka hraje. Vrstev: 0."
  zní podivně.

---

## 6. UI

### Silné stránky

- Polymorfní widgety jsou luxusně řešené — `KnobControl`, `CentsBar`,
  `InputLevelMeter`, `BeatIndicator`, `Pendulum` mají 4 vizuální varianty
  + `default` pre-hydration fallback.
- Konzistentní spacing — většina karet `rounded-lg border border-line
  bg-surface p-4|p-6`. Existují konstanty `PRIMARY_BUTTON`/`SECONDARY_BUTTON`/`SEGMENT_ACTIVE`
  (LoopPedal, AmpWidget, AmpStudio).
- Theme tokens — `bg-bg`, `bg-surface`, `text-accent`, `border-line`, `text-dim`
  pokrývají téměř všechno.
- `prefers-reduced-motion` respektováno pro Glassy blur (`globals.css:98-107`).
- Typografie má hierarchii: `text-3xl font-bold` h1, `text-lg font-semibold`
  h2, `text-sm` body, `text-xs text-dim` meta, `tabular-nums` pro čísla.

### Nálezy

#### Kritické

- **Tap targety pod 48 px** na mobilu (AGENTS pravidlo):
  - `BeatIndicator` dot `h-10 w-10` = 40 px (`BeatIndicator.tsx:132, 167, 227,
    289`) — pro klikání na akcent v 4-8 beat řadě úzké.
  - GuidedTuner reference-tone button `h-7 w-7` = 28 px (`GuidedTuner.tsx:170-171`)
    — uživatel ho přidržuje, **nejhůř dostupné kliknutí v celé aplikaci**.
  - Tempo +/- v metronomu `h-10 w-10` (`metronome/page.tsx:282, 299`).
  - `SettingsButton` `h-11 w-11` = 44 px (`SettingsButton.tsx:14`).
  - RecordingItem akce (`RecordingItem.tsx:184-201`) `px-2.5 py-1 text-xs`
    = ~26 px výška — Přejmenovat / Exportovat / Smazat se těžko trefí palcem.
  - Marker × button (`RecordingItem.tsx:257-263`) `rounded px-1` ještě menší.

#### Důležité

- **Animace ignorují `prefers-reduced-motion`** v komponentách: `BeatIndicator.tsx:158`
  (rocker-pulse), `InputLevelMeter.tsx:176` (Studio clip pulse), pendulum-accent-flash
  (`globals.css:121`), `animate-pulse` v RecorderControls. Globální media query
  pokrývá jen Glassy backdrop blur — pulzující LED metronomu poběží pořád.
- **`bg-accent/N` opacity obchází theme tokeny** (36 souborů) — viz sekce 4.
- **Focus styly** — KnobControl je vzorová (`role=slider`, aria-valuemin/max/now,
  klávesy Arrow/PageUp/Home/End, focus-visible ring). Většina ostatních buttonů
  spoléhá na browser default, který se na tématových pozadích ztrácí. Jen 1 grep
  match na `focus-visible:ring-2` v celé aplikaci.
- **`NotationViewer` file input `className="hidden"`** + `<label>` (typický
  trik pro stylování) — vizuální focus se ztrácí (`NotationViewer.tsx:131-134`).
- **`SettingsButton` ignoruje safe-area** (`SettingsButton.tsx:14` jen
  `top-3 right-3`); na iPhone v landscape se dostane pod notch.
- **`ChatPanel` textarea `min-w-0 flex-1` + `Odeslat self-end shrink-0`**
  (`ChatPanel.tsx:222-237`) — na 320 px mobilu s rows={2} může overflow.
- **Onboarding tlačítka Zpět/Dál** `px-5 py-2.5` / `px-6 py-2.5` = ~36 px
  výška (`Onboarding.tsx:127, 135`) — pod 48 px.
- **`SplitColumns` přepíná na 2 sloupce až `lg:`** (`SplitColumns.tsx:28`);
  na tabletu 768-1024 ostane 1 column. Záměrné?

#### Kosmetické

- `GuidedTuner.tsx:142` — `border-success/60` bez tokenu.
- `StatsRow.tsx:31` — `grid-cols-3` vždy; na úzké obrazovce „minut celkem"
  se stlačí na 1.5 řádek.
- Breakpointy v dashboard kartách nejsou stejné — `AchievementsCard sm:grid-cols-2`
  vs Dashboard `PLAN_GRID` na `lg:`.
- `BreakReminder` má `fixed inset-x-3 bottom-24` (`BreakReminder.tsx:59`);
  na desktopu kde nav je `pl-56`, reminder se vycentruje přes celý viewport
  včetně overlap nad navem.
- Reference tone v GuidedTuner používá `♪` (`GuidedTuner.tsx:175`) bez unicode
  font fallback — na některých Androidech tečka místo not.

---

## 7. Přístupnost (a11y) — souhrn

- **aria-label / aria-pressed** pokryto v 30 souborech — KnobControl
  a BeatIndicator jako vzor.
- **`<label htmlFor>` páry** v 19 souborech — formuláře svázané správně.
- **`role="status"`** jen na 2 místech (`ServiceWorkerRegister.tsx:81`,
  metronome tempo). BreakReminder, RecorderControls „Nahrávám…", ChatPanel
  „Přemýšlí…" by si zasloužily live region.
- **`prefers-reduced-motion`** pokrývá jen Glassy blur, ne animované
  LED/pulse/flash.
- **Focus styly** — jen 1 grep match (`KnobControl`). Většina spoléhá na
  browser default, který se na tématech ztrácí.
- **Tap targety** — několik klíčových akcí pod 48 px (viz UI nálezy).
- **Landmarky** — `<nav>` (Nav.tsx:46), `<main>` (AppShell.tsx:41) jsou.
  Skip-link chybí, ale při 1 navu to není kritické.
- **Kontrast** staticky nezhodnotitelný; theme tokens vypadají rozumně, ale
  `--muted` na `--surface` v studio dark (`#5a5448` na `#171513`) může být
  na hranici WCAG AA pro malá písmena.

---

## Doporučené další kroky

### Hned (před dalším bodem roadmapy)

1. **Opravit tři `drawLoop` leaky** v `metronome.ts`, `riffPlayer.ts`,
   `gallopPlayer.ts`. Jednotný fix přes sdílený `LoopRunner` helper současně
   uzavře otevřenou otázku z `INTERCONNECT_PLAN.md` fáze D.
2. **Doplnit chybějící klíče v `backup.ts`** (`rocker-amp`, `rocker-amp-presets`,
   `rocker-metronome`, `rocker-custom-tunings`) + bump `BACKUP_VERSION`. Starší
   zálohy se musí umět restorovat bez těchto klíčů.
3. **`/live` rozhodnutí** — buď minimalisticky doimplementovat (velký BPM
   + velký tuner + record button), nebo z README + ARCHITECTURE vyhodit.
   Pokud doimplementovat, přidat do `screens.ts`, ať se objeví v navu.
4. **`YouTubePlayer` cleanup race** — uložit promise z `loadYouTubeApi`
   do refu, v cleanup zavolat `playerRef.current?.destroy()` po `.then()`.

### Brzy (v rámci stabilizační dávky)

5. **Tap targety pod 48 px** — zvětšit klíčové (`BeatIndicator`, GuidedTuner
   reference-tone, tempo +/-, RecordingItem akce, Onboarding Dál/Zpět) buď
   přímo, nebo přes invisible touch-area wrapper. Estetiku to nemusí narušit.
6. **`prefers-reduced-motion`** rozšířit z Glassy blur na animované LED/pulse/flash
   (`BeatIndicator`, `InputLevelMeter` Studio variant, pendulum-accent-flash,
   `RecorderControls` animate-pulse).
7. **Focus-visible ring** přes utility — přidat globální `focus-visible:ring-2
   focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-bg`
   do `globals.css` na `button:focus-visible, a:focus-visible`.
8. **SW.js — `if (response.ok) cache.put(...)` + `event.waitUntil`** pro
   cache zápisy.
9. **`ChatPanel` AbortController** + reader cleanup ve `streamChat` — šetří
   BYOK quotu.
10. **`LoopPedal` try/catch okolo `decodeAudioData`** + jasný error stav.

### Refaktorová dávka (větší, po stabilizaci)

11. **Vyřešit vrstvení** — buď přesunout `useHydrated`, `useActiveTheme`,
    `layout`, `useWakeLock` do `src/hooks/` a `resolveTuning` rozštěpit
    (čistá funkce v lib + lookup adapter ve store), nebo aktualizovat
    `AGENTS.md` a `ARCHITECTURE.md`, ať realita odpovídá pravidlu.
12. **Sdílený `src/audio/scheduler.ts`** (uzavře `INTERCONNECT_PLAN.md` fázi D
    a současně řeší bod 1).
13. **Integrační testy audio singletonů** — start/stop cyklus pro každý
    singleton (fake AudioContext), ověřit `rafId === 0` po stopu. Tyto
    testy by chytily i `drawLoop` leak.
14. **`AudioEngine.ts` → `audioEngine.ts`** (sjednocení s ostatními soubory
    v `audio/`).
15. **ESLint config** s pravidly proti raw barvám (`bg-neutral|bg-gray|bg-slate`),
    `any`, `@ts-ignore` — automatická síť pro vibecoding.

### Polish (kdykoli)

16. UX zkratka „Nalaď teď" z dashboardu (jeden klik → tuner + auto-start).
17. `NotationViewer` empty stav — přidat větu o lokálním zpracování.
18. Aktualizovat zastaralý `ampStore.ts` docstring.
19. Sjednotit `bg-accent/N` opacity → `bg-accent-soft` token.
20. Doplnit `aria-live` regiony pro BreakReminder, „Nahrávám…", „Přemýšlí…".

---

## Otevřené otázky pro Jendu

1. **`/live`** — doimplementovat (velký BPM + tuner + record), nebo z dokumentace
   vyhodit?
2. **„17 obrazovek"** — přeformulovat na „12 + 4 redirecty + 1 placeholder",
   nebo skutečně doimplementovat zbylé?
3. **PWA install prompt** — chceš vlastní „Nainstaluj na plochu" tlačítko
   v Onboardingu / Profilu, nebo nechat browser default?
4. **Wake lock na recorder/looper/studio/notation** — má zůstávat zapnutý
   jako na metronome/tuner?
5. **Daily plan zkratky** — má jeden klik na „Naladit teď" rovnou spustit
   `audioEngine.start()`, místo jen otevřít screen?
6. **`prefers-reduced-motion`** — aplikovat i na pulzující LED / akcentní
   flash / animate-pulse?
7. **Tap targety pod 48 px** — vědomé estetické rozhodnutí (studio look),
   nebo doladit přes invisible touch-area wrapper?
8. **`window.confirm/prompt`** pro mazání ladění a marker editaci — nechat
   (nativní = známé), nebo udělat tématový modal?
9. **`SettingsButton` safe-area** — posunout na `top-[env(safe-area-inset-top,12px)]`?
10. **Hooks v `src/lib/`** (`useHydrated`, `useActiveTheme`, `layout`) — povolit
    oficiálně (změnit AGENTS.md), nebo přesunout do `src/hooks/`?
11. **Sdílený scheduler** — uzavřít `INTERCONNECT_PLAN.md` fázi D rozhodnutím
    (udělat / zamítnout) místo „zvážit"?
12. **Backup verze** — má restore starších záloh (bez nových klíčů) projít
    tiše, nebo s informací uživateli?
