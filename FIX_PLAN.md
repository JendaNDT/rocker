# Rocker — implementační plán oprav

**Datum:** 2026-06-02
**Vychází z:** `AUDIT.md`
**Účel:** rozsekat všechny nálezy z auditu na samostatné, postupně řešitelné kroky.

---

## Úvod

Audit našel celkem ~60 nálezů (kritické / důležité / kosmetické) napříč
6 dimenzemi (logika, provázanost, stabilita, čistota, UX, UI). V tomhle plánu
jsou zorganizované do **28 kroků** seskupených do **6 fází**:

- **Fáze A — Kritická stabilita** (kroky 1–4): rAF leaky, backup, race conditions
- **Fáze B — Důležitá stabilita** (kroky 5–9): SW, LoopPedal, ChatPanel, audio cleanupy
- **Fáze C — Live mód a dokumentace** (kroky 10–13): rozhodnutí o `/live`, soulad docs
- **Fáze D — Důležité UX/UI** (kroky 14–19): tap targety, focus, reduced-motion
- **Fáze E — Architektura a refaktor** (kroky 20–24): vrstvení, sjednocení, ESLint
- **Fáze F — Kosmetika a polish** (kroky 25–28): tokens, breakpointy, drobnosti

**Doporučená cesta:** začni **krokem 1** (sdílený `LoopRunner` helper). Je to
největší ROI v plánu — opraví tři kritické leaky najednou, uzavře otevřenou
otázku z `INTERCONNECT_PLAN.md` fáze D a vytvoří základ pro budoucí audio
přehrávače. Pak postupuj fázemi A → F. **Po fázi A je appka stabilní**, po
fázi C je dokumentace pravdivá, po fázi D je mobilní zážitek důstojný.

Velikost: **S** = ~1 soubor / 30 minut, **M** = 2–4 soubory / hodina, **L** =
větší refaktor / půl dne. Dohromady plán cílí na ~3–5 sessions.

---

## FÁZE A — Kritická stabilita

### Krok 1: Sdílený `LoopRunner` helper

- **Co:** Vytvořit `src/audio/loopRunner.ts` — třída/factory, která drží rAF
  smyčku a Web Audio scheduler-look-ahead vzor („two clocks"). Vystavit
  `start()` / `stop()` / `isRunning` API a v `stop()` spolehlivě nulovat
  context i flag, aby další tick z čekajícího `requestAnimationFrame` smyčku
  neresuscitoval. Současně uzavírá otevřenou otázku z `INTERCONNECT_PLAN.md`
  fáze D (duplicitní scheduler ve 3 souborech).
- **Soubory:** nový `src/audio/loopRunner.ts`, nový `src/audio/loopRunner.test.ts`,
  případně mírná aktualizace `ARCHITECTURE.md` (sekce 3.4) a `INTERCONNECT_PLAN.md`
  fáze D.
- **Závažnost:** kritické (blokuje #2)
- **Závislosti:** žádné
- **Ověření:** `npx tsc --noEmit` + nový vitest test, který ověří
  `rafId === 0` po `stop()`, žádný další tick po stop, a že druhý
  `start()` po stop neběží paralelně s prvním.
- **Velikost:** M

### Krok 2: Přepsat `metronome.ts` na `LoopRunner` (kritický rAF leak)

- **Co:** Nahradit ručně psanou rAF smyčku v `metronome.drawLoop` voláním
  `LoopRunner` z kroku 1. `stop()` musí spolehlivě zastavit i pendulum tick
  / accent flash callback.
- **Soubory:** `src/audio/metronome.ts` (řádky 107-109 konstanty,
  637-660 `drawLoop`), případně mírná úprava `src/stores/metronomeStore.ts`.
- **Závažnost:** kritické
- **Závislosti:** krok 1
- **Ověření:** vitest test, který spustí–stopne–spustí metronom 5× a ověří,
  že běží právě jedna smyčka; ručně: dlouhodobý běh + sledování CPU v dev
  tools, žádné zdvojené `currentBeat` updaty.
- **Velikost:** M

### Krok 3: Přepsat `riffPlayer.ts` a `gallopPlayer.ts` na `LoopRunner`

- **Co:** Stejný refaktor jako krok 2, ale pro `RiffPlayer.drawLoop` a
  `GallopPlayer.drawLoop`. Po tomhle kroku je v audio vrstvě jediný
  zdroj pravdy pro „two clocks" scheduler.
- **Soubory:** `src/audio/riffPlayer.ts` (28-30, 170-182),
  `src/audio/gallopPlayer.ts` (26-28, 152-169).
- **Závažnost:** kritické
- **Závislosti:** krok 1 (a ideálně po kroku 2, aby pattern byl ozkoušený)
- **Ověření:** vitest start/stop test pro každý player; ruční test
  riff traineru a gallop traineru v UI.
- **Velikost:** M

### Krok 4: Doplnit chybějící klíče v `backup.ts`

- **Co:** Přidat do `BACKUP_KEYS` čtyři chybějící: `rocker-amp`,
  `rocker-amp-presets`, `rocker-metronome`, `rocker-custom-tunings`. Bump
  `BACKUP_VERSION`. Ošetřit, že starší zálohy bez nových klíčů projdou
  restorem tiše (nezahazují existující data). Doplnit testy v
  `src/lib/backup.test.ts` pro každý nový klíč (round-trip + missing key
  case).
- **Soubory:** `src/lib/backup.ts:12-20`, `src/lib/backup.test.ts`.
- **Závažnost:** kritické
- **Závislosti:** žádné
- **Ověření:** vitest (round-trip každého storu + starší backup bez nových
  klíčů); ručně: export → smazat localStorage → import → ověřit, že amp
  presety, vlastní ladění, metronom config a amp settings jsou zpět.
- **Velikost:** S

---

## FÁZE B — Důležitá stabilita

### Krok 5: `YouTubePlayer` cleanup race

- **Co:** V `useYouTubePlayer.ts` uložit promise z `loadYouTubeApi()` do refu
  a v cleanup zavolat `playerRef.current?.destroy()` až po jejím dořešení.
  Zamezit vytvoření nového playeru, pokud `cancelled === true` v okamžiku
  `.then()`.
- **Soubory:** `src/components/youtube/useYouTubePlayer.ts:29-54`.
- **Závažnost:** kritické (ale úzké — týká se jen YouTube screen)
- **Závislosti:** žádné
- **Ověření:** `npx tsc --noEmit`; ruční test v dev-mode (React Strict —
  effect běží 2×) — žádný duch-iframe.
- **Velikost:** S

### Krok 6: `LoopPedal` try/catch okolo `decodeAudioData`

- **Co:** Obalit `decodeAudioData` v `stopBase()` / `stopOverdub()` try/catch.
  Při chybě nastavit jasný error stav (např. nový pole `lastError` ve
  state) a vrátit pedal do `"idle"` namísto polovičního stavu. UI to může
  zobrazit jako toast / hláška.
- **Soubory:** `src/audio/loopPedal.ts:53-64, 76-88`, případně
  `src/components/looper/LoopPedal.tsx` pro zobrazení chyby.
- **Závažnost:** důležité
- **Závislosti:** žádné
- **Ověření:** vitest s vadným blobem (mock `AudioContext.decodeAudioData`
  jako reject) → pedal skončí v `idle`, mapa vrstev je prázdná.
- **Velikost:** S

### Krok 7: `ChatPanel` AbortController + `streamChat` reader cleanup

- **Co:** V `ChatPanel.tsx` zavést `AbortController` a předat signál do
  `streamChat`. V `geminiClient.ts:225-271` v `streamChat` na všech
  early-return cestách (`!response.body`, network error, abort) volat
  `reader.cancel()` / `releaseLock()`. Při unmount panelu controller
  abortovat — šetří BYOK quotu.
- **Soubory:** `src/lib/ai/geminiClient.ts:225-271`,
  `src/components/tutor/ChatPanel.tsx:70-105`.
- **Závažnost:** důležité
- **Závislosti:** žádné
- **Ověření:** vitest (mock ReadableStream + simulovat abort → ověřit
  `reader.cancel` volán); ručně: zavřít tutor uprostřed streamu, ověřit
  v dev tools, že fetch je `aborted`.
- **Velikost:** M

### Krok 8: `AudioEngine.start/stop` race

- **Co:** V `AudioEngine` vystavit interní `startGeneration` čítač nebo
  podobný guard. `handleDeviceLost` z prvního streamu nesmí shodit
  engine, pokud uvnitř už běží nový start z jiné generace.
- **Soubory:** `src/audio/AudioEngine.ts:230-287`.
- **Závažnost:** důležité
- **Závislosti:** žádné
- **Ověření:** vitest s mockovaným `getUserMedia` → stop → start → ověřit
  že engine zůstane spuštěný i po doběhnutí „ended" listeneru ze starého
  streamu.
- **Velikost:** M

### Krok 9: `sw.js` — error responses a `event.waitUntil`

- **Co:** V network-first větvi přidat `if (response.ok) cache.put(...)`
  — neukládat 404/500. Obalit cache zápisy do `event.waitUntil(...)` na
  všech třech místech, aby je SW neukončil předčasně.
- **Soubory:** `public/sw.js:64-95, 67, 89, 104`.
- **Závažnost:** důležité
- **Závislosti:** žádné
- **Ověření:** ručně přes Chrome DevTools → Network throttling „Offline"
  + simulace 404 response (Service Worker tab), ověřit že chyby
  v cache nezůstávají.
- **Velikost:** S

---

## FÁZE C — Live mód a dokumentace

### Krok 10: Rozhodnutí o `/live` — minimal implementace

- **Co:** Doimplementovat minimalistický Live mód (velký BPM display
  + velký Tuner display + jeden Record toggle, žádné menu, žádné karty).
  Reuse existující komponenty (`TunerDisplay`, BPM display z metronome
  page, `RecorderControls` v minimalistické formě). Wake Lock automaticky
  zapnutý.
- **Soubory:** `src/app/live/page.tsx` (kompletně přepsat —
  zatím jen ScreenPlaceholder), případně extrahovat sdílené části.
- **Závažnost:** kritické (UX nález)
- **Závislosti:** žádné (ale po fázi A je to bezpečnější, ať Live nepoužívá
  rozbité smyčky)
- **Ověření:** ruční test na mobilu i desktopu, kontrola landscape
  „stojan" zážitku, kontrola wake lock.
- **Velikost:** M

### Krok 11: Přidat `/live` do `screens.ts` + UX zkratka z dashboardu

- **Co:** Přidat `live` do `src/lib/screens.ts`, ať se objeví v navu. V
  dashboardu doplnit primární CTA „Hraj teď" → `/live`. Volitelně přidat
  „Nalaď teď" shortcut, který otevře `/tuner` s pre-startem (engine se
  spustí hned po hydrataci, žádný extra klik).
- **Soubory:** `src/lib/screens.ts`, `src/components/dashboard/Dashboard.tsx`,
  případně `src/app/tuner/page.tsx` (auto-start query param).
- **Závažnost:** důležité (UX)
- **Závislosti:** krok 10
- **Ověření:** ruční test, navigace funguje, auto-start tuneru čeká na
  permission OK.
- **Velikost:** S

### Krok 12: Doladit ARCHITECTURE.md a PROJECT_STATUS.md o počty obrazovek

- **Co:** Buď přeformulovat „17 obrazovek" → „12 obrazovek + 4 legacy
  redirecty + Live", nebo (po kroku 10/11) skutečně mít 13 obrazovek
  v navu. Aktualizovat README, ARCHITECTURE sekci 5, PROJECT_STATUS.
- **Soubory:** `README.md`, `ARCHITECTURE.md` (411, 462, 430),
  `PROJECT_STATUS.md` (~663).
- **Závažnost:** důležité (dokumentace)
- **Závislosti:** krok 11
- **Ověření:** vizuální čtení; pravdy v dokumentaci odpovídají kódu.
- **Velikost:** S

### Krok 13: Drobné nesoulady v dokumentaci

- **Co:** (a) Aktualizovat `ampStore.ts:21-32` docstring (mluví o
  per-screen AmpChain — neplatí, je singleton); (b) v `ARCHITECTURE.md`
  sekci 2 pod diagramem doplnit, že mirror stores znají i vrstvu 5 (audio);
  (c) doplnit do `ARCHITECTURE.md` poznámku, že komponenty volají audio
  singletony napřímo (contact surface ~40 míst); (d) v `ARCHITECTURE.md`
  doplnit poznámku o vědomě fix-themed SVG fretboardech (vedle seznamu
  polymorfních výjimek); (e) v `AGENTS.md` upřesnit pravidlo o `src/lib/`
  — buď povolit React hooks, nebo říct, že se přesouvají (řeší krok 20).
- **Soubory:** `src/stores/ampStore.ts:21-32`, `ARCHITECTURE.md`,
  `AGENTS.md`.
- **Závažnost:** kosmetické / důležité (přesnost docs)
- **Závislosti:** žádné, ale (e) ladí s krokem 20
- **Ověření:** vizuální čtení.
- **Velikost:** S

---

## FÁZE D — Důležité UX/UI

### Krok 14: Tap targety pod 48 px — kritické akce

- **Co:** Zvětšit hit area (buď přímo, nebo přes invisible touch-area
  wrapper, aby studio look zůstal) u: GuidedTuner reference-tone button
  (28→48 px), BeatIndicator dot (40→48 px), metronome tempo +/-,
  SettingsButton, RecordingItem akce (Přejmenovat / Exportovat / Smazat),
  Marker × button, Onboarding Zpět/Dál.
- **Soubory:** `src/components/tuner/GuidedTuner.tsx:170-171`,
  `src/components/metronome/BeatIndicator.tsx:132, 167, 227, 289`,
  `src/app/metronome/page.tsx:282, 299`,
  `src/components/SettingsButton.tsx:14`,
  `src/components/recorder/RecordingItem.tsx:184-201, 257-263`,
  `src/components/Onboarding.tsx:127, 135`.
- **Závažnost:** kritické (UX nález)
- **Závislosti:** žádné
- **Ověření:** Chrome DevTools mobile preview — všechny targety ≥ 48 px;
  ruční test palcem na mobilu.
- **Velikost:** M

### Krok 15: Globální focus-visible ring

- **Co:** Do `globals.css` přidat utility / pravidlo pro
  `button:focus-visible, a:focus-visible, [role="button"]:focus-visible`,
  které dá viditelný `ring-2 ring-accent ring-offset-2 ring-offset-bg`
  v každém tématu. Cílem je, aby uživatel s klávesnicí viděl, kde stojí
  — dnes je jen `KnobControl` vzor.
- **Soubory:** `src/app/globals.css`.
- **Závažnost:** důležité (a11y)
- **Závislosti:** žádné
- **Ověření:** ruční test — Tab klávesou napříč hlavními obrazovkami,
  každý focused element má viditelný ring.
- **Velikost:** S

### Krok 16: `prefers-reduced-motion` na pulzující animace

- **Co:** Rozšířit `globals.css` `@media (prefers-reduced-motion)` z Glassy
  blur i na: `rocker-pulse` (BeatIndicator), Studio clip pulse (InputLevelMeter),
  `pendulum-accent-flash`, `animate-pulse` v RecorderControls. Buď přes
  globální `* { animation: none }` v komponentách s pulzy, nebo cílené
  pravidlo per animace.
- **Soubory:** `src/app/globals.css:98-107` + případné per-komponentní
  CSS (`BeatIndicator.tsx:158`, `InputLevelMeter.tsx:176`, atd.).
- **Závažnost:** důležité (a11y)
- **Závislosti:** žádné
- **Ověření:** Chrome DevTools → Rendering → Emulate CSS media feature
  `prefers-reduced-motion: reduce` → ověřit, že LED a flash se zklidní.
- **Velikost:** S

### Krok 17: `aria-live` regiony pro průběžné statusy

- **Co:** Přidat `role="status"` / `aria-live="polite"` na: BreakReminder,
  RecorderControls „Nahrávám…" indikátor, ChatPanel „Přemýšlí…" loading.
- **Soubory:** `src/components/BreakReminder.tsx`,
  `src/components/recorder/RecorderControls.tsx:95-176`,
  `src/components/tutor/ChatPanel.tsx:160-216`.
- **Závažnost:** důležité (a11y)
- **Závislosti:** žádné
- **Ověření:** ruční test se screen readerem (macOS VoiceOver) — change
  se ohlásí.
- **Velikost:** S

### Krok 18: Recorder test-poslech vstupu

- **Co:** Do `RecorderControls` vsadit `InputLevelMeter` viditelný hned
  po startu enginu (před nahráváním), aby uživatel viděl, že signál teče.
- **Soubory:** `src/components/recorder/RecorderControls.tsx:147`.
- **Závažnost:** důležité (UX)
- **Závislosti:** žádné
- **Ověření:** ruční test — viditelný meter před `recState !== "idle"`.
- **Velikost:** S

### Krok 19: Drobné UX/UI doladění

- **Co:** (a) NotationViewer empty stav: doplnit větu „Soubor zůstane
  v prohlížeči" (po vzoru AudioInputSettings); (b) ChatPanel — přidat
  „Kopírovat odpověď" tlačítko k assistant zprávám; (c) SettingsButton —
  posunout na `top-[env(safe-area-inset-top,12px)]` + `right-[env(safe-area-inset-right,12px)]`;
  (d) ChatPanel mobile overflow: `flex-wrap` nebo button pod textareou
  na úzkých obrazovkách; (e) BreakReminder — desktop layout
  `md:left-[14rem]` (kvůli `pl-56` nav railu).
- **Soubory:** `src/components/notation/NotationViewer.tsx:137-141`,
  `src/components/tutor/ChatPanel.tsx:180-237`,
  `src/components/SettingsButton.tsx:14`,
  `src/components/BreakReminder.tsx:59`.
- **Závažnost:** důležité (UX/UI)
- **Závislosti:** žádné
- **Ověření:** ruční test na mobilu + desktopu.
- **Velikost:** M

---

## FÁZE E — Architektura a refaktor

### Krok 20: Vyřešit hooks v `src/lib/`

- **Co:** Přesunout React hooks z `src/lib/` do `src/hooks/` (nová složka):
  `useHydrated.ts`, `useActiveTheme.ts`, `layout.ts` (pokud obsahuje hook).
  `useWakeLock.ts` přesunout z `src/components/` taky tam. `resolveTuning.ts`
  rozštěpit: čistá funkce v `lib/` + lookup adapter ve `customTuningsStore`.
  Aktualizovat `AGENTS.md` + `ARCHITECTURE.md`, ať realita odpovídá pravidlu
  („lib je čistý TS bez React"). Update všech importů.
- **Soubory:** přesun + update všech importujících (`src/lib/useHydrated.ts`,
  `useActiveTheme.ts`, `layout.ts`, `resolveTuning.ts`,
  `src/components/useWakeLock.ts`, ~30 importérů přes celé src/).
- **Závažnost:** důležité (architektura)
- **Závislosti:** žádné, ale jdou s krokem 13(e)
- **Ověření:** `npx tsc --noEmit` + `npm test` zelená; manuální check, že
  všechny obrazovky renderují.
- **Velikost:** L

### Krok 21: Sjednotit `clamp01` duplicitu

- **Co:** Vytvořit `src/lib/math.ts` s `clampGain01(v, fallback = 0)` (nebo
  podobně). Nahradit duplicitní implementace v `metronomeConfig.ts:42`
  a `multitrack.ts:35`. Pokud sémantika fallbacků opravdu liší, držet dvě
  pojmenované funkce (`clamp01OrFull` / `clamp01OrZero`).
- **Soubory:** nový `src/lib/math.ts`, `src/lib/metronomeConfig.ts:42`,
  `src/lib/multitrack.ts:35`.
- **Závažnost:** kosmetické
- **Závislosti:** žádné
- **Ověření:** vitest zelená + grep, že žádná původní duplicita nezbyla.
- **Velikost:** S

### Krok 22: Přejmenovat `AudioEngine.ts` → `audioEngine.ts`

- **Co:** Sjednotit konvenci v `src/audio/` (PascalCase pro třídy uvnitř,
  camelCase pro soubor). Update všech importů.
- **Soubory:** `src/audio/AudioEngine.ts` → `audioEngine.ts`, ~21 importérů.
- **Závažnost:** kosmetické
- **Závislosti:** žádné
- **Ověření:** `npx tsc --noEmit` + git case-sensitivity check (na macOS
  default FS bývá case-insensitive — radši přes 2-step rename).
- **Velikost:** S

### Krok 23: ESLint config s ochrannou sítí

- **Co:** Přidat `eslint.config.mjs` s pravidly: `no-restricted-syntax`
  proti raw barvám v className (`bg-neutral-*`, `bg-gray-*`, `bg-slate-*`,
  `text-white`, `text-black`), `@typescript-eslint/no-explicit-any`,
  zákaz `@ts-ignore`. Doplnit `npm run lint` skript do README.
- **Soubory:** nový `eslint.config.mjs`, `package.json`, `README.md`.
- **Závažnost:** kosmetické (ale strategicky cenné pro vibecoding)
- **Závislosti:** žádné, ale po krocích 24/25 (ať lint projde čistý)
- **Ověření:** `npm run lint` projde bez warningů na čistém kódu.
- **Velikost:** S

### Krok 24: Integrační testy audio singletonů

- **Co:** Pro každý singleton (`AudioEngine`, `metronome`, `riffPlayer`,
  `gallopPlayer`, `ampEngine`, `loopPedal`, `multitrackEngine`) napsat
  start→stop→start test s fake AudioContext. Ověřit, že po stopu
  `rafId === 0` (nebo ekvivalent) a žádný další `setState` neprojde.
  Tyto testy by chytily i `drawLoop` leak z fáze A — proto je dávat až
  po opravách, jako regression net.
- **Soubory:** `src/audio/*.test.ts` (rozšířit existující nebo přidat
  nové soubory podle modulu).
- **Závažnost:** důležité (regression net)
- **Závislosti:** kroky 1–8 (testovat opravené verze)
- **Ověření:** všechny nové testy zelené.
- **Velikost:** L

---

## FÁZE F — Kosmetika a polish

### Krok 25: Sjednotit `bg-accent/N` opacity → `bg-accent-soft` token

- **Co:** V `theme-tokens.css` zajistit, že `--accent-soft` (a varianty
  pro 10/40/90) je definovaný a vystavený jako Tailwind utility. Nahradit
  všech ~36 výskytů `bg-accent/10`, `accent/40`, `bg-accent/90` v komponentách
  tokeny.
- **Soubory:** `src/app/theme-tokens.css`, ~36 komponent (grep
  `accent/\d`).
- **Závažnost:** kosmetické
- **Závislosti:** žádné
- **Ověření:** vizuální srovnání před/po ve všech 3 tématech;
  `npm run lint` (po kroku 23) projde.
- **Velikost:** M

### Krok 26: Lokální porušení theme tokenů

- **Co:** (a) `PalmMuteStudio.tsx:325` `text-sky-400` → `text-accent` nebo
  vlastní token; (b) `PalmMuteStudio.tsx:333` `bg-white` → `bg-text`;
  (c) `YouTubePlayer.tsx:166` `bg-black` → `bg-surface-2` nebo
  komentář „letterbox záměrný"; (d) `NotationViewer.tsx:263, 315, 328`
  `bg-white` — nechat, ale přidat komentář „notace záměrně bílá";
  (e) `GuidedTuner.tsx:142` `border-success/60` → token.
- **Soubory:** viz výše.
- **Závažnost:** kosmetické
- **Závislosti:** žádné
- **Ověření:** vizuální check ve všech 3 tématech.
- **Velikost:** S

### Krok 27: Responsivní drobnosti

- **Co:** (a) `StatsRow.tsx:31` `grid-cols-3` → `grid-cols-1 sm:grid-cols-3`;
  (b) sjednotit dashboard breakpointy (AchievementsCard `sm:`, PLAN_GRID
  `lg:` → vybrat jeden); (c) `SplitColumns.tsx:28` rozhodnout o `md:` vs
  `lg:` (řeknout, co dává smysl pro tablet); (d) GuidedTuner reference-tone
  unicode `♪` — přidat textový fallback / SVG ikonu.
- **Soubory:** `src/components/dashboard/StatsRow.tsx`,
  `src/components/dashboard/AchievementsCard.tsx`,
  `src/components/dashboard/Dashboard.tsx`,
  `src/components/SplitColumns.tsx`,
  `src/components/tuner/GuidedTuner.tsx:175`.
- **Závažnost:** kosmetické
- **Závislosti:** žádné
- **Ověření:** ruční test na 320/768/1024/1440 px.
- **Velikost:** S

### Krok 28: Drobné dluhy a poznámky

- **Co:** (a) `backup.ts.isStringRecord({})` → vrátit informaci „prázdný
  backup, nic nezrestorováno"; (b) v `audioStore.ts:17` přidat komentář
  k top-level `subscribe()`, proč to je SSR-safe; (c) `ScreenPlaceholder.tsx:16`
  přepsat „bod roadmapy" → „připravujeme" (uživatel nečte roadmapu);
  (d) `LoopPedal.tsx:144` — zformulovat „Vrstvení připraveno" namísto
  „Vrstev: 0"; (e) Onboarding — přidat install hint („Tip: ve sdíleném
  menu vybrat Přidat na plochu" + `beforeinstallprompt` chytit, kde to
  prohlížeč podporuje); (f) `window.confirm/prompt` — rozhodnout, případně
  napsat tématický modal komponentu pro reuse; (g) `navigator.onLine`
  indikátor v AI tutoru.
- **Soubory:** podle bodů, ~5 komponent.
- **Závažnost:** kosmetické
- **Závislosti:** žádné
- **Ověření:** ruční test.
- **Velikost:** M

---

## Doporučené milníky

- **Po kroku 4 (konec fáze A):** appka je **stabilní**. Tři kritické rAF leaky
  jsou pryč, backup nikomu nic neztratí. Můžeš s klidem deploynout a nehlídat
  CPU graf po každém přepnutí metronomu.
- **Po kroku 9 (konec fáze B):** appka je **odolná**. Cleanup race conditions
  vyřešené, service worker servíruje jen úspěšné odpovědi, AI tutor neteče
  quotou. Vhodný release bod.
- **Po kroku 13 (konec fáze C):** **dokumentace odpovídá realitě**, Live mód
  existuje a je v navu. Vibe coding s budoucími sessions bude méně matoucí.
- **Po kroku 19 (konec fáze D):** **mobilní zážitek je důstojný** — tap
  targety velké, klávesová navigace viditelná, citlivý uživatel
  s `prefers-reduced-motion` nedostává epileptické záchvaty z LED.
  **Skvělý release bod před Bodem 22 roadmapy.**
- **Po kroku 24 (konec fáze E):** **kódová báze je technicky čistá** —
  vrstvy se drží, žádné duplicity, regression net testů. Připravené na další
  rozvoj.
- **Po kroku 28 (konec fáze F):** **finální polish.** Themer a designer
  v jednom těle se může konečně klidně dívat na detaily.

---

## Časový odhad (orientační)

- Fáze A: 1 session (kroky 1–4 jsou propojené a logické dělat spolu)
- Fáze B: 1 session
- Fáze C: 1 session (Live mód je největší kus)
- Fáze D: 1 session
- Fáze E: 1–2 sessions (krok 20 a krok 24 jsou každý L)
- Fáze F: 1 session

**Celkem: 6–7 sessions.** Není to závod — důležitější je každý krok dotáhnout
včetně testů a commit+push (per pravidlo z paměti).
