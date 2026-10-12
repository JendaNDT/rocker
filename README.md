# Rocker

> PWA kytarový asistent pro učení a hraní na elektrickou kytaru.
> Jeden zdroj, mobil i desktop, žádný server.

**Živá verze:** https://rocker-rust.vercel.app

**Nativní Windows verze 0.2.0:** implementovaný je rozsah W04–W43 — všech 32 pohledů ve Vintage designu, WinUI 3 ovládání, C++/JUCE zvukové jádro, výuka, nástroje, nahrávky, Looper, Studio, data a instalátor. Závěrečné automatické sestavení a instalační přejímka prošly. [Stáhnout hotový instalátor](https://github.com/JendaNDT/rocker/releases/tag/windows-0.2.0-rc1-20261012); [konkrétní Windows důkaz](docs/windows/w43/windows-validation.json). [Návod a matice úplnosti](docs/windows/w43/README.md), [aktuální stav](PROJECT_STATUS.md), [roadmapa](WINDOWS_ROADMAP.md) a [skutečné snímky všech obrazovek](docs/windows/w36/native-gallery/index.html).

Bratr dostane sestavený instalátor; zdroje jsou samostatně a nic nemusí kompilovat. Skutečné hraní na M-Track Solo a měření celkové odezvy následují ve W44. [Původní návrhy](docs/windows/design/vsechny-navrhy.md) zůstávají referencí. Main a živá PWA se nemění; následující části popisují původní webovou aplikaci.

## Co to je

Rocker je progresivní webová aplikace, která pomáhá s rock/metal kytarou.
Vše běží přímo v prohlížeči — na PC se zvukovkou (testováno na M-Audio
M-Track Solo) i na mobilu přes mikrofon. Žádný backend, žádné účty.
Naladíš se, hraješ s metronomem, otevřeš si tab z Guitar Pro souboru,
drilluješ power chordy, máš AI tutora po ruce s vlastním Gemini API
klíčem.

Aplikace má tři přepínatelné vzhledy: **Studio** (tube amp, mosaz,
serify), **Vintage** (paleta inspirovaná kytarou Solton Double Cut —
mahogany, brass, cream) a **Glassy** (frosted gradient s neon akcenty).
Každý vzhled má kromě barev i odlišnou formu klíčových widgetů — VU
metr, knoby, beat-mince, rám not.

## Funkce

- **Domů** — denní plán cvičení (~15 min), streaky, 11 achievementů,
  primární CTA „Hraj teď" rovnou do Live módu
- **Live** — hraní s kytarou v ruce: velká ladička, velký BPM s play/tap
  a jeden Rec toggle. Žádné menu, Wake Lock automatický
- **Ladička** — Pitchy detekce, vedený i chromatický režim, 9 továrních
  ladění (Standard, Drop D/C/B, Eb std, D std, Open G, Open D, DADGAD)
  + vlastní uživatelská ladění, **referenční tón na strunu**
  (hold-to-play, kytarově obarvený), zvuková a haptická **konfirmace
  naladění** (volitelné), šumová brána s autokalibrací
- **Metronom** — sample-accurate scheduler, 7 taktových měr, presety,
  trenažér tempa s progresivním zrychlováním a „cvičným tichem", swing /
  shuffle feel (50–75 %), počítadlo předtaktu (count-in), drum mode
  (kick/snare/hi-hat per dobu), polyrytmus (3 proti 4 cvičení),
  alternativní vizualizace **Wittner kyvadlem** s accent flashem,
  6 továrních presetů (Blues shuffle, Metal gallop, Funk groove…)
- **Cvičení** — Riff Trainer + YouTube Practice Player (A/B smyčka,
  slow-down) + Surgical Mode (drill problémového místa)
- **AI tutor** — chat „zeptej se", generátor cvičení, zpětná vazba —
  Google Gemini, BYOK, přímo z prohlížeče
- **Hry** — hmatníková hra a ear training
- **Metal** — Power Chord, Gallop a Palm Mute trenažéry
- **Aparát** — efektový řetězec (komprese, zkreslení, EQ, delay,
  dozvuk, chorus), presety + vlastní. Mini `AmpWidget` (ON/OFF
  + 4 vestavěné presety) sedí i v Cvičení a Metalu, sdílí nastavení.
  Aparát hraje napříč obrazovkami (sdílený engine) a umí nahrát
  zaaparátovaný zvuk.
- **Knihovna** — 26 akordů, 6 stupnic, 14 riffů (2 ikonické +
  12 originálních ve stylu rock/metal)
- **Noty** — AlphaTab prohlížeč Guitar Pro / MusicXML s přehráváním
- **Nahrávání** — multi-track, loop pedál, mixdown do WAV

Lekce: 18 vestavěných lekcí (Stage 1–3) s „Try it" detekcí tónu.

## Stack

- **Framework**: Next.js 16 (App Router, statický export)
- **Jazyk**: TypeScript 6
- **Styling**: Tailwind CSS v4 + CSS proměnné pro 3 vzhledy
- **State**: Zustand 5 (Zustand persist → localStorage)
- **Audio**: Web Audio API + getUserMedia, detekce tónů přes Pitchy
- **Notace**: @coderline/alphatab (SONiVOX soundfont)
- **AI**: Google Gemini API (BYOK, bez proxy serveru)
- **Testy**: Vitest (561 testů; čistá logika v `src/lib/` + lifecycle
  testy všech audio singletonů)
- **Lint**: ESLint 9 (flat config) — zákaz `any` a `@ts-ignore`
- **PWA**: ručně psaný service worker, manifest, instalovatelnost
- **Nasazení**: Vercel, auto-deploy z `main`

## Vývoj lokálně

Potřebuješ Node 22+ a npm.

```bash
npm install
npm run dev        # http://localhost:3000
```

Po každé větší změně:

```bash
npx tsc --noEmit   # kontrola typů
npm test           # vitest run
npm run lint       # eslint — zákaz `any` a `@ts-ignore`
npm run build      # next build + razítkování service workeru
```

## Architektura

Sedm vrstev — vrstva zná jen tu pod sebou. Čistá logika v `src/lib/` je
TypeScript bez Reactu a bez audia (aby šla testovat); React hooks žijí
zvlášť v `src/hooks/`. Detaily a důvody najdeš v ostatních dokumentech
repa:

- `ARCHITECTURE.md` — vrstvy, audio graf, perzistence
- `AGENTS.md` — pravidla projektu, konvence, lessons learned
- `SKILL.md` — pravidla spolupráce s Claudem (vibecoding)
- `ROADMAP.md` — číslované body roadmapy
- `PROJECT_STATUS.md` — aktuální stav, klíčová rozhodnutí, mapa kódu
- `AUDIT.md` + `FIX_PLAN.md` — komplexní audit kódu (2026-06-02)
  a 28-krokový implementační plán; všechny kroky dokončené 2026-06-03

## Roadmap (zkráceně)

- ✅ **Fáze 0–4** (MVP): PWA skořápka, audio jádro, ladička, metronom,
  knihovna, lekce, riff trainer, YouTube player, surgical mode, pokrok,
  nahrávání, rock/metal moduly, UX dokončení, AI tutor
- ✅ **Fáze 5** (pokročilý zvuk): AlphaTab noty, ampsim, multi-track
  (Bod 18 — stem separation vědomě vynecháno)
- ✅ **Fáze 6** (rozšíření): Hry, profil nástroje (Bod 20 — Mirror-AR
  vědomě vynecháno)
- ⬜ **Bod 22**: Sdílení a organizace (poslední bod roadmapy)

Nad rámec roadmapy: 3 přepínatelné vzhledy + polymorfní widgety
(Solton-inspired Vintage), audit a opravy, šumová brána, upozornění
na novou verzi, záloha dat, Vercel deploy, rozšířený metronom,
responzivní PC rozvržení, Stage 3 lekce. **Šestifázový upgrade
metronomu** (2026-05-27): per-beat zvuk, swing, count-in, polyrytmus,
vizuální kyvadlo, tovární presety. **Čtyřfázový upgrade Ladičky**
(2026-06-02): referenční tón na strunu, konfirmace naladění (zvuk
+ haptika), 3 nová tovární ladění (Open G / Open D / DADGAD),
vlastní uživatelská ladění s inline editorem. **Balík oprav z reálné
zpětné vazby** (2026-06-02): nahrávání do obou repráčků, aparát hrající
přes obrazovky (sdílený engine + SW fix), spolehlivý export, audit
validace vstupů/výstupů s opravami, zviditelněné nahrávání v Aparátu.
**Komplexní audit + 28krokový FIX_PLAN** (2026-06-03): Live mód
implementace, sdílený `LoopRunner` audio helper (zavřel 3 rAF leaky
+ duplicity), `src/hooks/`, kompletní backup pokrytí 11 stores,
generation guard v `AudioEngine`, ESLint flat config, lifecycle testy
audio singletonů, theme-token cleanup, a11y (tap targety 48 px,
focus-visible ring, `prefers-reduced-motion`, `aria-live`). 65 nových
testů (496 → 561).

Detaily v `ROADMAP.md`, `PROJECT_STATUS.md`, `AUDIT.md` a `FIX_PLAN.md`.

## Nasazení

Vercel projekt je propojený s GitHub repem. **Push do `main`
= automatický deploy** (~1 min). Aplikace je statický PWA export —
žádný backend ani proxy nejsou potřeba.

## Jak Rocker vznikl

Vibe coding s Claudem. Programovat neumím, ale mám nápady — Claude píše
kód, já směruji, schvaluji, testuji. Pravidla naší spolupráce jsou
v `SKILL.md`, projektová pravidla v `AGENTS.md`.

## Licence

MIT — viz `LICENSE`.
