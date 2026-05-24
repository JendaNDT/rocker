# Rocker – Project Status
*Naposled aktualizováno: 24. 05. 2026*

## 🎯 Co to je
PWA kytarový asistent pro učení a hraní na elektrickou kytaru. Funguje na PC
(kytara přes zvukovku M-Audio M-Track Solo) i na mobilu (přes mikrofon).

Stack: Next.js 16 (App Router, statický export), TypeScript 6, Tailwind 4,
Zustand, Web Audio API + getUserMedia, Vitest, ručně psaný service worker.
Perzistence localStorage (přes Zustand persist) pro malá data včetně pokroku;
IndexedDB (přes `idb`) pro velké bloby — nahrávky, od Bodu 11.

## ⏭️ Příští krok
Hotové: Široké MVP (body 1–13), Fáze 4 (Bod 14 — AI tutor) i **Bod 15
(AlphaTab — prohlížeč Guitar Pro / MusicXML souborů)**.

Beta test s 5–10 lidmi je **vědomě odložený** (Jenda ho zatím neprovede;
lze kdykoliv později). Další podle roadmapy: **Bod 16 (ampsim a efekty)**.

Reálný test na Jendovi (`npm run dev`): AI tutor (režimy „Plán cvičení",
„Zpětná vazba") a obrazovka „Noty" — nahrání Guitar Pro souboru,
přehrávání, smyčka, tempo.

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
Fáze 1–4 dokončené, Fáze 5 rozpracovaná (Bod 15 hotový). Aktuálně **267
automatických testů**, vše prochází; `tsc` čistý.

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

## 📝 TODO
### MVP (Široké MVP, nutné pro v1) — body 1–13
- ✅ Body 1–13 (viz Hotovo) — **Široké MVP hotové**

### Fáze 4 — hotová
- ✅ Bod 14 (AI tutor) — chat „zeptej se", generátor cvičení, zpětná vazba
- Beta test s 5–10 lidmi — **vědomě odložený**, lze provést kdykoliv později

### Probíhá — Fáze 5
- ✅ Bod 15 (AlphaTab) — prohlížeč Guitar Pro / MusicXML souborů
- Bod 16 (ampsim a efekty) ← **další**

### Backlog (později) — body 16–22
- Body 16–18: ampsim, multi-track, stem separation
- Body 19–22: AR vizualizér, hmatníkové hry, profil Soltonu, sdílení
- Drobnost: tvary akordů v drop ladění (odloženo z Bodu 5)

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
- **Co neděláme:** vlastní DB tabů, stem separation v prohlížeči, online
  multiplayer jam, iOS-specifické funkce.

## 📁 Mapa kódu
- `src/app/` — stránky (12 obrazovek, App Router)
- `src/components/` — React komponenty (po doménách: tuner, metronome,
  library, practice, youtube, dashboard, profile, recorder, metal, tutor,
  notation)
- `public/alphatab/` — font Bravura a SONiVOX soundfont pro AlphaTab (Bod 15)
- `src/audio/` — audio vrstva (AudioEngine, metronome, gallopPlayer,
  riffPlayer, pitchDetector, muteAnalyzer, chordPlayer, countIn, tunings,
  recorder)
- `src/lib/` — čistá logika a data (pitch, tempo, chords, scales, lessons,
  surgicalEngine, exercises, youtube, progress, achievements, dailyPlan,
  practiceTime, recordings, recordingsDb, powerChords, metalExercises,
  metalRiffs, powerChordTrainer, gallopTrainer, palmMute, breakReminder,
  ai/ — Gemini klient, prompty, generátor plánu, typy) — sem patří testy
- `src/stores/` — Zustand stores (settings, audio, metronome, lessonProgress,
  ytLoop, progress, recordings, gallopPlayer, riffPlayer, ai, chat)
- `ARCHITECTURE.md` · `ROADMAP.md` · `AGENTS.md` · `SKILL.md` — projektové
  dokumenty
