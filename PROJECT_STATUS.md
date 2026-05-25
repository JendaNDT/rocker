# Rocker – Project Status
*Naposled aktualizováno: 25. 05. 2026*

## 🎯 Co to je
PWA kytarový asistent pro učení a hraní na elektrickou kytaru. Funguje na PC
(kytara přes zvukovku M-Audio M-Track Solo) i na mobilu (přes mikrofon).
Živě nasazená na Vercelu: **https://rocker-rust.vercel.app** — push do `main`
spustí automatický deploy.

Stack: Next.js 16 (App Router, statický export), TypeScript 6, Tailwind 4,
Zustand, Web Audio API + getUserMedia, Vitest, ručně psaný service worker.
Perzistence: localStorage (přes Zustand persist) pro malá data včetně pokroku;
IndexedDB (přes `idb`) pro velké bloby — nahrávky.

## ⏭️ Příští krok
Roadmapa je prakticky hotová — body 1–21 dokončené (18 a 20 vědomě
vynechané), zbývá poslední **Bod 22 (sdílení a organizace)**. Nad rámec
roadmapy přibylo 5 vylepšení (vzhledy, šumová brána + autokalibrace,
upozornění na verzi, krok vzhledu v úvodu, záloha dat) — vše nasazené.

Otevřené úkoly:
- **Bod 22 (sdílení a organizace)** — poslední bod roadmapy. Záloha dat
  část „organizace" částečně pokrývá; zbývá share-a-riff a songbook.
- **Reálné testy s kytarou — na Jendovi.** **Aparát** (Bod 16) a **Studio
  / Looper** (Bod 17) jsou postavené naslepo — zvuk, latence i overdub
  NEOVĚŘENÉ. Palm Mute Studio — prahy v `useMuteDetector.ts` můžou chtít
  doladit. Hodila by se jedna seance s kytarou.
- **Beta test s 5–10 lidmi** — vědomě odložený, lze provést kdykoliv.

Pozn.: plný `npm run build` se v sandboxu nedá ověřit (EPERM — známý quirk,
viz `AGENTS.md`); ověřuje se `tsc` + Vitest. Build na Macu i na Vercelu
doběhne.

Než příští session začne: přečíst `AGENTS.md` a `SKILL.md` (pravidla
spolupráce), pak tento soubor.

## ✅ Hotovo
**334 automatických testů**, vše prochází; `tsc` čistý. Roadmapa: Fáze 0–4
hotové, Fáze 5 hotová (body 15–17; 18 vynechán), Fáze 6 z velké části
hotová (19 a 21 hotové, 20 vynechán, zbývá 22).

### Roadmapa — body 1–21
- **Bod 1 + 1b** — PWA skořápka (Next.js, manifest, service worker, 17
  obrazovek), přepínání zobrazení Automaticky/Počítač/Mobil, trvalé
  tlačítko nastavení
- **Bod 2** — audio engine: AudioContext, výběr vstupu, oprávnění,
  indikátor úrovně, měření latence
- **Bod 3** — ladička: detekce výšky tónu (Pitchy), chromatický i vedený
  režim, 6 ladění, odchylka v centech
- **Bod 4** — metronom: sample-accurate scheduler, 40–240 BPM, takty,
  subdivize, akcent, tap tempo, haptika
- **Bod 5** — knihovna: 26 akordů, 6 stupnic, přepínač leváci/praváci
- **Bod 6** — lekční engine + 12 lekcí (Stage 1–2), „try it" detekce tónu
- **Bod 7** — riff trainer: přehrávač MP3/WAV, zpomalení bez změny výšky,
  A/B smyčka, count-in, postupné zrychlování
- **Bod 8** — Přesný trénink (Surgical Mode): drill izolující problémové
  místo
- **Bod 9** — YouTube Practice Player: A/B smyčka, rychlost, štítky úseků
- **Bod 10** — sledování pokroku: odcvičené minuty, streaky, 11
  achievementů, denní plán, dashboard
- **Bod 11** — nahrávání: MediaRecorder, uložení do IndexedDB, A/B
  srovnání, markery
- **Bod 12** — rock/metal moduly: Power Chord / Gallop / Palm Mute
  trenažéry (obrazovka Metal), 14 riffů v Knihovně
- **Bod 13** — UX dokončení MVP: Wake Lock, upozornění na přestávky, Zen
  režim, notace H/B, Stojan režim, onboarding
- **Bod 14** — AI tutor (`/tutor`): chat „zeptej se", generátor cvičení,
  zpětná vazba; Google Gemini, BYOK, bez serveru
- **Bod 15** — AlphaTab (`/notation` „Noty"): prohlížeč Guitar Pro /
  MusicXML s přehráváním, kurzorem, A/B smyčkou
- **Bod 16** — ampsim a efekty (`/amp` „Aparát") — *kód hotový, zvuk
  NEOVĚŘENÝ, čeká test s kytarou*
- **Bod 17** — multi-track + loop pedál (`/studio`, `/looper`) — *kód
  hotový, zvuk NEOVĚŘENÝ, čeká test s kytarou*
- **Bod 19** — vizuální učení (`/games` „Hry"): hmatníková hra, ear
  training
- **Bod 21** — profil nástroje (`/instrument` „Kytara"): editovatelná
  karta nástroje + tónový poradce

### Nad rámec roadmapy
- **3 přepínatelné vzhledy** — Studio (tube amp, serif, mosaz), Vintage
  (staré studio, papír, mosazné cvočky), Glassy (frosted gradient), každé
  ve Světlém / Tmavém / Auto-OS režimu. Volba v `/profile → Vzhled`,
  persistuje se. CSS proměnné v `src/app/theme-tokens.css`, Tailwind v4
  `@theme` proxy v `globals.css`, `ThemeProvider` synchronizuje
  `data-theme`/`data-mode` na `<html>`. Všech 58 obrazovek migrováno
  z `bg-neutral-*` na theme-aware třídy (`bg-surface`, `text-accent`…)
- **Audit vnitřní logiky + opravy** — kompletní audit (audio, lib, AI,
  stores, komponenty), žádný kritický bug. Opraveno ve 4 dávkách:
  hydratační guardy, sanitizace `settingsStore` i `recordingsDb`,
  ošetření prázdné historie chatu a streamu v `geminiClient`, strop
  minut v `planGenerator`, `onerror` v recorderu, úklid efektů při
  odmountu, drobné audio fixy. +9 testů pro `recordingsDb` (vývojová
  závislost `fake-indexeddb`, jen pro testy)
- **Šumová brána + autokalibrace** — detekce tónů (`pitchDetector`) má
  amplitudovou bránu proti okolnímu ruchu. Citlivost posuvníkem nebo
  tlačítkem „Změřit pozadí" v Profilu → Zvuk a vstup
- **Upozornění na novou verzi** — po nasazení appka ukáže lištu „Načíst";
  `sw.js` se při buildu razítkuje unikátní verzí (`scripts/stamp-sw.mjs`),
  aby prohlížeč novou verzi poznal
- **Krok výběru vzhledu v úvodu** — onboarding má 4 kroky, druhý je výběr
  tématu
- **Záloha dat** — `/profile → Záloha dat`: export/import nastavení a
  pokroku do JSON souboru. Bez nahrávek (IndexedDB), aby zůstal malý
- **Nasazení na Vercel** — živě na `rocker-rust.vercel.app`, automatický
  deploy z `main`

## 📝 TODO
- **Bod 22 — sdílení a organizace** (poslední bod roadmapy) — share-a-riff,
  songbook organizér
- Beta test s 5–10 lidmi — vědomě odložený
- Bod 18 (stem separation) a Bod 20 (Mirror-AR) — vědomě vynechané
- Backlog: tvary akordů v drop ladění (odloženo z Bodu 5); témově
  specifické widgety vzhledů (analogový VU metr, otočné knoby, papírový
  displej not, mosazné beat-mince)

## 🐛 Známé bugy
Žádné. Audit vnitřní logiky neodhalil kritický bug; nalezené drobnosti
byly opraveny.

## 🏗️ Klíčová rozhodnutí
*(Aby ses k tomu zase zbytečně nevracel.)*
- **Typ aplikace:** PWA, ne nativní. Dva ze tří rešeršních dokumentů radily
  nativní appku — šli jsme vědomě cestou PWA kvůli jednomu kódu pro PC i mobil.
- **Framework:** Next.js (App Router), statický export — bez backendu.
- **Lokální start:** žádné účty, žádný server, vše v prohlížeči.
- **Nasazení:** Vercel, projekt propojený s GitHub repem — push do `main`
  spustí automatický deploy. AI tutor nevyžaduje server (Gemini BYOK
  přímo z prohlížeče), appka zůstává čistá statická PWA.
- **Service worker:** ručně psaný `public/sw.js`, ne knihovna Serwist —
  `@serwist/next` nepodporuje Turbopack (výchozí builder Next.js 16). SW
  novou verzi tiše nepřebírá (čeká na souhlas uživatele) a při buildu se
  razítkuje unikátní verzí, jinak by prohlížeč změnu nepoznal.
- **Detekce výšky tónu:** Pitchy na **hlavním vlákně** (`AnalyserNode`), ne
  v AudioWorkletu — pro ladičku a detekci tónů ověřený standard a jednodušší.
- **Šumová brána:** detekce tónů má amplitudovou bránu — vstup tišší než
  nastavený práh se zahodí. Práh je nastavitelný (posuvník + autokalibrace),
  ukládá se jako `noiseGate` v `settingsStore`. Hlasitý ruch přímo
  u mikrofonu software neodfiltruje — proto zůstává i ruční ovládání.
- **Zpomalení v riff traineru:** nativní `preservesPitch` prohlížeče, ne
  knihovna SoundTouch.js.
- **Surgical Mode je „self-paced":** detekce jednotlivých tónů ve vlastním
  tempu, ne rytmické hodnocení v plném tempu (to je v prohlížeči nespolehlivé).
- **Perzistence:** malá data (nastavení, pokrok, YT smyčky) v localStorage
  přes Zustand persist; IndexedDB až pro velké bloby (nahrávky, Bod 11).
  Pokrok z Bodu 10 (minuty po dnech, streaky, achievementy) je vědomě taky
  v localStorage — malá strukturovaná data, ne bloby.
- **IndexedDB přes `idb`:** vrstva v `src/lib/recordingsDb.ts`, jeden object
  store `recordings` (blob i metadata pohromadě). Délka nahrávky se měří
  wall-clock — webm/opus bloby nemají délku v hlavičce.
- **Knihovna riffů je hybridní (Bod 12):** 2 ikonické riffy (Smoke on the
  Water, Seven Nation Army) + 12 originálních ve stylech rock/metal. Vědomá
  odchylka od původního seznamu pojmenovaných skladeb — přesné taby
  chráněných skladeb nejdou spolehlivě ověřit a byl by to autorskoprávní
  problém. Originální riffy navíc cíleně učí techniky.
- **AI tutor — Gemini, BYOK, bez serveru (Bod 14):** Google Gemini (free
  tier, bez platební karty), uživatel zadá vlastní klíč. Volá se přímo
  z prohlížeče — nativní Gemini endpoint posílá CORS hlavičky, proxy ani
  backend nejsou potřeba. Claude API se nepoužilo: je placené i pro
  uživatele s předplatným, Gemini má free tier.
- **AlphaTab — vše na hlavním vlákně (Bod 15):** `core.useWorkers: false`,
  player ScriptProcessor výstup — AlphaTab je jinak citlivý na bundling
  (Web Worker, Audio Worklet) a jeho oficiální webpack plugin
  s Turbopackem nesedí. Font (Bravura) a soundfont (SONiVOX) ručně
  v `public/alphatab/`.
- **3 vzhledy — theme přes CSS proměnné, ne per-komponentu:** žádná
  komponenta neví, jaké téma je aktivní — čte jen CSS proměnné
  z `[data-theme]`/`[data-mode]`. Výjimka čtoucí `theme` přímo:
  `VintageFrame` (mosazné cvočky jen ve Vintage). Glassy frosted efekt
  řešen plošně CSS pravidlem na `.bg-surface*` v `globals.css` — bez
  refactoru všech karet na `<Card>` komponentu a bez mrtvého kódu.
- **3 vzhledy — co NENÍ hotové:** témově specifické widgety z mockupu
  (analogový VU metr na ladičce, otočné knoby na metronomu, papírový
  vs. LCD displej not, mosazné beat-mince) — handoff patch je
  nepokrýval, je to samostatná větší práce. Po migraci se mění barvy,
  fonty a akcenty všude; widgety zůstávají ve standardní podobě.
- **Záloha dat — jen localStorage, bez nahrávek:** záloha zabalí klíče
  `rocker-*` z localStorage do JSON souboru. Nahrávky (IndexedDB bloby)
  vědomě mimo — soubor by byl velký; nahrávky jdou exportovat jednotlivě.
  Obnova přepíše a přijme jen známé klíče (cizí soubor nic nepropašuje).
- **Co neděláme:** vlastní DB tabů, stem separation v prohlížeči, online
  multiplayer jam, iOS-specifické funkce.

## 📁 Mapa kódu
- `src/app/` — stránky (17 obrazovek, App Router), `globals.css`,
  `theme-tokens.css` (CSS proměnné 3 vzhledů × 2 režimy)
- `src/components/` — React komponenty (po doménách: tuner, metronome,
  library, practice, youtube, dashboard, profile, recorder, metal, tutor,
  notation, amp, studio, looper, games, instrument). Napříč appkou:
  `ThemeProvider`, `ThemePicker`, `VintageFrame` (vzhledy),
  `ServiceWorkerRegister` (registrace SW + lišta nové verze),
  `DataBackup`, `AudioInputSettings`, `InputLevelMeter`, `Onboarding`
- `src/audio/` — audio vrstva (AudioEngine, metronome, gallopPlayer,
  riffPlayer, pitchDetector — detekce tónů + šumová brána, muteAnalyzer,
  chordPlayer, countIn, tunings, recorder, ampChain, multitrackEngine,
  loopPedal)
- `src/lib/` — čistá logika a data (pitch, tempo, chords, scales, lessons,
  surgicalEngine, exercises, youtube, progress, achievements, dailyPlan,
  practiceTime, recordings, recordingsDb, powerChords, metalExercises,
  metalRiffs, powerChordTrainer, gallopTrainer, palmMute, breakReminder,
  ampPresets, multitrack, wav, loopMix, fretboardGame, earTraining,
  instrument, backup, useHydrated, ai/ — Gemini klient, prompty, generátor
  plánu, typy) — vedle leží testy `*.test.ts`
- `src/stores/` — Zustand stores (settings, audio, metronome, lessonProgress,
  ytLoop, progress, recordings, gallopPlayer, riffPlayer, ai, chat,
  instrument)
- `scripts/stamp-sw.mjs` — razítkuje `sw.js` verzí při buildu
- `public/sw.js` — ručně psaný service worker; `public/alphatab/` — font
  Bravura a SONiVOX soundfont (Bod 15)
- `ARCHITECTURE.md` · `ROADMAP.md` · `AGENTS.md` · `SKILL.md` — projektové
  dokumenty
