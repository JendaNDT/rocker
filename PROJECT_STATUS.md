# Rocker – Project Status
*Naposled aktualizováno: 25. 05. 2026*

## 🎯 Co to je
PWA kytarový asistent pro učení a hraní na elektrickou kytaru. Funguje na PC
(kytara přes zvukovku M-Audio M-Track Solo) i na mobilu (přes mikrofon).

Stack: Next.js 16 (App Router, statický export), TypeScript 6, Tailwind 4,
Zustand, Web Audio API + getUserMedia, Vitest, ručně psaný service worker.
Perzistence localStorage (přes Zustand persist) pro malá data včetně pokroku;
IndexedDB (přes `idb`) pro velké bloby — nahrávky, od Bodu 11.

## ⏭️ Příští krok
Hotové (v kódu): Široké MVP (body 1–13), Fáze 4 (Bod 14), Body 15–17,
**Bod 19 (vizuální učení — „Hry")** a **Bod 21 (profil nástroje —
„Kytara")**.

Beta test s 5–10 lidmi, Bod 18 (stem separation) i Bod 20 (Mirror-AR
vizualizér) jsou **vědomě odložené / vynechané** (Bod 18 — placená API,
serverová závislost; Bod 20 — experimentální vrstva s velkou závislostí
MediaPipe a nejistou hodnotou). Zbývá poslední **Bod 22 (sdílení
a organizace)**.

**Nutné reálné testy na Jendovi** (`npm run dev`) — neověřená audio práce
se hromadí, hodila by se jedna seance s kytarou:
- **Bod 16 (Aparát)** a **Bod 17 (Studio, Looper)** — zvuk, latence
  i overdub NEOVĚŘENÉ, postaveno vědomě naslepo.
- AI tutor, „Noty" a „Hry" — méně rizikové, stačí proklikat.

Zbývá také starší ruční test se zvukem a kytarou:
- **Palm Mute Studio** (Metal → Palm mute) — prahy v `useMuteDetector.ts`
  (`ONSET_THRESHOLD`, vzorkovací okno) můžou potřebovat doladit.
- Ladička, metronom, přehrávání riffů a metalových trenažérů — vše, co
  potřebuje mikrofon nebo zvukový výstup.

Pozn.: plný `npm run build` se v sandboxu nedá ověřit (EPERM na mazání
starých build složek — známý quirk, viz `AGENTS.md`); ověřuje se `tsc`.
Build na Macu doběhne.

Než příští session začne: přečíst `AGENTS.md` a `SKILL.md` (pravidla
spolupráce), pak tento soubor.

## ✅ Hotovo
Hotovo **13 z 13 bodů MVP** (+ rozšíření 1b) — **Široké MVP je kompletní**.
Fáze 1–4 dokončené, Fáze 5 rozpracovaná (Body 15–17 hotové), Fáze 6
rozpracovaná (Body 19 a 21 hotové, Bod 20 vynechán). Aktuálně
**325 automatických testů**, vše prochází; `tsc` čistý.

- Příprava — prostudované 3 rešeršní dokumenty, `ARCHITECTURE.md`, `ROADMAP.md`
- **Bod 1** — PWA skořápka: Next.js projekt, manifest, service worker,
  navigace, 9 obrazovek
- **Bod 1b** — přepínání zobrazení (Automaticky / Počítač / Mobil) + trvalé
  tlačítko nastavení vpravo nahoře
- **Bod 2** — audio engine: AudioContext, výběr vstupu (M-Track Solo /
  mikrofon), oprávnění, indikátor úrovně, měření latence
- **Bod 3** — ladička: detekce výšky tónu (Pitchy), chromatický i vedený
  režim, 6 ladění, odchylka v centech
- **Bod 4** — metronom: sample-accurate scheduler, 40–240 BPM, takty,
  subdivize, akcent, tap tempo, haptika
- **Bod 5** — knihovna: 26 akordů (diagramy + zvuková ukázka), 6 stupnic na
  hmatníku, přepínač leváci/praváci *(konec Fáze 1)*
- **Bod 6** — lekční engine + 12 lekcí (Stage 1–2), obrazovka Cvičení
  s přehrávačem, „try it" detekce tónu
- **Bod 7** — riff trainer: přehrávač MP3/WAV, zpomalení bez změny výšky,
  A/B smyčka, waveforma, count-in, postupné zrychlování
- **Bod 8** — Přesný trénink (Surgical Mode): 5 cvičení, surgical engine,
  detekce tónu, drill (izolace problému + 3 čisté repetice)
- **Bod 9** — YouTube Practice Player: vložení videa, A/B smyčka, rychlost,
  štítkování a ukládání úseků
- **Bod 10** — sledování pokroku: automatické měření odcvičených minut na
  cvičebních obrazovkách, streaky, 11 achievementů, denní ~15min plán,
  dashboard na úvodní obrazovce, detail pokroku v Profilu *(začátek Fáze 3)*
- **Bod 11** — nahrávání: záchyt hry přes MediaRecorder, uložení do
  IndexedDB, přehrávání, A/B srovnání dvou take-ů, markery (označování míst),
  přejmenování a export nahrávky
- **Bod 12** — rock/metal moduly: obrazovka Metal se třemi trenažéry (Power
  Chord Trainer, Gallop Trainer, Palm Mute Studio s kalibrací a drillem),
  sekce Riffy v Knihovně (14 riffů, tab v SVG + zpomalené přehrávání).
  Power Chord a Gallop napojily BPM rekordy z Bodu 10
- **Bod 13** — UX dokončení MVP: Wake Lock na cvičebních obrazovkách,
  upozornění na přestávku po 45 min, Zen režim, přepínač notace H/B,
  Stojan režim (celoobrazovkový tab riffu), onboarding s úvodní audio
  kalibrací, fallbacky (webkitAudioContext, výpadek IndexedDB)
- **Bod 14** — AI tutor (konec Fáze 4): obrazovka `/tutor` se třemi režimy.
  **Chat „zeptej se"** — AI vrstva v `src/lib/ai/`, `aiStore`, nastavení
  klíče v Profilu (`AiSettings`), navržené otázky, ukládaná historie
  (`chatStore`), streamování odpovědí. **Generátor cvičení** —
  `planGenerator` + `PlanPanel`, AI sestaví plán na míru (čas + zaměření +
  pokrok). **Zpětná vazba z dat** — `feedback.ts` + `FeedbackPanel`, AI
  poradna nad pokrokem. BYOK — vlastní bezplatný Gemini klíč, voláno přímo
  z prohlížeče bez serveru
- **Bod 15** — AlphaTab integrace (začátek Fáze 5): obrazovka `/notation`
  („Noty"). Uživatel nahraje vlastní Guitar Pro / MusicXML soubor, AlphaTab
  vykreslí tabulaturu i noty, přehrává přes MIDI syntezátor (SONiVOX
  soundfont) s kurzorem, A/B smyčkou a zpomalením. Knihovna
  `@coderline/alphatab`; vykreslování i audio na hlavním vlákně
  (`useWorkers: false`, ScriptProcessor výstup); font a soundfont
  v `public/alphatab/`
- **Bod 16 (kód hotový, zvuk neověřený)** — ampsim a efekty: obrazovka
  `/amp` („Aparát"). Živý vstup kytary → `AmpChain` (pre-gain, zkreslení,
  3-pásmový EQ, filtrová simulace reproboxu, delay) → výstup. Presety
  s generickými názvy (Čistý / Crunch / Lead / Metal), posuvníky všech
  parametrů. Audio na hlavním vlákně (ScriptProcessor). POZOR: postaveno
  naslepo — zvuk a latence čekají na reálný test s kytarou
- **Bod 17 (kód hotový, zvuk neověřený)** — multi-track a loop pedál.
  Obrazovka `/studio` („Studio"): nahrávání stop přes sebe (overdub),
  přehrání všech, u stopy ztlumení / hlasitost / smazání, mixdown a export
  do WAV (`OfflineAudioContext`). Obrazovka `/looper`: loop pedál — nahraj
  smyčku, bezešvě se opakuje (`loop=true`), vrstvi přes ni. Engine staví
  na recorderu z Bodu 11; multi-track projekt žije v paměti session
- **Bod 19** — vizuální učení (začátek Fáze 6): obrazovka `/games`
  („Hry") se dvěma režimy. **Hmatníková hra** — appka pojmenuje tón, hráč
  klikne na klikatelný hmatník; skóre a série. **Ear training** —
  rozpoznávání intervalů a kvality akordů sluchem (přes syntézu z Bodu 5).
  Logika v `fretboardGame.ts` a `earTraining.ts` (s testy). 3D Three.js
  vizualizér vědomě vynechán
- **3 přepínatelné vzhledy** (mimo roadmapu — vizuální nadstavba,
  slito do `main` 25. 05. 2026, vizuálně ověřeno Jendou). Témata **Studio**
  (tube amp, serif, mosaz), **Vintage** (staré studio, papír, mosazné
  cvočky) a **Glassy** (frosted gradient), každé v režimu Světlý /
  Tmavý / Auto-OS. Volba v `/profile → Vzhled`, persistuje se přes
  Zustand v `rocker-settings`. CSS proměnné v `theme-tokens.css`,
  Tailwind v4 `@theme` proxy v `globals.css`, `ThemeProvider`
  synchronizuje `data-theme`/`data-mode` na `<html>`. Všech 58 souborů
  obrazovek přemigrováno z `bg-neutral-*` na theme-aware třídy
  (`bg-surface`, `text-accent`…). Vintage cvočky přes `VintageFrame`,
  Glassy frosted blur plošně přes CSS. Postaveno z design handoff
  balíčku; `tsc` čistý, 325 testů prochází, vizuálně ověřeno
- **Audit vnitřní logiky + opravy** (větev `fix/audit-opravy`, čeká na
  slití do `main`). Proběhl kompletní audit logiky (audio, lib, AI,
  stores, komponenty) — **žádný kritický bug**. Opraveno ve 4 dávkách:
  hydratační guard `VintageFrame`; sanitizace persistovaných dat
  v `settingsStore` i `recordingsDb`; prázdná historie chatu →
  srozumitelná chyba a dozpracování streamu v `geminiClient`; strop
  minut v `planGenerator`; `onerror` v `recorder.ts`; úklid při
  odmountu (`RecorderControls`, `ChatPanel`, `RiffTrainer`); změna
  taktu metronomu za běhu; odpočet `countIn` z audio hodin; guard
  prázdného cellu v `gallopPlayer`; Looper a Studio se počítají do
  odcvičených minut. +9 testů pro `recordingsDb` (nová vývojová
  závislost `fake-indexeddb`, jen pro testy)
- **Šumová brána** (větev `feature/sumova-brana`, čeká na slití). Detekce
  tónů (`pitchDetector`) má amplitudovou bránu — vstup tišší než nastavená
  hranice se zahodí, aby okolní ruch nezaregistroval jako tón. Citlivost
  je nastavitelná posuvníkem v Profilu → Zvuk a vstup (`noiseGate`
  v `settingsStore`, default 0.06, perzistuje se); práh se kreslí jako
  čárka na ukazateli hlasitosti pro vizuální kalibraci. `tsc` čistý,
  325 testů. Pozn.: bere jen tichý okolní zvuk, hlasitý ruch přímo
  u mikrofonu software neodfiltruje
- **Autokalibrace šumové brány** (větev `feature/autokalibrace`, čeká
  na slití). Tlačítko „Změřit pozadí" v Profilu → Zvuk a vstup —
  appka 2 s poslouchá vstup, najde špičku hluku místnosti a nastaví
  bránu kousek nad ni (`recommendedNoiseGate` v `pitchDetector`,
  s testy). Ruční posuvník zůstává pro doladění
- **Upozornění na novou verzi** (větev `feature/update-prompt`, čeká na
  slití). Ručně psaný `sw.js` novou verzi tiše nepřebírá — počká; po
  nasazení appka ukáže lištu „Je k dispozici nová verze / Načíst",
  klik pošle workeru `SKIP_WAITING` a po převzetí jednorázově reloadne.
  Aby prohlížeč novou verzi vůbec poznal, build razítkuje `sw.js`
  unikátní verzí (`scripts/stamp-sw.mjs`, volá se z `npm run build`).
  Detekce + lišta v `ServiceWorkerRegister.tsx`. Bez nových testů —
  je to celé kolem prohlížečového API, žádná čistá logika
- **Bod 21** — profil nástroje: obrazovka `/instrument` („Kytara").
  **Karta profilu** — editovatelná (název, tělo, snímače krk/střed/kobylka
  × humbucker/single/P90, menzura, ladění, struny, poznámky), uložená
  v localStorage (Zustand persist, na načtení `sanitizeProfile`).
  **Tónový poradce** — vybereš styl (7 stylů), appka poradí snímač, gain
  a EQ přizpůsobené snímačům profilu. Logika v `src/lib/instrument.ts`
  (s testy). Poradce jen radí, nic sám nemění. Bod 20 (Mirror-AR) vědomě
  vynechán

## 📝 TODO
### MVP (Široké MVP, nutné pro v1) — body 1–13
- ✅ Body 1–13 (viz Hotovo) — **Široké MVP hotové**

### Fáze 4 — hotová
- ✅ Bod 14 (AI tutor) — chat „zeptej se", generátor cvičení, zpětná vazba
- Beta test s 5–10 lidmi — **vědomě odložený**, lze provést kdykoliv později

### Fáze 5 — Body 15–17 hotové
- ✅ Bod 15 (AlphaTab) — prohlížeč Guitar Pro / MusicXML souborů
- ✅ Bod 16 (ampsim a efekty) — kód hotový, zvuk čeká na ověření
- ✅ Bod 17 (multi-track / loop pedál) — kód hotový, zvuk čeká na ověření
- Bod 18 (stem separation) — **vědomě odložen** (placené API, serverová závislost)

### Probíhá — Fáze 6
- ✅ Bod 19 (vizuální učení — hmatníková hra, ear training)
- Bod 20 (Mirror-AR vizualizér) — **vědomě vynechán**
- ✅ Bod 21 (profil nástroje — „Kytara", tónový poradce)
- Bod 22 (sdílení a organizace) ← **další**

### Backlog (později)
- Drobnost: tvary akordů v drop ladění (odloženo z Bodu 5)
- Témově specifické widgety vzhledů — VU metr, knoby, papírový displej
  not, beat-mince (viz Klíčová rozhodnutí)

## 🏗️ Klíčová rozhodnutí
*(Aby ses k tomu zase zbytečně nevracel.)*
- **Typ aplikace:** PWA, ne nativní. Dva ze tří rešeršních dokumentů radily
  nativní appku — šli jsme vědomě cestou PWA kvůli jednomu kódu pro PC i mobil.
- **Framework:** Next.js (App Router), statický export — bez backendu.
- **Lokální start:** žádné účty, žádný server, vše v prohlížeči.
- **AI mimo MVP:** AI tutor až Fáze 4, po dokončení MVP.
- **Service worker:** ručně psaný `public/sw.js`, ne knihovna Serwist —
  `@serwist/next` nepodporuje Turbopack (výchozí builder Next.js 16).
- **Detekce výšky tónu:** Pitchy na **hlavním vlákně** (`AnalyserNode`), ne
  v AudioWorkletu — pro ladičku a detekci tónů ověřený standard a jednodušší.
- **Zpomalení v riff traineru:** nativní `preservesPitch` prohlížeče, ne
  knihovna SoundTouch.js.
- **Surgical Mode je „self-paced":** detekce jednotlivých tónů ve vlastním
  tempu, ne rytmické hodnocení v plném tempu (to je v prohlížeči nespolehlivé).
- **Perzistence:** malá data (nastavení, pokrok, YT smyčky) v localStorage
  přes Zustand persist; IndexedDB až pro velké bloby (nahrávky, Bod 11).
  Pokrok z Bodu 10 (minuty po dnech, streaky, achievementy) je vědomě taky
  v localStorage — malá strukturovaná data, ne bloby; je to odchylka od
  znění ROADMAP, které u Bodu 10 zmiňovalo IndexedDB.
- **BPM rekordy odloženy do Bodu 12:** žádná dnešní obrazovka nevyrábí
  dvojici cvičení + tempo (riff trainer jede z vlastních souborů, surgical
  mode je self-paced). Záchyt přijde s Power Chord / Gallop Trainerem;
  plumbing v `progressStore` (`recordBpm`, achievement) je hotový.
- **IndexedDB přes `idb`:** vrstva v `src/lib/recordingsDb.ts`, jeden object
  store `recordings` (blob i metadata pohromadě). Seznam se načítá bez blobů,
  blob se dotahuje až při přehrání. Délka nahrávky se měří wall-clock —
  webm/opus bloby nemají délku v hlavičce (`audio.duration` = `Infinity`).
- **Knihovna riffů je hybridní (Bod 12):** 2 ikonické riffy (Smoke on the
  Water, Seven Nation Army) + 12 originálních ve stylech rock/metal. Vědomá
  odchylka od původního seznamu pojmenovaných skladeb v ROADMAP — důvod:
  přesné taby chráněných skladeb nejdou spolehlivě ověřit a 20 přepisů by
  byl autorskoprávní problém. Originální riffy navíc cíleně učí techniky.
- **AI tutor — Gemini, BYOK, bez serveru (Bod 14):** poskytovatel Google
  Gemini (free tier, bez platební karty). Uživatel zadá vlastní klíč (BYOK).
  Volá se přímo z prohlížeče — nativní Gemini endpoint posílá CORS hlavičky,
  proxy ani backend nejsou potřeba; appka zůstává čistá statická PWA. Dřív
  jsme kvůli nesprávné informaci o CORS plánovali proxy server — ověřeno
  (OPTIONS preflight), že není třeba. Claude API se nepoužilo: jeho API je
  placené i pro uživatele s předplatným, Gemini má free tier. „Zpětná vazba
  na nahrávku" bude pojatá jako rada nad strukturovanými daty o cvičení, ne
  jako analýza surového zvuku (v prohlížeči nespolehlivá).
- **AlphaTab — vše na hlavním vlákně (Bod 15):** prohlížeč Guitar Pro /
  MusicXML běží na knihovně `@coderline/alphatab`. Vykreslování i audio
  syntéza jdou na hlavním vlákně (`core.useWorkers: false`, player
  `outputMode` ScriptProcessor) — AlphaTab je jinak citlivý na bundling
  (Web Worker, Audio Worklet) a jeho oficiální pomocník je webpack plugin,
  který s Turbopackem nesedí. Hlavní vlákno je pro jednu skladbu
  dostačující a obchází celé bundlování workerů. Font (Bravura) a soundfont
  (SONiVOX) jsou ručně zkopírované do `public/alphatab/`.
- **3 vzhledy — theme přes CSS proměnné, ne per-komponentu:** žádná
  komponenta neví, jaké téma je aktivní — čte jen CSS proměnné
  z `[data-theme]`/`[data-mode]`. Výjimky čtoucí `theme` přímo:
  `VintageFrame` (mosazné cvočky jen ve Vintage). Glassy frosted efekt
  řešen plošně CSS pravidlem na `.bg-surface*` v `globals.css` —
  vědomá odchylka od patche, který navrhoval `<Card>` komponentu
  a refactor všech karet; plošné CSS dává stejný výsledek bez
  52-souborového refactoru a bez mrtvého kódu. `text-neutral-500`
  mapováno na `text-muted` (handoff mapování ho explicitně neuvádělo).
- **3 vzhledy — co NENÍ hotové:** témově specifické widgety z mockupu
  (analogový VU metr na ladičce, otočné knoby na metronomu, papírový
  vs. LCD displej not, mosazné beat-mince) — handoff patch je
  nepokrýval, je to samostatná větší práce. Po migraci se mění barvy,
  fonty a akcenty všude; widgety zůstávají ve standardní podobě.
- **Co neděláme:** vlastní DB tabů, stem separation v prohlížeči, online
  multiplayer jam, iOS-specifické funkce.

## 📁 Mapa kódu
- `src/app/` — stránky (17 obrazovek, App Router)
- `src/components/` — React komponenty (po doménách: tuner, metronome,
  library, practice, youtube, dashboard, profile, recorder, metal, tutor,
  notation, amp, studio, looper, games, instrument). Vzhledy:
  `ThemeProvider`, `VintageFrame`, `ThemePicker`
- `src/app/theme-tokens.css` — CSS proměnné 3 vzhledů × 2 režimy
- `public/alphatab/` — font Bravura a SONiVOX soundfont pro AlphaTab (Bod 15)
- `src/audio/` — audio vrstva (AudioEngine, metronome, gallopPlayer,
  riffPlayer, pitchDetector, muteAnalyzer, chordPlayer, countIn, tunings,
  recorder, ampChain, multitrackEngine, loopPedal)
- `src/lib/` — čistá logika a data (pitch, tempo, chords, scales, lessons,
  surgicalEngine, exercises, youtube, progress, achievements, dailyPlan,
  practiceTime, recordings, recordingsDb, powerChords, metalExercises,
  metalRiffs, powerChordTrainer, gallopTrainer, palmMute, breakReminder,
  ampPresets, multitrack, wav, loopMix, fretboardGame, earTraining,
  instrument, ai/ — Gemini klient, prompty, generátor plánu, typy) — sem
  patří testy
- `src/stores/` — Zustand stores (settings, audio, metronome, lessonProgress,
  ytLoop, progress, recordings, gallopPlayer, riffPlayer, ai, chat,
  instrument)
- `ARCHITECTURE.md` · `ROADMAP.md` · `AGENTS.md` · `SKILL.md` — projektové
  dokumenty
