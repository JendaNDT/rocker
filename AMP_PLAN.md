# Plán rozšíření Aparátu — Rocker

Implementační plán pro rozšíření obrazovky Aparát (`/amp`, Bod 16). Vznikl
2026-05-25 z přání reálného uživatele — „více různorodých efektů a simulací".

## Jak plán číst

- Platí vibecoder workflow (`AGENTS.md`, `SKILL.md`): diskuze před kódem,
  krok po kroku, čekat na „piš". Tenhle plán je podklad — jednotlivé kroky
  se před psaním ještě domluví.
- Stavět po fázích A → D. V rámci fáze mají kroky pořadí.
- Po každém kroku: `npx tsc --noEmit` + `npx vitest run`, report výsledku.
- Po dokončení fáze: aktualizovat `PROJECT_STATUS.md` a připravit Jendovi
  příkazy na commit + push.
- **Zvuk se ověřuje jen sluchem.** Aparát byl postavený naslepo; mono chyba
  byla jeho první reálný test. Každý nový efekt proto přidáváme po jednom
  a Jenda (nebo uživatel) ho otestuje uchem, než přidáme další. Build se
  v sandboxu neověřuje (EPERM quirk — viz `AGENTS.md`).

## Kontext — co Aparát má dnes

Pevný řetězec (`src/audio/ampChain.ts`): vstup → pre-gain (drive) →
WaveShaper zkreslení → 3pásmový EQ → simulace reproboxu (filtry) → výstup,
plus zpětnovazební delay. 4 pevné presety (Čistý / Crunch / Lead / Metal)
v `src/lib/ampPresets.ts`. Vše ve Web Audio, bez externích souborů a bez
licencí — to rozšíření drží.

Vstup je od opravy z 2026-05-25 sloučený do mono (`AudioEngine`), takže
nové efekty pracují s čistým mono signálem.

---

## Fáze A — Vlastní presety ✅ HOTOVO (2026-05-25)

Dnes jsou presety 4 pevné. Cíl: uživatel si uloží vlastní pojmenované
nastavení aparátu. Nízké riziko — data a UI, žádné nové DSP.

### A1 — Persistovaný store vlastních presetů ✅
Zustand persist store (localStorage, jako ostatní) — uložit / přejmenovat /
smazat pojmenovaný preset držící `AmpSettings`. Type guard při načtení
(konvence projektu — poškozený záznam se zahodí). Čistá logika + testy.

**Hotovo (2026-05-25):** nový `src/stores/ampPresetsStore.ts`; čisté guardy
`sanitizeAmpSettings` (dopředně kompatibilní — chybějící parametr dostane
výchozí hodnotu) a `sanitizeUserPresets` v `ampPresets.ts` + testy.

### A2 — UI v Aparátu ✅
„Uložit jako…", seznam vlastních presetů vedle 4 vestavěných, mazání
a přejmenování. Vestavěné presety zůstávají.

**Hotovo (2026-05-25):** v `AmpStudio.tsx` pole na název + „Uložit jako
preset", vlastní presety jako štítky vedle vestavěných, mazání křížkem.
Přejmenování store umí, ale UI pro něj zatím není — vědomě odloženo
(inline editace by A2 zbytečně rozbujela), lze doplnit později.

**Pozn.:** preset uložený dnes nebude mít parametry efektů přidaných ve
fázi B — `sanitizeAmpSettings` jim doplní rozumné výchozí hodnoty (stejný
princip jako u ostatních uložených dat).

---

## Fáze B — Nové efekty ✅ HOTOVO (2026-05-25)

Jádro plánu a uživatelovo přání. Každý efekt = jeden krok, postavit
a otestovat sluchem, pak teprve další. Pořadí od nejsnazšího po náročnější.

Vše hotové v kódu, sluchem NEOVĚŘENO — doladí se podle odezvy uživatele.
Místo zvláštního přepínače u/vypnuto má každý efekt posuvník, kde 0 =
vypnuto (vyhne se přepojování uzlů za běhu).

### B1 — Kompresor ✅
`DynamicsCompressorNode` — hotový uzel Web Audio. Srovná dynamiku, sluší
čistému zvuku a funku. Triviální uzel, velký přínos.

**Hotovo (2026-05-25):** `DynamicsCompressorNode` na začátku řetězce
(pozice kompresorového pedálu), posuvník „Komprese" 0–100 %. Na 0 % je
průhledný (poměr 1:1) — místo zvláštního přepínače, vyhne se přepojování
uzlů za běhu. 4 vestavěné presety mají `compression: 0`.

### B2 — Víc typů zkreslení a reproboxů ✅
Overdrive / distortion / fuzz jako varianty křivky `WaveShaper` + volič
typu. Víc reproboxů = jiná nastavení filtrů simulace (jiný „cab").
Využívá uzly, které už v řetězci jsou — žádný nový typ uzlu.

**Hotovo (2026-05-25):** B2a — tři křivky zkreslení (overdrive / distortion
/ fuzz), `setDistortionType`, volič v UI. B2b — tři reproboxy (Moderní /
Vintage / Otevřený), `setCabinet` přenastaví filtry, volič v UI. Enumy
`distortion` a `cabinet` v `AmpSettings`, sanitizace je ověřuje. Vestavěné
presety na „distortion" + „modern" — zní jako dosud.

### B3 — Reverb ✅
`ConvolverNode` se syntetizovanou impulzní odezvou (generovaný doznívající
šum — žádný externí IR soubor, drží rozhodnutí Bodu 16). Velký efekt na
vjem prostoru.

**Hotovo (2026-05-25):** `ConvolverNode` s generovanou IR (~2,2 s
doznívajícího stereo šumu), paralelní send vedle delaye, posuvník „Dozvuk"
(0 = vypnuto). `AmpSettings.reverb`, sanitizace.

### B4 — Modulace (chorus) ✅
LFO modulovaný delay = chorus. Rozechvěje a oživí zvuk.

**Hotovo (2026-05-25):** krátký delay (~20 ms) modulovaný LFO (sinus
0,8 Hz), paralelní send, posuvník „Chorus" (0 = vypnuto).
`AmpSettings.chorus`, sanitizace. Flanger/phaser jako varianty vědomě
vynechány — chorus jako univerzální modulace stačí, lze doplnit později.

---

## Fáze C — UX a vizualizace ✅ HOTOVO (2026-05-25)

Vylepšení ovládání, žádné nové DSP — vizualizace jen čte signál.

### C1 — Indikátory úrovně ✅
Ukazatel vstupní a výstupní úrovně signálu — uživatel vidí, že kytara
„jde dovnitř" a že výstup nepřebuzuje.

**Hotovo (2026-05-25):** `AmpChain.outputAnalyser` (čtecí odbočka výstupu);
obecná komponenta `SignalLevelMeter`; v Aparátu vstupní (`InputLevelMeter`)
i výstupní ukazatel, oba červenají při přebuzení.

### C2 — Vizualizér zvuku ✅
Osciloskop nebo spektrum z `AnalyserNode`. Čistě zobrazení, read-only.

**Hotovo (2026-05-25):** komponenta `SignalScope` — osciloskop na `<canvas>`
čtoucí výstupní analyzér, barvy z theme proměnných.

### C3 — A/B porovnání presetů ✅
Rychlé přepnutí mezi dvěma nastaveními, ať uživatel slyší rozdíl.

**Hotovo (2026-05-25):** sekce „Porovnání A/B" — dvě paměti nastavení,
uložit/načíst. Čistě stav a UI v `AmpStudio`.

---

## Fáze D — Přeskládatelný pedalboard (otevřená otázka)

Plný pedalboard — efekty jako pedály, které jdou přetahovat a měnit jejich
pořadí. **Velký refaktor** řetězce (přestavba zapojení uzlů za běhu).

**Doporučení:** s přepínači zapnuto/vypnuto z fáze B už uživatel dostane
většinu hodnoty. Plné přeskládání pořadí je drahá menšina — a kanonické
pořadí kytarového řetězce (drive → EQ → cab → modulace → delay → reverb)
je pro appku pro začátečníky rozumné napevno. Fázi D zvážit až tady, podle
toho, jestli reálně chybí. Klidně vynechat.

---

## Pořadí, velikosti, riziko

| Fáze | Krok | Velikost | Riziko |
|---|---|---|---|
| A | A1 — store presetů | S | ✅ hotovo |
| A | A2 — UI presetů | S | ✅ hotovo |
| B | B1 — kompresor | S | ✅ hotovo (test sluchem čeká) |
| B | B2 — typy zkreslení a reproboxů | M | ✅ hotovo (test sluchem čeká) |
| B | B3 — reverb | M | ✅ hotovo (test sluchem čeká) |
| B | B4 — modulace (chorus) | M | ✅ hotovo (test sluchem čeká) |
| C | C1 — indikátory úrovně | S | ✅ hotovo |
| C | C2 — vizualizér | S–M | ✅ hotovo |
| C | C3 — A/B porovnání | S | ✅ hotovo |
| D | pedalboard | L | vysoké — zvážit, zda vůbec |

Návrh: začít fází A (rychlé rozehřátí, nízké riziko), pak fáze B (jádro,
po efektech), pak C, fázi D nechat jako otevřenou otázku.

## Otevřené otázky

1. ~~**Pořadí fází** — fáze A (presety) jako rozehřátí, nebo rovnou fáze B?~~
   Vyřešeno (2026-05-25): A první, hotová. Na řadě fáze B.
2. **Fáze D** — dělat plný přeskládatelný pedalboard, nebo stačí přepínače
   zapnuto/vypnuto z fáze B? Návrh: zatím jen přepínače, plný pedalboard
   vynechat / zvážit později.
3. **Rozsah efektů** — všechny čtyři kroky fáze B, nebo podmnožina? Návrh:
   všechny, ale po jednom a s testem mezi nimi.
