# Rocker

> PWA kytarový asistent pro učení a hraní na elektrickou kytaru.
> Jeden zdroj, mobil i desktop, žádný server.

**Živá verze:** https://rocker-rust.vercel.app

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

- **Domů** — denní plán cvičení (~15 min), streaky, 11 achievementů
- **Ladička** — Pitchy detekce, vedený i chromatický režim, 6 ladění,
  šumová brána s autokalibrací
- **Metronom** — sample-accurate scheduler, 7 taktových měr, presety,
  trenažér tempa s progresivním zrychlováním
- **Cvičení** — Riff Trainer + YouTube Practice Player (A/B smyčka,
  slow-down) + Surgical Mode (drill problémového místa)
- **AI tutor** — chat „zeptej se", generátor cvičení, zpětná vazba —
  Google Gemini, BYOK, přímo z prohlížeče
- **Hry** — hmatníková hra a ear training
- **Metal** — Power Chord, Gallop a Palm Mute trenažéry
- **Aparát** — efektový řetězec (komprese, zkreslení, EQ, delay,
  dozvuk, chorus), presety + vlastní. Mini `AmpWidget` (ON/OFF
  + 4 vestavěné presety) sedí i v Cvičení a Metalu, sdílí nastavení.
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
- **Testy**: Vitest (395 testů, čistá logika v `src/lib/`)
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
npm run build      # next build + razítkování service workeru
```

## Architektura

Sedm vrstev — vrstva zná jen tu pod sebou. Čistá logika v `src/lib/` je
TypeScript bez Reactu a bez audia (aby šla testovat). Detaily a důvody
najdeš v ostatních dokumentech repa:

- `ARCHITECTURE.md` — vrstvy, audio graf, perzistence
- `AGENTS.md` — pravidla projektu, konvence, lessons learned
- `SKILL.md` — pravidla spolupráce s Claudem (vibecoding)
- `ROADMAP.md` — číslované body roadmapy
- `PROJECT_STATUS.md` — aktuální stav, klíčová rozhodnutí, mapa kódu

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
responzivní PC rozvržení, Stage 3 lekce.

Detaily v `ROADMAP.md` a `PROJECT_STATUS.md`.

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
