# Rocker – Project Status
*Naposled aktualizováno: 11. 10. 2026 — sestavený Windows prototyp; historický stav PWA níže pochází z 03. 06. 2026.*

## Aktuální směr: nativní Rocker pro Windows

**Aktuální způsob spolupráce (11. 10. 2026):** Jenda chce nejprve postupně
postavit celou Windows aplikaci včetně Vintage designu a testovat až potom.
Pokračovat autonomně podle W04–W43, po jednotlivých krocích provádět automatické
kontroly a Windows sestavení, zaznamenávat výsledky a informovat o postupu.
Nežádat o ruční test ani další souhlas s každým krokem v dohodnutém rozsahu.
Ruční scénáře jsou odložené do přejímky uceleného kandidáta po W43; neověřené
chování zůstává výslovně označené. Tento pokyn je uložený i v `AGENTS.md` a
`SKILL.md` a mění předchozí čekání na ruční přejímku každého prototypu.

**Předávání testovacích verzí od 11. 10. 2026:** Jenda požaduje už sestavenou
aplikaci. Před každým předáním nejdřív Windows build, automatické kontroly a
ověření balíku; potom odkaz na ZIP s EXE. Zdroje zůstávají dostupné zvlášť.
Uživatel nemá ručně připravovat vývojové prostředí. Postup je v
[návodu Windows](windows/README.md) a [CI workflow](.github/workflows/windows-prototype.yml).

Uživatel zvolil skutečné nativní ovládání Windows a vzhled Vintage. Zvuková
cesta má používat samostatné nativní jádro; ve W02 bylo vybráno WinUI 3 / C#
a C++ / JUCE s bezplatnou AGPL/GPL cestou. Technické propojení a start okna už prošly Windows CI;
Jenda 11. 10. 2026 potvrdil skutečný poslech testovacího tónu i zastavení, opětovné spuštění a zastavení zvuku při zavření okna. Cílové zařízení pro závěrečný
test je uživatelem potvrzený M-Audio M-Track Solo s kytarou v jednom vstupu.

Uložený podklad: [úplná roadmapa W01–W48](WINDOWS_ROADMAP.md),
[32 návrhů obrazovek](docs/windows/design/vsechny-navrhy.md) a jejich
[specifikace](docs/windows/design/README.md). Vedle plánu a konceptů už existuje
[technický řez W03](windows/README.md); kompletní nativní aplikace a její
produkční zvukové jádro zatím hotové nejsou.

W01 je dokončený: [inventura a referenční podklady](docs/windows/baseline/README.md)
zachycují všech 32 pohledů, 11 stores, obsah a umělé nahrávky. Kontrola
source hashů, referenční logiky a obnovy dat prošla. W02 je dokončený:
[platforma, licence a předání](docs/windows/decisions/0001-platform-and-distribution.md)
a [výběr verzí s podklady](docs/windows/decisions/platform-selection.json).
Bratr stáhne jen instalátor; zdroje budou dostupné samostatným odkazem, bez
povinného stažení. Veřejné vydání ani placená licence pro tuto cestu nejsou potřeba.
W03 (WinUI, C++ a testovací tón) je uzavřený v rozsahu technického řezu se zaznamenanými omezeními: Windows/MSVC sestavení, C++ kontrakt,
C# integrační kontroly a automatické otevření/zavření publikovaného WinUI okna prošly.
[Sestavený ZIP pro Jendu](https://github.com/JendaNDT/rocker/releases/tag/windows-w03-preview-20261011)
má potřebné runtimy, samostatnou nabídku zdrojů a kontrolní součty.
[Protokol Windows](docs/windows/w03-windows-validation.json) odděluje ověřené sestavení
od uživatelem potvrzeného poslechu a základního ovládání i dosud neprovedených dalších ručních a hardwarových testů.
Jenda následně potvrdil Tab/Enter, 20 start/stop cyklů, oba sluchátkové výstupy
a nové spuštění po zavření během tónu. Odpojení skutečného zařízení uživatel
výslovně odmítl testovat; ruční kontrola chybějící DLL a další scénáře
v [protokolu](docs/windows/w03-manual-acceptance.md) zůstávají neověřené.
Nepodepsanou zvukovou DLL na Jendově počítači blokoval Smart App Control
(0x800711C7); přehrávání fungovalo po jeho vypnutí. Podepsaný balík se zapnutou
ochranou není ověřený. W04 je rozpracovaný: první technický řez přidává nativní notový pohled
(AlphaTab .NET / AlphaSkia), import GP7 a MusicXML, výběr taktu a export MIDI.
[Podklady W04](docs/windows/w04/README.md) oddělují implementaci od výsledků
Windows sestavení a ruční přejímky. Windows build, 22 notových kontrol a
import/vykreslení v publikovaném WinUI okně prošly v běhu 38135049339.
[Sestavené vydání W04](https://github.com/JendaNDT/rocker/releases/tag/windows-w04-notation-preview-20261011)
je zveřejněné v soukromém repozitáři; publikovaný ZIP byl znovu stažen a ověřen.
Klikání a systémové dialogy na Jendově počítači zatím potvrzené nejsou.
Nový [offline C++ mediální modul](docs/windows/w04/media.md) dekóduje
WAV/MP3/Ogg/Opus a mění rychlost se zachováním tónu pomocí Signalsmith.
Linux ověření prošlo 103/103 kontrolami. Ještě není připojený k oknu ani
společnému transportu; WebM/AAC, přehrávání not a YouTube zbývají.
Windows běh 38142313330 prošel: 103/103 mediálních kontrol, původní
kontrakty a noty, WinUI smoke i balení. Stažené interní ZIPy a všech šest
zdrojových archivů prošly kontrolou. [Protokol médií](docs/windows/w04/media-validation.json)
obsahuje skutečná měření; ruční poslech je odložený po W43.
[Původní Linux protokol](docs/windows/w03-validation.json) zůstává historickým záznamem.
Opraven byl pád při načtení XAML po publikování: projekt nyní zapíná `EnableMsixTooling`.
Vývoj a průběžné
kontroly proběhnou bez kytary; bratr dostane až funkčně kompletní testovací
Windows verzi po W43. Skutečná odezva a stabilita jeho zařízení se ověří
ve W44. První nativní implementace je technický prototyp, ne hotová aplikace pro bratra.

Při W03 znovu prošlo 561/561 testů v 54 souborech, kontrola
TypeScriptu a lint (naposledy 11. 10. 2026). To je výsledek současné PWA, nikoli ověření Windows verze.

## 🎯 Co to je
PWA kytarový asistent pro učení a hraní na elektrickou kytaru. Funguje na PC
(kytara přes zvukovku M-Audio M-Track Solo) i na mobilu (přes mikrofon).
Živě nasazená na Vercelu: **https://rocker-rust.vercel.app** — push do `main`
spustí automatický deploy.

Stack: Next.js 16 (App Router, statický export), TypeScript 6, Tailwind 4,
Zustand, Web Audio API + getUserMedia, Vitest, ručně psaný service worker.
Perzistence: localStorage (přes Zustand persist) pro malá data včetně pokroku;
IndexedDB (přes `idb`) pro velké bloby — nahrávky.

## Historický plán PWA před změnou směru
Roadmapa je prakticky hotová — body 1–21 dokončené (18 a 20 vědomě
vynechané), zbývá poslední **Bod 22 (sdílení a organizace)**. Před ním
proběhl komplexní 28krokový **FIX_PLAN refactor** (2026-06-03), všech
28 kroků hotových: appka je stabilní, odolná, dokumentace odpovídá
realitě, mobilní zážitek důstojný, kódová báze technicky čistá. Nad
rámec roadmapy přibylo 13 vln vylepšení (vzhledy, šumová brána + autokalibrace,
upozornění na verzi, krok vzhledu v úvodu, záloha dat, rozšířený
metronom, responzivní PC rozvržení, polymorfní widgety vzhledů
inspirované Soltonem, AmpWidget na obrazovkách Cvičení a Metal,
**šestifázový upgrade metronomu** s drum modem, swingem, count-in,
polyrytmem, vizuálním kyvadlem a továrními presety, **čtyřfázový
upgrade Ladičky** s referenčním tónem na strunu, konfirmací naladění,
3 novými továrními laděními a vlastními uživatelskými laděními,
**balík oprav z reálné zpětné vazby (2026-06-02)** — nahrávání do
obou repráčků, aparát hrající napříč obrazovkami přes sdílený
AmpEngine, spolehlivý export, audit validace vstupů/výstupů s opravami
a zviditelněné nahrávání v Aparátu, **komplexní audit + 28krokový
FIX_PLAN (2026-06-03)** — Live mód implementace, sdílený `LoopRunner`
helper (zavřel 3 kritické rAF leaky + duplicity), `src/hooks/` (oddělené
od `src/lib/`), kompletní backup pokrytí všech 11 perzistovaných stores,
generation guard v `audioEngine` (start/stop race), ESLint flat config
s `any`/`@ts-ignore` ban, lifecycle testy všech audio singletonů,
theme-token cleanup, a11y baterie (tap targety 48 px, focus-visible
ring, `prefers-reduced-motion`, `aria-live` regiony) — viz `AUDIT.md`
a `FIX_PLAN.md`) — vše nasazené.

Otevřené úkoly:
- **Bod 22 (sdílení a organizace)** — poslední bod roadmapy. Záloha dat
  část „organizace" částečně pokrývá; zbývá share-a-riff a songbook.
- **Reálné testy s kytarou — na Jendovi.** **Aparát** (Bod 16) a **Studio
  / Looper** (Bod 17) jsou postavené naslepo — zvuk, latence i overdub
  NEOVĚŘENÉ. Palm Mute Studio — prahy v `useMuteDetector.ts` můžou chtít
  doladit. Hodila by se jedna seance s kytarou. **Metronom** —
  šestifázový upgrade (drum mode, swing, count-in, polyrytmus, kyvadlo,
  factory presety) je kompletně postavený, Jenda po hlavní ověření jen
  jednu věc (`accentVolume`) — opraveno. Zbývá detailní reálné poslechové
  ověření drum módu, polyrytmu a kyvadla v různých vzhledech.
- **Beta test s 5–10 lidmi** — vědomě odložený, lze provést kdykoliv.

Pozn.: plný `npm run build` se v sandboxu nedá ověřit (EPERM — známý quirk,
viz `AGENTS.md`); ověřuje se `tsc` + Vitest. Build na Macu i na Vercelu
doběhne.

Než příští session začne: přečíst `AGENTS.md` a `SKILL.md` (pravidla
spolupráce), pak tento soubor.

## ✅ Hotovo
**496 automatických testů**, vše prochází; `tsc` čistý. Roadmapa: Fáze 0–4
hotové, Fáze 5 hotová (body 15–17; 18 vynechán), Fáze 6 z velké části
hotová (19 a 21 hotové, 20 vynechán, zbývá 22).

### Roadmapa — body 1–21
- **Bod 1 + 1b** — PWA skořápka (Next.js, manifest, service worker, 17
  obrazovek), přepínání zobrazení Automaticky/Počítač/Mobil, trvalé
  tlačítko nastavení
- **Bod 2** — audio engine: AudioContext, výběr vstupu, oprávnění,
  indikátor úrovně, měření latence
- **Bod 3** — ladička: detekce výšky tónu (Pitchy), chromatický i vedený
  režim, 9 továrních ladění + uživatelská vlastní ladění (viz
  Čtyřfázový upgrade Ladičky níže), odchylka v centech
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
  **Polyrytmus (2026-05-27, Fáze 4 z plánu vylepšení metronomu):**
  `MetronomeConfig` rozšířen o `polyrhythm: PolyrhythmConfig` (enabled,
  ticksPerBeat 2–7, sound, volume 0–1; default off / 3 ticky / hi-hat /
  0.6). `sanitizePolyrhythm` s fallback na default — staré configy beze
  změny. Engine: druhá nezávislá smyčka v `scheduler()` plánuje
  sekundární ticky s vlastním `60 / bpm / ticksPerBeat` intervalem. Při
  `start()` fázově zarovnaná na hlavní downbeat. Tichá během count-in
  (`bar < 0`) a `isSilentBar` (gap) — polyrytmus je extra barva, ne
  pomůcka pro orientaci. Hlasitost = `polyrhythm.volume * volume * 0.5`
  (headroom). Žádné akcenty — všechny sekundární ticky stejné. Vlastní
  syntéza v `playSecondaryTick` (laděné nižší než hlavní vrstva, aby se
  oddělila). Nová komponenta `PolyrhythmPanel.tsx` v
  `src/components/metronome/` — checkbox aktivace, segmented control 2–7,
  dropdown zvuku, slider hlasitosti, dynamický popisek („3 not proti 4
  hlavního metronomu"). Vložená dolů pod Presety v pravém sloupci. Power
  Chord Trainer vypíná polyrytmus při startu. +7 testů (`sanitizePolyrhythm`
  pro garbage / clamp ticksPerBeat / clamp volume / sound fallback /
  enabled coercion, extension testy `sanitizeMetronomeConfig`).
  **Vizuální kyvadlo / Pendulum (2026-05-27, Fáze 5 z plánu vylepšení
  metronomu):** nová polymorfní komponenta `Pendulum.tsx` v
  `src/components/metronome/` — čtyři rendery přes `useActiveTheme()`:
  Studio chromová tyčka s mosazným závažím v dark brass bezelu, Vintage
  mahogany krabička s brass arm a vyrytým coin závažím (Solton estetika),
  Glassy frosted neon stopa s pink halo, default minimalistický
  fallback. Animace v rAF loopu přes ref (přímá DOM manipulace
  `style.transform`, ne `useState` per frame). Sinusoidální easing
  (`sin((phase-0.5)*π)`) dává klasický Wittner feel — pomalé na krajích,
  rychlé uprostřed. Jedna plná sekvence = 2 doby (sudé beaty kývou
  vlevo→vpravo, liché zpět). Rozsah ±45°. Když metronom nehraje,
  kyvadlo zaparkuje uprostřed (0°). Subdivize ignorovány — kyvadlo se
  kýve **po dobách**, ne po 16. notách. Akcent zatím vizuálně
  nezvýrazněný (uživatel akcent slyší). Persistence: nová pole
  `visualMode: "dots" | "pendulum"` přímo v `metronomeSettingsStore`
  **mimo `config`** — globální preference, ne per-preset (uživatel má
  typicky jednu oblíbenou vizualizaci, nemíchá ji s presety).
  `sanitizeVisualMode` vyexportován pro testy. UI: segmented control
  `Body | Kyvadlo` v sekci Přízvuky vedle tlačítka „Pokročilé".
  V `MetronomeWidget` na obrazovce Cvičení zůstávají body (kompaktní,
  bez toggle). +3 testy (`sanitizeVisualMode`). **Polish (2026-05-27):**
  kyvadlo zvětšeno z 160×140 na **240×220 px**, arm ze 100 na **170 px**
  (proporcionálně i závaží a pivot ve všech 4 vzhledech) — autentičtější
  „kus dřeva před tebou". **Accent flash na akcentní dobu:** každá doba,
  která má v accentPatternu „accent", spustí krátký 260 ms záblesk přes
  `filter: brightness(1.5) saturate(1.2)`. CSS keyframe
  `pendulum-accent-flash` v `globals.css` (univerzální, funguje stejně
  na všech vzhledech přes filter). JS toggle v Pendulum přes class
  add/remove se setTimeout. Force reflow přes `void box.offsetWidth`
  restartuje animaci i při dvou akcentech back-to-back.
  **Fix accentVolume — extra-gain model (2026-05-27):** stará rovnice
  `accent = 0.9 * accentVolume` měla problém: rozdíl mezi akcentem
  (max 0.9) a normální dobou (fixní 0.6) byl jen ~1.5× (~+3.5 dB) a při
  snižování klesl akcent pod normální (~67 % → akcent rovný normálu,
  pod tím tišší). Uživatel tipoval, že se nic nemění.
  Nová rovnice `accent = 0.6 + 0.4 * accentVolume`: 0 % = stejně jako
  normální doba (bez přízvuku), 100 % = 1.0 (~+4.5 dB nad normálem,
  výrazný akcent). Intuitivnější — snížení = menší rozdíl, ne mizející
  doba. Pro úplné ztišení akcentní doby slouží stávající „muted" stav
  v BeatIndicatoru.
  **Tovární presety (2026-05-27, Fáze 6 z plánu vylepšení metronomu,
  poslední):** nová konstanta `FACTORY_PRESETS: readonly
  MetronomePreset[]` v `metronomePresets.ts` se šesti predefinovanými
  presety, které využívají všechny předchozí fáze: **Blues shuffle**
  (4/4 osminy, swing 2/3, kick/snare + hi-hat na osminy), **Metal
  gallop** (4/4 šestnáctky, ramp 130→200 po 8 taktech, kick/snare),
  **Funk groove** (4/4 šestnáctky, accent na 2+4, snare na 2+4, hi-hat
  na 16.), **Waltz 3/4** (3/4 čtvrtky, accent jen na 1), **Jazz swing**
  (4/4 osminy, swing 2/3, hi-hat na osminy), **Polyrytmus 3 proti 4**
  (4/4 šestnáctky, polyrytmus 3 ticks/beat). Aproximace bicích zvuků
  bere existujících 5 syntetizovaných: kick = „Cvak", snare = „Dřevo",
  hi-hat = „Hi-hat". Helper `isFactoryPreset(id)` rozliší podle prefixu
  `factory-`. Tovární presety procházejí přes `sanitizeMetronomeConfig`,
  takže zůstávají validní i pokud se defaulty pohnou v dalším release.
  UI: `PresetPanel` rozdělen na dvě sekce — **Tovární** nahoře
  (read-only, jen tlačítko Použít) a **Moje** níže (stávající chování
  s Uložit + Smazat). Tovární nejdou smazat — defenzivní guard
  `isFactoryPreset(id)` v `handleDeletePreset` zahodí pokus, i kdyby
  staré uložená data propašovala factory id do uživatelského seznamu.
  Když uživatel chce vlastní verzi továrního presetu, klikne Použít,
  upraví a uloží pod novým jménem přes stávající input. +9 testů
  (`isFactoryPreset`, FACTORY_PRESETS integrita, ids, swing/ramp/poly
  obsah).

  **Tím je 6-fázový plán vylepšení metronomu hotový.** Bonus přibyly:
  per-beat zvuk (drum mode), swing/shuffle, count-in, polyrytmus,
  vizuální kyvadlo, 6 továrních presetů. Bonus k Bodu 4 z roadmapy.
  Vše čeká na ruční ověření Jendou.
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

  **Pozn. (2026-06-02): `useLocalAmpChain` byl nahrazen sdíleným
  `AmpEngine` singletonem** — aparát teď žije v `AppShellu` a hraje
  napříč obrazovkami, `active` stav drží engine. Tento odstavec popisuje
  původní (superseded) návrh; aktuální viz „Refaktor Aparátu" výše
  a Klíčová rozhodnutí.
- **Čtyřfázový upgrade Ladičky (2026-06-02)** — Ladička (Bod 3)
  rozšířena ve čtyřech postupných commitech. Bonus k Bodu 3 z roadmapy.
  Vše nasazené, čeká reálný test s kytarou.

  **Fáze 1 — Referenční tón cílové struny:** vedle každé struny ve
  Vedeném režimu malé tlačítko ♪. Přidržíš → modul `referenceTone.ts`
  spustí krátký kytarově obarvený tón (sinus fundamental + 2. harmonika
  s gainem 0.3 + 3. harmonika s gainem 0.15) cílové výšky. Vlastní lazy
  `AudioContext` (nezávislý na `audioEngine`, takže funguje i bez
  spuštěné ladičky). Attack/release envelope 30/50 ms, safety cap 5 s
  (kdyby pointerUp nedorazil). Pointer Events: `onPointerDown/Up/Leave/
  Cancel` + `onBlur` pojistka. Hold-to-play. Hlasitost (default 30 %)
  posuvníkem v Profilu → Ladička, čtená v komponentě přes `useRef`
  ze Zustand subscribe, takže pohyb posuvníkem se projeví okamžitě.
  +6 testů (clampReferenceVolume, isPlayableFrequency).

  **Fáze 2 — Konfirmace naladění (zvuk + haptika):** když struna ve
  Vedeném režimu vydrží v zóně ±5 centů po dobu 400 ms, spustí se
  krátký dvounótový chime (1320 Hz → 1980 Hz, sequence ~250 ms, modul
  `confirmationSound.ts` — vlastní lazy `AudioContext`, module-singleton
  player) plus `navigator.vibrate(60)`. Re-trigger logika přes
  `confirmedIndexRef`: stejná struna v zóně se pípne jen jednou, reset
  při výpadku ze zóny nebo přepnutí na jinou strunu. Toggles
  `tunerConfirmSound` a `tunerConfirmHaptic` (oboje default **vypnuto**)
  v Profilu → Ladička. Haptický checkbox se sám disabluje na zařízení
  bez `navigator.vibrate` (iOS Safari) s popiskem „(toto zařízení
  vibrace nepodporuje)". +7 testů (clamp, no-window guard, haptic
  detection + invocation + exception swallowing).

  **Fáze 3A — Tři nová tovární ladění:** **Open G** (D G D G B D),
  **Open D** (D A D F# A D), **DADGAD** (D A D G A D) přidány na
  konec `TUNINGS` v `src/audio/tunings.ts`. Open E a Open A vědomě
  vynechány — obě jsou „vysoká" ladění, na elektrice s tenčími drátky
  hrozí napětí strun; uživatel si je může vyrobit jako vlastní (Fáze
  3B). Plus defenzivní invariant test ve všech továrních laděních
  (MIDI rostoucí low→high). +5 testů.

  **Fáze 3B — Vlastní uživatelská ladění:** nový store
  `customTuningsStore` (Zustand persist, klíč `rocker-custom-tunings`)
  s `addTuning`, `removeTuning`, defenzivní `sanitizeList` v `merge`
  (drop garbage, dedupe id, cap MAX 20, MIDI range 24–84). ID format
  `custom-<crypto.randomUUID()>` s fallbackem na `custom-<timestamp>-
  <random>` pro starší prohlížeče. Nový helper `src/lib/resolveTuning.
  ts` jako **jediný zdroj pravdy pro lookup ladění** — bere id a pole
  custom tunings, vrátí Tuning bez ohledu na to, jestli je tovární
  nebo vlastní; graceful fallback na `standard` pro neznámé id
  (včetně právě smazaného custom). Nová komponenta
  `CustomTuningEditor` — inline panel pod selectem (žádný modal),
  6 strun s tlačítky `−` `+` o půltón, MIDI clamp 24–84, frekvence
  v Hz a indikátor „(původně E2)", validace neprázdného názvu (po
  trim), Uložit/Zrušit/Reset. Edit existujícího vlastního ladění
  vědomě **není** podporován — uživatel klikne „Vytvořit vlastní…"
  se selectovaným ladění jako základem, upraví, uloží jako nové,
  staré smaže. Jednodušší UI, méně edge cases. `tuner/page.tsx`
  rozšířena: select s `<optgroup>` Tovární / Vlastní, tlačítka
  „Vytvořit vlastní…" a „Smazat (s confirm dialogem)". Smazání
  aktivního vlastního ladění přepíná `setTuningId(DEFAULT_TUNING_ID)`
  — žádné zombie id ve storage. **Provázanost:** 6 callsitů, které
  dosud volaly `getTuning(id)` přímo, přepnuto na `resolveTuning`:
  `library/page.tsx` (Knihovna stupnic), všechny tři tutor panely
  (`PlanPanel`, `ChatPanel`, `FeedbackPanel` — AI kontext o ladění),
  `PowerChordTrainer` a `GallopTrainer` (metal trenažéry). Vlastní
  ladění tak doteče i tam — bez dalšího drátku. `settingsStore.merge`
  toleruje `tuningId` začínající `custom-` (jinak by se aktivní
  vlastní ladění při startu appky resetovalo). +22 testů (16 v
  `customTuningsStore.test.ts` — sanitizace garbage / out-of-range
  MIDI / non-custom prefix / empty label / dedupe / trim / cap,
  add+remove API; 6 v `resolveTuning.test.ts` — factory / custom /
  deleted custom / empty customs / order).
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
- **Nahrávání do obou repráčků (2026-06-02)** — z reálné zpětné vazby:
  Nahrávky, Studio i Looper braly syrový vstup z `getInputStream()`.
  Dvouvstupová zvukovka (M-Track Solo) posílá kytaru v jednom kanálu, takže
  nahrávka hrála jen z jednoho repráčku. `AudioEngine` nově vystavuje
  `getRecordingStream()` — mono-sečtený `MediaStreamAudioDestinationNode`
  napojený na existující `monoMix`. Tři nahrávací cesty (`RecorderControls`,
  `multitrackEngine`, `loopPedal`) přepnuty na něj; kytara se sečte do mono
  a zní z obou repráčků. Aparát má vlastní (zaaparátovaný) stream beze změny.
- **Refaktor Aparátu — sdílený AmpEngine přes obrazovky (2026-06-02)** —
  aparát už nežije v komponentě obrazovky (`useLocalAmpChain`, který ho
  rušil při každé navigaci), ale v **modul-level singletonu `AmpEngine`**
  (`src/audio/ampEngine.ts`) — stejný vzor jako `audioEngine`. Komponenta
  `AmpHost` v `AppShellu` synchronizuje `ampStore.settings` do enginu; hook
  `useAmpEngine` zrcadlí stav pro konzumenty (`AmpStudio`, `AmpWidget`).
  Protože singleton i `AppShell` přežijí client-side navigaci, aparát hraje
  dál i po přepnutí obrazovky a widget na Cvičení/Metalu zrcadlí tentýž
  běžící aparát. `useLocalAmpChain` odstraněn. Doplněn `onunmount` guard
  v `AmpStudio`, který dokončí a uloží probíhající nahrávání (engine žije
  dál). **Service worker** (`public/sw.js`) navíc servíruje Next.js RSC
  data (`.txt` payloady) network-first, aby přepnutí obrazovky nepadalo na
  full reload po deploy. Tím padlo dřívější (chybné) přesvědčení, že
  „static export = full reload, audio přes obrazovky nemožné" — viz
  Klíčová rozhodnutí.
- **Spolehlivý export (2026-06-02)** — z reálné zpětné vazby „nemohu
  exportovat nahranou věc". Příčina: export rušil `URL.revokeObjectURL`
  synchronně hned po kliknutí, což u větší nahrávky stažení zruší dřív,
  než začne. Nový helper `src/lib/download.ts` (`downloadBlob`) připojí
  odkaz do DOM (kvůli Firefoxu) a URL uvolní až po prodlevě. Použit
  v `RecordingItem` (export nahrávky), `Studio` (export mixu) a
  `DataBackup` (záloha) — Studio a záloha navíc dřív ani nepřipojovaly
  odkaz do stránky.
- **Audit a opravy validace vstupů/výstupů (2026-06-02)** — kompletní
  audit (perzistence, soubory, externí, UI). Většina vzorová; dvě reálné
  díry zaceleny: `lessonProgressStore` a `ytLoopStore` neměly `merge`
  guard (corrupt data mohla shodit dashboard/tutor, resp. YouTube
  přehrávač). Nové čisté sanitizéry `src/lib/lessonProgress.ts`
  a `src/lib/ytLoops.ts` (s testy) zapojené do `merge` obou stores —
  tím mají guard všechny persist stores. Plus dvě UX hlášky: `RiffTrainer`
  (`<audio onError>`) a YouTube přehrávač (`onError` IFrame API) teď
  hlásí selhání přehrávání místo tichého nic.
- **Zviditelněné nahrávání v Aparátu (2026-06-02)** — blok „Nahrát
  zaaparátovaný zvuk" v `AmpStudio` posunut nad metry, accent rámeček
  a vysvětlivka (zachytí zvuk po efektech, na rozdíl od sekce Nahrávání =
  suchý vstup; uloží do Nahrávání).
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
Pozn.: stejná jednostranná vada u samostatného nahrávání (Nahrávky,
Studio, Looper) je od 2026-06-02 také opravena — recorder bere
mono-sečtený `getRecordingStream()` místo syrového streamu (viz
„Nahrávání do obou repráčků" v Hotovo). Vše čeká reálný poslechový test.

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
- **Aparát — sdílený AmpEngine singleton v AppShellu (2026-06-02,
  nahrazuje dřívější „lokální chain per obrazovka"):** Dřívější verze
  tvrdila, že `output: 'export'` dělá z každé `<Link>` navigace plný
  document reload, takže aparát nemůže přežít přechod mezi obrazovkami.
  **To bylo nepřesné.** Next.js i u statického exportu přepíná obrazovky
  client-side (soft navigace), takže `AppShell` i modul-level singletony
  navigaci přežijí. Aparát teď žije v singletonu `AmpEngine`
  (`src/audio/ampEngine.ts`, vzor jako `audioEngine`); `AmpHost`
  v `AppShellu` do něj synchronizuje settings, hook `useAmpEngine` ho
  zrcadlí. `AmpStudio` i `AmpWidget` ovládají tentýž běžící chain, takže
  aparát hraje napříč obrazovkami a widget zrcadlí jeho stav. Dřívější
  pozorovaný „full reload" nastával jen **po nasazení nové verze** —
  známý Next.js jev (selhání fetche RSC payloadu), zhoršený tím, že
  service worker servíroval RSC `.txt` data cache-first. Oprava: SW je
  teď servíruje network-first (`public/sw.js`). `useAmpStore` dál sdílí
  jen settings a presetId (persist), aktivní stav drží engine.
  `useLocalAmpChain` odstraněn. Přechod na server-rendered Next.js není
  potřeba — appka zůstává statická PWA na Vercelu.
- **Ladění — `resolveTuning` jako jediný zdroj pravdy (2026-06-02):**
  s příchodem vlastních uživatelských ladění (Fáze 3B) bylo potřeba
  místo dosavadního `getTuning(id)` (čistě tovární lookup) zavést
  helper, který bere id i pole vlastních ladění a vrátí Tuning bez
  ohledu na zdroj. Žádná komponenta už **přímo nevolá `getTuning`** —
  jen `resolveTuning(id, customs)` (případně `resolveAllTunings` pro
  select boxy). Fallback na `standard` pro neznámé id (včetně právě
  smazaného vlastního) drží appku v konzistentním stavu, i kdyby
  uživatel ručně přepsal localStorage. Vedlejší úprava:
  `settingsStore.merge` toleruje `tuningId` začínající `custom-` —
  jinak by rehydratace zahodila aktivní vlastní ladění. `resolveTuning`
  ho gracefully obhospodaří, kdyby ho mezitím někdo smazal.
  Vlastní ladění tak doteče do Knihovny stupnic, AI tutora i metalových
  trenažérů — bez dalšího drátku, díky propojení z Fáze C interconnectu
  (`settingsStore.tuningId` jako jediný zdroj id).
- **Ladička — vlastní `AudioContext` per modul (referenceTone,
  confirmationSound, 2026-06-02):** moduly Ladičky (referenční tón
  na strunu, konfirmační chime) si **nesahají na `audioEngine`** —
  každý drží vlastní lazy `AudioContext`. Důvod: obě funkce musí
  fungovat, i když uživatel Ladičku nespustil (mikrofon není potřeba
  pro přehrání cílového tónu) nebo když `audioEngine` mezitím
  zastavil. AudioContext se vytvoří až při první uživatelské
  interakci (browsery policy). Drobná režie navíc (dva extra kontexty
  vedle hlavního), ale moduly jsou tak plně izolované a testovatelné.
- **Co neděláme:** vlastní DB tabů, stem separation v prohlížeči, online
  multiplayer jam, iOS-specifické funkce.

## 📁 Mapa kódu
- `src/app/` — stránky (13 obrazovek v navu + 4 legacy redirecty pro
  staré URL, App Router), `globals.css`, `theme-tokens.css` (CSS proměnné
  3 vzhledů × 2 režimy)
- `src/components/` — React komponenty (po doménách: tuner, metronome,
  library, practice, youtube, dashboard, profile, recorder, metal, tutor,
  notation, amp — včetně `AmpWidget`, `AmpHost` (drží sdílený aparát
  v AppShellu) a hooku `useAmpEngine`, studio,
  looper, games, instrument, **widgets** — sdílené polymorfní widgety,
  dnes `KnobControl`). Napříč appkou: `ThemeProvider`, `ThemePicker`,
  `VintageFrame` (vzhledy), `ServiceWorkerRegister` (registrace SW + lišta
  nové verze), `DataBackup`, `AudioInputSettings`, `InputLevelMeter`,
  `Onboarding`, `PageContainer` + `SplitColumns` (responzivní PC rozvržení)
- `src/audio/` — audio vrstva (**audioEngine** (camelCase soubor;
  obsahuje třídu `AudioEngine`), metronome, gallopPlayer, riffPlayer,
  **loopRunner** — sdílený "two clocks" lifecycle helper pro 3
  přehrávače výše, pitchDetector — detekce tónů + šumová brána,
  muteAnalyzer, chordPlayer, countIn, tunings, recorder, ampChain,
  **ampEngine** — sdílený singleton aparátu žijící přes obrazovky,
  multitrackEngine, loopPedal, **referenceTone** — hold-to-play
  referenční tón pro Ladičku, **confirmationSound** — dvounótový chime
  + haptika pro konfirmaci naladění). Každý singleton má lifecycle
  test (`*.test.ts` vedle něj).
- `src/lib/` — čistá logika a data, **bez Reactu** (pitch, tempo,
  metronomeConfig, metronomePresets, chords, scales, lessons,
  surgicalEngine, exercises, youtube, progress, achievements, dailyPlan,
  practiceTime, recordings, recordingsDb, powerChords, metalExercises,
  metalRiffs, powerChordTrainer, gallopTrainer, palmMute, breakReminder,
  ampPresets, multitrack, wav, loopMix, fretboardGame, earTraining,
  instrument, backup, **math** — `clampGainOrFull` / `clampGainOrSilent`
  s explicitními fallbacky, **customTunings** — typ + `isCustomTuningId`
  (oddělené ze store, vyhnutí cyklu), **download** — spolehlivé stažení
  blobu, **lessonProgress** + **ytLoops** — sanitizéry perzistence,
  **resolveTuning** — jediný zdroj pravdy pro lookup ladění (tovární
  i vlastní), ai/ — Gemini klient, prompty, generátor plánu, kontext
  o cvičení, typy) — vedle leží testy `*.test.ts`
- `src/hooks/` — sdílené React hooky oddělené od `src/lib/` (`useHydrated`,
  `useActiveTheme`, `useResolvedLayoutMode`, `useWakeLock`)
- `src/stores/` — Zustand stores (settings, audio, metronome,
  metronomeSettings, lessonProgress, ytLoop, progress, recordings,
  gallopPlayer, riffPlayer, ai, chat, instrument, ampPresets, **amp** —
  sdílené aparát settings a presetId, **ampEngine** — zrcadlo stavu
  sdíleného AmpEngine, **customTunings** — uživatelská
  vlastní ladění s defenzivní sanitizací v rehydraci)
- `scripts/stamp-sw.mjs` — razítkuje `sw.js` verzí při buildu
- `public/sw.js` — ručně psaný service worker; `public/alphatab/` — font
  Bravura a SONiVOX soundfont (Bod 15)
- `ARCHITECTURE.md` · `ROADMAP.md` · `AGENTS.md` · `SKILL.md` — projektové
  dokumenty
