# Rocker – Project Status
*Naposled aktualizováno: 27. 05. 2026*

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
roadmapy přibylo 9 vylepšení (vzhledy, šumová brána + autokalibrace,
upozornění na verzi, krok vzhledu v úvodu, záloha dat, rozšířený
metronom, responzivní PC rozvržení, polymorfní widgety vzhledů
inspirované Soltonem, AmpWidget na obrazovkách Cvičení a Metal) — vše
nasazené.

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
**430 automatických testů**, vše prochází; `tsc` čistý. Roadmapa: Fáze 0–4
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
- **Bod 6** — lekční engine + 18 lekcí (Stage 1–3), „try it" detekce tónu
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
- **Rozšířený metronom** — Bod 4 rozšířen: 7 taktových měr (2/4, 3/4,
  4/4, 5/4, 6/8, 7/8, 12/8), klikací akcentový vzor (každá doba přízvuk /
  normální / ztišená), 5 syntetizovaných zvuků (Klasik, Dřevo, Pípnutí,
  Cvak, Hi-hat), oddělená hlasitost metronomu a přízvuku, trenažér tempa
  (progresivní zrychlování + „cvičné ticho" / gap trainer) a presety
  s persistencí. Engine `src/audio/metronome.ts`, čistá logika
  `src/lib/metronomeConfig.ts` + `metronomePresets.ts` (s testy),
  persistovaný store `src/stores/metronomeSettingsStore.ts`, komponenty
  v `src/components/metronome/`. Zvuk a UI čekají na ruční ověření Jendou.
  **Per-beat zvuk + drum mode (2026-05-27, Fáze 1 z plánu vylepšení
  metronomu):** `MetronomeConfig` rozšířen o volitelná pole
  `soundPattern?: PerBeatSound` (per dobu null = fallback na globální
  `sound`, jinak konkrétní zvuk) a `subdivisionSound?: ClickSoundId`
  (přebíjí každý tik mezi dobami, např. hi-hat nad kick/snare). Sanitizéry
  v `metronomeConfig.ts` s fallbackem na undefined — staré presety se
  nerozbijí. Engine `metronome.ts` má `setSoundPattern`, `setBeatSound`,
  `setSubdivisionSound`; `playClick` přijímá vybraný `ClickSoundId` přes
  novou funkci `soundForTick`. UI: toggle „Pokročilé" nad přízvuky;
  dropdown subdivisionSound se zobrazí, jen když `subdivision >= 2`;
  dropdown pod každou tečkou v `BeatIndicator` s defaultem „Stejný
  (<globální>)". Per Jendovo rozhodnutí 3A se data zachovají i po vypnutí
  toggle. Power Chord Trainer explicitně resetuje `soundPattern` a
  `subdivisionSound` na undefined při startu — drum mode mu nepřeteče.
  Gallop Trainer engine nepoužívá. +16 testů (`sanitizeSoundPattern`,
  `resizeSoundPattern`, `sanitizeSubdivisionSound`, extension testy
  `sanitizeMetronomeConfig`).
  **Swing / Shuffle (2026-05-27, Fáze 2 z plánu vylepšení metronomu):**
  `MetronomeConfig` rozšířen o `swing: number` (0.5–0.75, default 0.5 =
  rovné osminy). Hodnoty: 0.5 rovné, ~0.667 jazzový triolový swing, 0.75
  ostrý dotted shuffle. Žádný extra „enabled" flag — neutrální hodnota
  stačí. Čistá lib funkce `swingedTickDuration(tickInBeat, baseDuration,
  subdivision, swing)`: sudé ticky páru dostanou `2*swing`, liché
  `2*(1-swing)`, součet páru zůstává `2*base` — beat sám zůstává přesně
  v čase, posouvají se jen vnitřní subdivize. Funguje na `subdivision ∈
  {2, 4}` (osminy a šestnáctiny); čtvrťové a trioly se nemění. Engine
  scheduler v `metronome.ts` volá `swingedTickDuration` místo statického
  `secondsPerTick`. UI v `TrainerPanel`: slider 50–75 % se zobrazí jako
  první sekce nad Ramp a Gap, s dynamickým popiskem (rovné / lehký swing /
  triolový / shuffle). Při `subdivision ∈ {1, 3}` je slider zašednutý
  s hintem „Swing funguje jen na osminy a šestnáctiny". Power Chord
  Trainer resetuje swing na 0.5 při startu (defenzivní — trenažér jede ve
  čtvrtkách, ale příprava na případnou změnu). +13 testů
  (`clampSwing`, `swingSupported`, `swingedTickDuration` ve všech
  subdivizích, extension testy `sanitizeMetronomeConfig`).
  **Count-in / Předtakt (2026-05-27, Fáze 3 z plánu vylepšení
  metronomu):** `MetronomeConfig` rozšířen o `countInBars: number`
  (0, 1, 2; default 0 = bez předtaktu). `clampCountInBars` zaokrouhluje
  a clampuje, fallback 0. Engine: nový derivovaný stav `isCountingIn:
  boolean` a `currentBar` se může pohybovat v záporném rozsahu během
  count-in. `start()` nastavuje `scheduledBar = -countInBars`. `applyRamp`
  ignoruje záporné takty (ramp trainer se aktivuje až od `bar >= 0`); gap
  trainer (přes `isSilentBar`) také záporné takty přeskakuje. Zvuk během
  count-in: stávající accent pattern + zvuk; žádné speciální tlumení —
  jednoduché a drum mode automaticky funguje (kick/snare se ozve i v
  předtaktu). UI: segmented control `Bez | 1 takt | 2 takty` v levém
  sloupci nad tlačítkem Spustit. Pod tlačítkem countdown
  „Předtakt: X/N takt(y)" s `aria-live="polite"`. Power Chord Trainer
  resetuje `countInBars = 0` při startu — má vlastní beat-counter loop.
  +6 testů (`clampCountInBars`, extension testy `sanitizeMetronomeConfig`).
- **Responzivní PC rozvržení** — všech 16 obrazovek dostalo rozvržení
  pro PC, řízené stejným přepínačem Počítač/Mobil/Automaticky jako
  navigace (`useResolvedLayoutMode`). Tři znovupoužitelné díly:
  `src/components/PageContainer.tsx` (šířka obsahu — `wide` xl→5xl,
  `focused` umírněný 3xl) a `src/components/SplitColumns.tsx`
  (dvousloupcové rozvržení, na mobilu se složí). Obrazovky se dělí na
  dvousloupcové na PC (metronom, úvod, profil, aparát, studio, kytara,
  youtube, nahrávky, cvičení), fokusované (metal, looper, hry, ladička,
  tutor) a široké (knihovna, noty). Mobil zůstává jednosloupcový.
  Čekají na ruční vizuální ověření Jendou.
- **Stage 3 — Rytmika a metalové riffy (2026-05-25)** — 6 nových lekcí
  v `src/lib/lessons.ts`: downstroke, střídavý úhoz, chugging, šestnáctky
  a gallop, hammer-on a pull-off, mini-riff. Lekce odkazují na Metal
  trenažéry (Power Chord Trainer, Gallop Trainer) — propojení výuky
  s drillem. Stage 4 (první sóla / pentatonika) jako přirozený další krok.
- **Polymorfní widgety vzhledů + Vintage = Solton (2026-05-26)** — pět
  fází, které posunuly 3 vzhledy z „barvy a fonty se mění" na „celá
  estetika se mění". Nový sdílený hook `useActiveTheme()`
  (`src/lib/useActiveTheme.ts`) zobecňuje pattern z `VintageFrame` — vrátí
  aktivní téma až po hydrataci, jinak `null`, aby widgety zvládly SSR bez
  mismatchu. Každý widget má **4 rendery**: studio, vintage, glassy
  a neutrální default (= pre-hydration fallback shodný s SSR výstupem).
  **Fáze 1 — CentsBar** (ladička): Studio chromová ručka v brass rámu,
  Vintage papírová stupnice s inkoustovou tečkou a popisky -50/0/+50,
  Glassy frosted track s neon orb halo. **Fáze 2 — InputLevelMeter:**
  Studio 18-segment LED ladder (zelená → amber → červená; top reds
  pulzují na clip), Vintage analogový VU dial s ručkou a clip LED,
  Glassy frosted track s pink→cyan gradient halo. **Fáze 3 —
  BeatIndicator** (metronom): Studio LED v mosazném prstenci, Vintage
  mosazné speed-knob mince s vyrytým číslem doby, Glassy neon orb
  s halo. **Fáze 4 — KnobControl** (nový widget v
  `src/components/widgets/`): vertikální pointer drag (200 px = celý
  rozsah), klávesnice (šipky / Shift / Home / End / PgUp / PgDn),
  zaměřený scroll wheel, ARIA `role="slider"`. Mapuje hodnotu na 270°
  sweep, bipolar mode centruje na 0°. Studio chromový válec s brass
  bezelem, Vintage mosazný Solton speed knob s vyrytým notch, Glassy
  frosted body s neon conic-gradient ringem. Nasazeno do Aparátu
  (9 parametrů: Komprese, Zkreslení, Basy/Středy/Výšky, Delay, Dozvuk,
  Chorus, Hlasitost) a do metronomu (2 hlasitosti; BPM zůstává na
  slideru — 200 hodnot by bylo na knobu příliš jemných). **Fáze 5 —
  NotationViewer rám:** vnitřní plocha vždy bílá (AlphaTab kreslí černé
  linky, na tmavém panelu by nebyly čitelné), rám se mění — Studio
  brass bezel s warm gold inset glow, Vintage brass-edged papír s grain
  texturou a vinětou, Glassy frosted neon conic ring s pink/cyan halo.
  **Vintage redesign = Solton Double Cut:** paleta tématu kompletně
  překopaná podle reálné kytary z `https://best-vintage-guitars.de/`
  — pozadí stmavlé na samet (`#0f0805` / `#050302`), surfacy přešly
  na hluboký mahagon (`#3a1e10` / `#2a1408`), `--accent` z oranžové
  (`#e8a04a`) na zlato/mosaz (`#d4a83e`), `--accent-2` přemapováno na
  cream maple, brass gradient získal 4 stops (highlight → tělo → stín →
  patina). Nové tokeny: `--maple-light`, `--mahogany-glow`,
  `--wood-grain`, `--gloss-reflex`. Pravidlo v `globals.css` aplikuje
  reflex + wood grain na `.bg-surface*` ve Vintage dark; Vintage light
  zachovává papírový pocit.
- **AmpWidget na obrazovkách Cvičení a Metal (2026-05-26)** — kompaktní
  aparát karta vedle MetronomeWidget na Cvičení a nad záložkami
  trenažérů na Metalu. Header s ON/OFF tlačítkem a stavem („Hraje —
  latence X ms" / „Vypnutý"), 4 vestavěné presety (Čistý / Crunch /
  Lead / Metal), `InputLevelMeter` (Vintage VU dial / Studio LED ladder
  / Glassy halo dle vzhledu) jen když aktivní, link „Otevřít plný
  Aparát →" na `/amp`. Vlastní presety a jednotlivé knoby zůstávají
  v plném Aparátu, kde je na to místo.

  **Architektura — `useLocalAmpChain` hook
  (`src/components/amp/useLocalAmpChain.ts`):** každá obrazovka, která
  hostuje aparát (plný `AmpStudio` na `/amp`, nebo `AmpWidget` na
  `/practice` a `/metal`), drží **vlastní krátkodobou instanci
  `AmpChain`**. Hook vrací `{active, starting, error, outputAnalyser,
  recordingStream, start, stop}` a při unmount komponentu volá
  `chain.dispose()` + odpojí source. Lokální chain je vědomé
  rozhodnutí — viz následující bod o omezení `output: 'export'`.

  **Sdílené přes `useAmpStore` (Zustand persist):** `settings`
  a `presetId`. Když si na `/amp` nastavíš tone, na Cvičení widget
  startuje s ním. `active` flag tam vědomě NENÍ — každá obrazovka má
  svůj lokální. Persist whitelist v `partialize` drží jen settings
  a presetId; rehydrate je sanituje přes `sanitizeAmpSettings`.
- **Provázání funkcí** — komplexní plán v `INTERCONNECT_PLAN.md` (4 fáze,
  od snadných výher po sjednocení audio enginů). Vznikl z auditu
  provázanosti; nic v něm nejsou opravy bugů, jen chytřejší propojení.
  **Fáze A hotová (2026-05-25):** Hry a Aparát se počítají do odcvičených
  minut; AI chat „zeptej se" dostává kontext o cvičení.
  **Fáze B hotová (2026-05-25):** ladění se nastavuje na jednom místě
  (zdroj `settingsStore.tuningId`, karta Kytary ho jen edituje); všechny
  tři AI režimy (chat, plán, zpětná vazba) znají profil nástroje.
  **Fáze C hotová (2026-05-25):** zvolené ladění respektuje hmatník
  stupnic v Knihovně (C1) i metalové trenažéry — názvy tónů a popisky
  strun (C2). Zbývá jen fáze D (sjednocení časovacích enginů) — vědomě
  otevřená jako sporná návratnost.
- **Rozšíření Aparátu** — plán v `AMP_PLAN.md` (4 fáze: vlastní presety,
  nové efekty, UX a vizualizace, pedalboard). Vznikl z přání reálného
  uživatele. **Fáze A hotová (2026-05-25):** uložitelné vlastní presety
  (`ampPresetsStore`). **Fáze B hotová (2026-05-25):** B1 kompresor,
  B2 typy zkreslení (overdrive/distortion/fuzz) a reproboxů (modern/vintage/
  open), B3 reverb, B4 chorus — vše v kódu, sluchem NEOVĚŘENO, doladí se
  podle odezvy uživatele. **Fáze C hotová (2026-05-25):** indikátory
  vstupní/výstupní úrovně, osciloskop, A/B porovnání presetů — bez nového
  DSP. Zbývá jen fáze D (přeskládatelný pedalboard) — vědomě otevřená,
  doporučení spíš vynechat.
- **Sloučení obrazovek** — plán v `SCREENS_PLAN.md` (5 fází, konsolidace
  informační architektury z přání uživatele). **Fáze A hotová
  (2026-05-25):** profil nástroje (Kytara) přesunut do Profilu jako sekce,
  `/instrument` přesměrovává. **Fáze B hotová (2026-05-25):** kompaktní
  metronom (`MetronomeWidget`) na obrazovce Cvičení, sdílí engine i nastavení
  s plným metronomem. **Fáze C hotová (2026-05-25):** Riff trainer a YouTube
  sloučeny do záložky „Hraj se skladbou" v Cvičení (přepínač zdroje),
  `/youtube` přesměrovává. **Fáze D hotová (2026-05-25):** Nahrávky, Studio
  a Looper sloučeny do obrazovky „Nahrávání" se záložkami; `/studio`
  a `/looper` přesměrovávají. **Fáze E hotová (2026-05-25):** Aparát umí
  nahrát zaaparátovaný zvuk (odbočka z výstupu → `MediaRecorder` →
  `recordingsStore`), take se objeví v Nahrávání. Plán sloučení obrazovek
  je tím celý hotový.
- **Bod 22 — sdílení a organizace** (poslední bod roadmapy) — share-a-riff,
  songbook organizér
- Beta test s 5–10 lidmi — vědomě odložený
- Bod 18 (stem separation) a Bod 20 (Mirror-AR) — vědomě vynechané
- Backlog: tvary akordů v drop ladění (odloženo z Bodu 5)

## 🐛 Známé bugy
Žádné otevřené. Audit vnitřní logiky neodhalil kritický bug; nalezené
drobnosti byly opraveny.

Opraveno z reálné zpětné vazby (2026-05-25): dvouvstupový interface
posílal dvoukanálový stream s kytarou jen v jednom kanálu — Aparát hrál
jen z jednoho repráku. `RAW_AUDIO_CONSTRAINTS` dostal `channelCount: 1`,
ale ten některé prohlížeče ignorují; spolehlivá oprava je v audio grafu
`AudioEngine.ts` — `ChannelSplitter` + `GainNode` sečtou vstupní kanály
do mono (`getInputSource()` vrací ten mono uzel), stereo výstup ho
zkopíruje do obou stran. Čeká ověření reálným testem se zvukem.
Pozn.: nahrávání ve Studiu/Looperu jede přes `MediaRecorder` napřímo ze
streamu — má teoreticky stejnou jednostrannou vadu, samostatná oprava.

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
  z `[data-theme]`/`[data-mode]`. Výjimky čtoucí `theme` přímo:
  `VintageFrame` (mosazné cvočky jen ve Vintage) a od 2026-05-26 i 5
  polymorfních widgetů (CentsBar, InputLevelMeter, BeatIndicator,
  KnobControl, NotationViewer) — viz následující bod. Glassy frosted
  efekt řešen plošně CSS pravidlem na `.bg-surface*` v `globals.css` —
  bez refactoru všech karet na `<Card>` komponentu a bez mrtvého kódu.
- **Polymorfní widgety — sdílený hook `useActiveTheme()`
  (2026-05-26):** zobecnění patternu z `VintageFrame`. Hook v
  `src/lib/useActiveTheme.ts` vrátí aktivní téma až po hydrataci, jinak
  `null`; každý polymorfní widget má 4 rendery (studio / vintage /
  glassy / default fallback shodný s SSR výstupem). Tím je SSR-safe
  bez hydratační chyby. Výjimky čtoucí téma přímo nejsou anti-pattern:
  CSS-only přístup nestačí na widgety, kde se mění **forma** (LED
  ladder vs. analogový dial vs. neon halo), ne jen barva.
- **Vintage = Solton Double Cut (2026-05-26):** Vintage dark paleta
  inspirovaná reálnou kytarou Solton Double Cut z přiloženého odkazu
  (`best-vintage-guitars.de`) — mahogany surfacy s vysokým leskem,
  brass akcent (`#d4a83e` místo původní oranžové `#e8a04a`), cream
  text (jako pickup bobbins kytary), tmavé velvet pozadí (jako
  podložka, na které je kytara fotografovaná). Vintage light zachován
  papírový — kytara nemá „světlou verzi sebe samé", kontrast je
  vědomý. Nové tokeny: `--maple-light`, `--mahogany-glow`,
  `--wood-grain`, `--gloss-reflex`. Pravidlo v `globals.css`
  aplikuje gloss + wood grain na `.bg-surface*` ve Vintage dark.
- **Záloha dat — jen localStorage, bez nahrávek:** záloha zabalí klíče
  `rocker-*` z localStorage do JSON souboru. Nahrávky (IndexedDB bloby)
  vědomě mimo — soubor by byl velký; nahrávky jdou exportovat jednotlivě.
  Obnova přepíše a přijme jen známé klíče (cizí soubor nic nepropašuje).
- **Rozšířený metronom — perzistence vedle enginu, ne v něm:** engine
  `metronome.ts` zůstává běhový zdroj pravdy. Uložené nastavení a presety
  žijí v samostatném persistovaném storu `metronomeSettingsStore`. Důvod:
  Power Chord Trainer řídí stejný engine přímo (nastavuje si 4/4 a
  čtvrťové doby) — kdyby perzistence byla v enginu, trenažér by Jendovi
  přepsal uložené nastavení. Obrazovka metronomu při otevření nahraje
  `config` ze storu do enginu a po každé uživatelské změně zapíše stav
  enginu zpět; trenažér store nečte ani nezapisuje.
- **Rozšířený metronom — zvuky syntetizované, žádné soubory:** všech 5
  zvuků (včetně Hi-hat) se syntetizuje ve Web Audio — drží se původního
  rozhodnutí Bodu 4. Hi-hat je bílý šum přes horní propust, ne navzorkovaná
  bicí souprava. Skutečný sample by znamenal zvukový soubor v `public/`;
  lze doplnit později, kdyby syntéza po poslechu nestačila.
- **Responzivní PC rozvržení — řídí se přepínačem, ne jen šířkou okna:**
  rozvržení obsahu čte `useResolvedLayoutMode()` (stejný hook jako
  navigace), takže volba Počítač/Mobil/Automaticky řídí i obsah, ne jen
  navigaci. „Auto" používá CSS breakpoint `lg:`. Mode-aware třídy se píšou
  jako objekt `{ desktop, mobile, auto }` s celými literály tříd — Tailwind
  neumí skládat názvy tříd za běhu (`grid-cols-${n}` by purge nezachytil).
  `PageContainer` má dvě velikosti: `wide` (xl→5xl, pro dvousloupcové
  obrazovky) a `focused` (3xl, pro soustředěné nástroje, kde by plná šířka
  obsah jen roztáhla).
- **Aparát — lokální chain per obrazovka, ne sdílený singleton
  (2026-05-26):** Snaha udržet AmpChain naživu napříč obrazovkami
  selhala kvůli **fyzické bariéře Next.js `output: 'export'`**: každá
  navigace přes `<Link>` je `performance.getEntriesByType("navigation")
  [0].type === "navigate"`, tj. plný document reload. Window se
  resetuje, AudioContext zhasne, žádný JS singleton (globalThis,
  module-level cache, persist trik) tuto bariéru neobejde — ověřeno.
  Místo toho každá obrazovka, která hostuje aparát, vytváří vlastní
  krátkodobou instanci přes `useLocalAmpChain`, která se dispose při
  unmount. `useAmpStore` sdílí jen settings a presetId. Přechod na
  server-rendered Next.js by problém vyřešil, ale rozbil PWA / Vercel
  free tier setup — vědomě vynecháno.
- **Co neděláme:** vlastní DB tabů, stem separation v prohlížeči, online
  multiplayer jam, iOS-specifické funkce.

## 📁 Mapa kódu
- `src/app/` — stránky (17 obrazovek, App Router), `globals.css`,
  `theme-tokens.css` (CSS proměnné 3 vzhledů × 2 režimy)
- `src/components/` — React komponenty (po doménách: tuner, metronome,
  library, practice, youtube, dashboard, profile, recorder, metal, tutor,
  notation, amp — včetně `AmpWidget` a hooku `useLocalAmpChain`, studio,
  looper, games, instrument, **widgets** — sdílené polymorfní widgety,
  dnes `KnobControl`). Napříč appkou: `ThemeProvider`, `ThemePicker`,
  `VintageFrame` (vzhledy), `ServiceWorkerRegister` (registrace SW + lišta
  nové verze), `DataBackup`, `AudioInputSettings`, `InputLevelMeter`,
  `Onboarding`, `PageContainer` + `SplitColumns` (responzivní PC rozvržení)
- `src/audio/` — audio vrstva (AudioEngine, metronome, gallopPlayer,
  riffPlayer, pitchDetector — detekce tónů + šumová brána, muteAnalyzer,
  chordPlayer, countIn, tunings, recorder, ampChain, multitrackEngine,
  loopPedal)
- `src/lib/` — čistá logika a data (pitch, tempo, metronomeConfig,
  metronomePresets, chords, scales, lessons,
  surgicalEngine, exercises, youtube, progress, achievements, dailyPlan,
  practiceTime, recordings, recordingsDb, powerChords, metalExercises,
  metalRiffs, powerChordTrainer, gallopTrainer, palmMute, breakReminder,
  ampPresets, multitrack, wav, loopMix, fretboardGame, earTraining,
  instrument, backup, useHydrated, **useActiveTheme** — sdílený hook pro
  polymorfní widgety, ai/ — Gemini klient, prompty, generátor
  plánu, kontext o cvičení, typy) — vedle leží testy `*.test.ts`
- `src/stores/` — Zustand stores (settings, audio, metronome,
  metronomeSettings, lessonProgress, ytLoop, progress, recordings,
  gallopPlayer, riffPlayer, ai, chat, instrument, ampPresets, **amp** —
  sdílené aparát settings a presetId)
- `scripts/stamp-sw.mjs` — razítkuje `sw.js` verzí při buildu
- `public/sw.js` — ručně psaný service worker; `public/alphatab/` — font
  Bravura a SONiVOX soundfont (Bod 15)
- `ARCHITECTURE.md` · `ROADMAP.md` · `AGENTS.md` · `SKILL.md` — projektové
  dokumenty
