# AGENTS.md — pravidla projektu Rocker

Tento soubor je „smlouva" pro každou session. Přečti ho dřív, než cokoliv změníš.

## Spolupráce s uživatelem (vibecoder)

Jenda je vibecoder — má skvělé nápady, ale neprogramuje. Plná pravidla jsou
v `SKILL.md`. Stručně:

- **Diskuze před kódem.** Nikdy nepiš kód bez popsaného plánu a explicitního
  schválení. Plán řekne: co uděláš, které soubory, výsledek, riziko. Pak čekej.
- **Schválení musí být jasné** — `piš`, `ok`, `pokračuj`, `jdi na to`. Milá
  reakce ("zní to dobře") schválení není.
- **Krok po kroku, ne dávkově.** Velký úkol rozsekej. Po každém kroku shrň,
  spusť kontrolu typů a testy, počkej na další `piš`.
- **Ověřuj fakta.** Konkrétní fakta (verze knihoven, API, čísla) musí mít
  zdroj. Když si nejsi jistý, řekni to.
- **Komunikace česky**, oslovení „Jendo", tykání.

## Stack

- Next.js (App Router), statický export (`output: 'export'`)
- TypeScript
- Tailwind CSS v4 — 3 přepínatelné vzhledy přes CSS proměnné
  a `[data-theme]`/`[data-mode]` na `<html>`
- Zustand (stav)
- Perzistence: Zustand persist → localStorage (malá data); IndexedDB
  (přes `idb`) pro velké bloby — nahrávky, v provozu od Bodu 11
  (`src/lib/recordingsDb.ts`)
- Web Audio API + getUserMedia (audio jádro; detekce tónů na hlavním vlákně)
- Vitest (testy čisté logiky)
- Vlastní service worker (`public/sw.js`, ručně psaný) — PWA; verzuje se
  při buildu skriptem `scripts/stamp-sw.mjs`
- Nasazení: Vercel, automatický deploy z větve `main`
  (živě na `rocker-rust.vercel.app`)

## Konvence

- Kód, názvy proměnných/funkcí/souborů, komentáře a commit messages **anglicky**.
- UI texty, hlášky a popisky **česky**.
- Sedm vrstev architektury (viz `ARCHITECTURE.md`, sekce 2) — vrstva zná jen
  vrstvu pod sebou, nikdy ne nad sebou.
- Čistá logika a data (`src/lib/`) je TypeScript **bez Reactu a bez
  audia** — aby šla testovat. Testy `*.test.ts` leží vedle ní.
- Sdílené React hooky žijí v `src/hooks/` (`useHydrated`,
  `useActiveTheme`, `useResolvedLayoutMode`, `useWakeLock`). Pravidlo:
  pokud potřebuje `useState`/`useEffect`/store read, patří sem; pokud
  je to čistá funkce nad daty, patří do `lib/`. (Historické soubory
  `src/lib/useHydrated.ts`, `useActiveTheme.ts`, `layout.ts` a
  `src/components/useWakeLock.ts` zůstávají jako thin re-exporty — k
  smazání, viz FIX_PLAN krok 20.)
- Data z úložiště vždy projdou type guardem; poškozený záznam se zahodí, appka
  nespadne.
- UI používá theme-aware Tailwind třídy (`bg-surface`, `text-accent`,
  `border-line`, `text-dim`…), **ne** `bg-neutral-*` ani pevné barvy —
  appka má 3 vzhledy a komponenta nesmí vědět, který je aktivní.
- Žádné emoji v kódu ani v UI textech.

## Testovací disciplína

Po každé netriviální změně:
1. `npx tsc --noEmit` — kontrola typů.
2. Testy doménové logiky (Vitest).
3. Report výsledku: „X / Y prošlo".

Audio a UI se testují ručně.

## Roadmapa

Stavíme shora dolů podle `ROADMAP.md`. Aktuální bod = nejnižší nedokončené
číslo, pokud Jenda neřekne jinak. Po dokončení bodu: označit ✅ v ROADMAP.md,
aktualizovat `PROJECT_STATUS.md`, zeptat se na další bod.

## Lessons learned

*(Sem se zapisují nečekané věci, na které příští session nemá znovu naběhnout —
quirky sandboxu, záludnosti knihoven, proč bylo něco rozhodnuto takhle.)*

- **Sandbox neumí mazat soubory v projektové složce.** `rm` ve složce Rocker
  selže s „Operation not permitted". Soubory jde vytvářet a přepisovat, ne
  mazat. Důsledek: nepoužívat nástroje, které potřebují mazání, a mazání nechat
  na Jendovi.
- **`create-next-app` nelze spustit přímo v Rocker** — složka není prázdná
  (jsou v ní `.md` dokumenty a PDF rešerše). Projekt proto scaffoldujeme ručně
  psaním jednotlivých souborů (i lépe odpovídá vibecoding stylu — malé,
  prohlédnutelné kroky).
- Sandbox má Node v22 a npm v10.
- **Vitest potřebuje vlastní alias `@/`** — je v `vitest.config.ts`
  (`resolve.alias`). Bez něj test soubory, které přes `@/` něco importují,
  spadnou na nenalezeném modulu.
- **`npm run build` v sandboxu selže na konci** chybou `EPERM rmdir` —
  Next.js při finalizaci exportu maže/přesouvá složky a sandbox to nepovolí.
  Není to chyba kódu: kompilace, typová kontrola i generování stránek
  proběhnou; build na reálném stroji (Mac) doběhne. V sandboxu se ověřuje přes
  `npx tsc --noEmit`, ne plným buildem.
- **node_modules nainstalované v sandboxu obsahují Linux binárky.** Jenda je na
  Macu — musí si `npm install` spustit u sebe, jinak nativní binárky (Next SWC,
  Tailwind oxide) nesednou. Sandbox install slouží jen k ověřování.
- **Next.js 16 používá Turbopack jako výchozí builder.** `@serwist/next`
  (plugin mode) ho nepodporuje a service worker se nevygeneruje. Proto se
  Serwist nepoužívá — service worker je ručně psaný `public/sw.js`.
- **Trik „smazat `.next`/`out` a buildnout znovu" nefunguje spolehlivě.**
  Jakmile build v sandboxu jednou proběhl, jeho soubory v `.next`/`out` už
  nejdou smazat (`rm -rf` selže s „Operation not permitted") a další build
  pak padne. V sandboxu se proto **plný build neověřuje** — jen
  `npx tsc --noEmit` a Vitest. Build na Macu doběhne.
- **Po `npm install <balíček>` může v sandboxu zmizet nativní binárka**
  (lightningcss, Tailwind oxide) a build pak spadne na `Cannot find module
  '...node'`. Oprava: spustit znovu prosté `npm install` — chybějící optional
  binárky se doplní. Není to chyba kódu.
- **`@types/youtube` 0.2.0 je `export =` modul, ne globální namespace** — s
  modulovými soubory ho TypeScript nenajde (`Cannot find namespace 'YT'`).
  Řešení: vlastní stručná definice v `src/youtube-iframe.d.ts`; balíček
  `@types/youtube` se nepoužívá.
- **Float hodnoty z `Float32Array` porovnávej v testech `toBeCloseTo`,**
  ne `toEqual` — single-precision zaokrouhlení rozbije přesnou rovnost.
- **Detekce tónů běží na hlavním vlákně** (`AnalyserNode` + Pitchy v rAF
  smyčce), ne v AudioWorkletu — architektura původně počítala s workletem,
  ale main-thread je pro nástroje spolehlivý a obchází balení s Turbopackem.
- **Service worker se musí při každém nasazení lišit.** Když je `sw.js`
  bajt po bajtu stejný, prohlížeč novou verzi nepozná a upozornění
  „nová verze" se nikdy neukáže. Proto `scripts/stamp-sw.mjs` po buildu
  vepíše do `out/sw.js` unikátní verzi — volá se z `npm run build`.
- **Testy IndexedDB potřebují `fake-indexeddb`.** Vitest běží v Node bez
  `indexedDB`; testy `recordingsDb` importují `fake-indexeddb/auto`, který
  ten globál doplní. Je to vývojová závislost (`devDependencies`).
- **Gemini API jde volat přímo z prohlížeče.** Nativní endpoint
  `generativelanguage.googleapis.com/.../generateContent` posílá CORS
  hlavičky (ověřeno OPTIONS preflightem), takže AI tutor (Bod 14) volá
  Gemini napřímo, bez proxy a bez serveru. POZOR na záměnu: CORS chyby,
  které lidé na webu hlásí, se týkají *OpenAI-kompatibilního* endpointu
  Gemini (jiná adresa) — ten z prohlížeče nejde; nativní `generateContent`
  ano. Klíč se posílá v hlavičce `x-goog-api-key`. AI vrstva je
  v `src/lib/ai/` (čistý fetch, žádná SDK závislost navíc).
- **`output: 'export'` NEdělá z navigace plný reload — soft navigace
  funguje (oprava dřívějšího lessonu, 2026-06-02).** Dřívější verze tvrdila
  opak; bylo to nepřesné. Next.js i ve statickém exportu přepíná obrazovky
  **client-side** přes `<Link>` (data routy jako `.txt` RSC payloady), takže
  `AppShell` i modul-level singletony navigaci přežijí — proto **může
  „audio žít přes obrazovky"**: Aparát běží v singletonu `AmpEngine`
  (`src/audio/ampEngine.ts`) hostovaném v `AppShellu` přes `AmpHost`, ne
  v komponentě obrazovky. K plnému reloadu dochází jen výjimečně, typicky
  první navigace **po nasazení nové verze** kvůli selhání fetche RSC
  payloadu; zhoršoval to service worker, který RSC `.txt` servíroval
  cache-first — oprava je servíruje network-first (`public/sw.js`). Přechod
  na server-rendered Next.js tedy NENÍ potřeba, appka zůstává statická PWA.
- **Synchronní `URL.revokeObjectURL` hned po `link.click()` může zrušit
  stažení** většího blobu dřív, než vůbec začne — projevilo se to jako
  „nemohu exportovat nahrávku". Spolehlivé stahování je v
  `src/lib/download.ts` (`downloadBlob`): připojí `<a>` do DOM (jinak
  Firefox stažení ignoruje) a URL uvolní až po prodlevě. Používat ten
  helper, ne ad-hoc `<a download>`.
- **Nahrávací cesty berou mono `getRecordingStream()`, ne syrový
  `getInputStream()`.** Dvouvstupová zvukovka má kytaru jen v jednom
  kanálu; syrový stream nahraje jen jeden repráček. `getRecordingStream()`
  (v `AudioEngine`) vrací mono-sečtený stream. Aparát má vlastní
  zaaparátovaný stream (`AmpChain.recordingStream`).
