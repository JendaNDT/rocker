# Plán provázání funkcí — Rocker

Implementační plán pro chytřejší propojení existujících funkcí. Vznikl
2026-05-25 z auditu provázanosti celé appky.

## Jak plán číst

- Platí **vibecoder workflow** (viz `AGENTS.md` a `SKILL.md`): diskuze před
  kódem, krok po kroku, čekat na „piš". Tenhle plán je podklad — jednotlivé
  kroky se před psaním ještě domluví.
- Stavět **shora dolů po fázích** (A → D). Fáze jsou nezávislé; v rámci fáze
  mají kroky pořadí.
- Po každém kroku: `npx tsc --noEmit` + `npx vitest run`, report výsledku.
- Po dokončení fáze: aktualizovat `PROJECT_STATUS.md`.
- Build se v sandboxu neověřuje (EPERM quirk) — viz `AGENTS.md`.

## Kontext — co audit zjistil

Nic není rozbité. Co je propojené, je propojené správně. Tohle nejsou opravy
bugů, ale **chytřejší propojení funkcí, které dnes fungují izolovaně**.
Seřazeno od nejlevnějšího (fáze A) po největší refaktor (fáze D).

---

## Fáze A — Snadné výhry ✅ HOTOVO (2026-05-25)

### A1 — Hry a Aparát se počítají do odcvičených minut

**Co:** Obrazovky `/games` a `/amp` jsou reálné cvičení, ale nepřispívají do
statistik — chybí v seznamu cvičebních rout.

**Soubory:** `src/lib/practiceTime.ts` (pole `PRACTICE_ROUTES`),
`src/lib/practiceTime.test.ts`.

**Kroky:** Jeden krok — přidat `/games` a `/amp` do `PRACTICE_ROUTES`,
upravit/doplnit testy.

**K rozhodnutí s Jendou:** `/notation` a `/recorder` zůstávají vynechané
(prohlížení not / přehrávání nahrávek není aktivní cvičení) — potvrdit.

**Riziko:** triviální.

**✅ Hotovo (2026-05-25):** `/games` a `/amp` přidány do `PRACTICE_ROUTES`
v `practiceTime.ts`, testy doplněny. Jenda potvrdil, že `/notation`
a `/recorder` zůstávají mimo.

### A2 — AI chat dostane kontext o cvičení

**Co:** Záložky Plán a Zpětná vazba posílají AI data o pokroku; chat „zeptej
se" neposílá nic. Chat tak nemůže zohlednit reálný stav uživatele.

**Soubory:** nový `src/lib/ai/context.ts` (+ test), `src/components/tutor/
ChatPanel.tsx`, případně `src/lib/ai/prompts.ts`. Inspirace v `feedback.ts`
(funkce `summarizePractice`).

**Kroky:**
1. Čistá funkce `buildPracticeContext()` — z dat pokroku (odcvičené minuty,
   streak, hotové lekce, achievementy) složí krátký textový kontext. S testy.
2. Zapojit kontext do `ChatPanel` — připojit k systémovému promptu chatu.

**Riziko:** nízké. Datová instalatérská práce už existuje pro Plán/Zpětnou
vazbu.

**✅ Hotovo (2026-05-25):** nový `src/lib/ai/context.ts`
(`buildPracticeContext()`) + testy; `ChatPanel` připojuje kontext
k systémovému promptu chatu (`TUTOR_SYSTEM_PROMPT` + kontext).

---

## Fáze B — Propojení profilu Kytary ✅ HOTOVO (2026-05-25)

### B1 — Ladění v profilu Kytary strukturovaně + napojení na Ladičku

**Co:** Profil nástroje (`/instrument`) drží ladění jako **volný text** a nikdo
mimo tu obrazovku ho nečte. Ladička má ladění strukturovaně
(`audio/tunings.ts` + `settingsStore.tuningId`). Ladění tak nastavuješ na dvou
nezávislých místech.

**⚠️ Nejdřív rozhodnout datový model — probrat s Jendou:**
Kde má „ladění" žít jako jediný zdroj pravdy? Dvě varianty:
- (a) Profil Kytary se stane domovem strukturovaného ladění; Ladička čte
  z profilu; `settingsStore.tuningId` se zruší/zmigruje.
- (b) `settingsStore.tuningId` zůstane zdrojem pravdy; profil Kytary ho jen
  zrcadlí / odkazuje.
Doporučení: varianta (a) — ladění logicky patří k nástroji. Ale rozhodne Jenda.

**Soubory:** `src/lib/instrument.ts` (typ `InstrumentProfile`),
`src/stores/instrumentStore.ts` (type guard + migrace), `src/audio/tunings.ts`,
`src/app/tuner/page.tsx`, `src/components/instrument/ProfileCard.tsx`.

**Kroky:**
1. Domluvit datový model (viz výše).
2. `InstrumentProfile` nese strukturované ladění (id z `TUNINGS`).
   **Migrace:** stávající uživatelé mají uložený volný text — type guard
   v `instrumentStore` musí starý formát bezpečně převést, ne shodit appku.
3. `ProfileCard` — výběr ladění ze seznamu místo textového pole.
4. Ladička čte ladění z profilu; vyřešit osud `settingsStore.tuningId`.

**Riziko:** střední — datový model + migrace localStorage. Citlivé na to,
aby se uživatelům neztratilo nastavení.

**✅ Hotovo (2026-05-25):** Jenda zvolil variantu (b) — zdrojem pravdy
zůstává `settingsStore.tuningId`. Z `InstrumentProfile` odebráno volně-textové
pole `tuning` (typ, `DEFAULT_PROFILE`, `sanitizeProfile`); starý klíč se tiše
ignoruje, žádná migrace. `ProfileCard` edituje ladění výběrem ze seznamu
`TUNINGS` napojeným na `settingsStore`.

### B2 — AI tutor čte profil nástroje

**Co:** Tutor radí ohledně zvuku a cvičení, ale profil Kytary (snímače,
ladění, struny, tělo) mu nikdy nepředáme. S ním by rady byly konkrétní pro
uživatelovu kytaru.

**Soubory:** `src/lib/ai/context.ts` (rozšíření z A2), `src/lib/ai/
feedback.ts`, `planGenerator.ts`, `prompts.ts`, panely
`ChatPanel`/`FeedbackPanel`/`PlanPanel`.

**Kroky:**
1. Rozšířit kontext z A2 o data profilu nástroje.
2. Zapojit do všech tří AI režimů (chat, plán, zpětná vazba).

**Závislosti:** ideálně po A2 (sdílí instalatérskou práci kontextu) a po B1
(strukturované ladění se předává čistěji). Není striktně blokované.

**Riziko:** nízké až střední.

**✅ Hotovo (2026-05-25):** `context.ts` rozšířen o `buildInstrumentContext()`
+ testy; kontext kytary připojen k systémovému promptu chatu i zpětné vazby
a (přes nepovinné `instrumentSummary`) k dotazu generátoru plánu.

---

## Fáze C — Ladění napříč appkou ✅ HOTOVO (2026-05-25)

### C1 — Knihovna respektuje zvolené ladění

**Co:** Hmatník a stupnice v Knihovně počítají tóny napevno ve standardním
ladění. V Drop D appka pořád ukazuje standard.

**Soubory:** `src/components/library/Fretboard.tsx`, `ScaleExplorer.tsx`,
`src/app/library/page.tsx`, `src/audio/tunings.ts` (poskytuje MIDI strun).

**Kroky:**
1. Knihovna čte aktivní ladění (zdroj z fáze B1) a předává tóny prázdných
   strun do `Fretboard`/`ScaleExplorer`.
2. `Fretboard` přepočítá tóny podle zadaných strun.

**Závislosti:** po B1 (zdroj ladění je tam usazený).

**Riziko:** střední — práce s výškami tónů, hlídat přepočet.

**✅ Hotovo (2026-05-25):** `Fretboard` dostal prop `tuning` a čte struny
z něj; `ScaleExplorer` ho prožene, `library/page.tsx` předá aktivní ladění
s hydratační pojistkou. Akordové diagramy zůstávají standardní (jiné ladění
= jiný akord).

### C2 — Metalové trenažéry respektují ladění

**Co:** Power Chord a Gallop trainer počítají pozice napevno
(`powerChords.ts`), ladění ignorují.

**Soubory:** `src/lib/powerChords.ts`, `src/components/metal/
PowerChordTrainer.tsx`, `GallopTrainer.tsx`.

**Kroky:** Zpřístupnit trenažérům aktivní ladění a přepočítat z něj kořenové
tóny power chordů / názvy tónů.

**Riziko:** střední. **Nižší priorita** — power chord je tvar, ladění mění
hlavně zobrazené názvy tónů. Klidně až nakonec, nebo vynechat.

**✅ Hotovo (2026-05-25):** Jenda zvolil C2 udělat. `powerChordAt`
a `getPowerChord` dostaly nepovinný `openMidi`; oba trenažéry předají
aktivní ladění, Power Chord trainer bere i popisky strun mini-hmatníku
z ladění. Trenažéry jsou metronomové (ne detektorové), takže šlo čistě
o správné názvy tónů — žádná detekční logika.

---

## Fáze D — Sjednocení časovacích enginů (největší, zvážit)

### D1 — Sdílený scheduler

**Co:** `metronome.ts`, `gallopPlayer.ts`, `riffPlayer.ts` a `countIn.ts`
mají každý vlastní lookahead scheduler s oscilátorovým klikáním — dělají
v podstatě totéž.

**Soubory:** nový `src/audio/scheduler.ts`; postupný refaktor `metronome.ts`,
`gallopPlayer.ts`, `riffPlayer.ts`, `countIn.ts`.

**Kroky:**
1. Vytáhnout sdílené scheduler jádro (lookahead, dvojí hodiny).
2.–5. Přepojit každý engine zvlášť, jeden krok = jeden engine, po každém
   ruční test zvuku.

**⚠️ Riziko: vysoké.** Audio se ověřuje jen poslechem (ne automaticky). Riff
trainer a metalové trenažéry už fungují (částečně neověřené) — refaktor jejich
zvuku riskuje regresi v něčem těžko testovatelném.

**Doporučení:** **fáze D je nejvíc sporná návratnost.** Je to vnitřní úklid,
ne funkce pro uživatele. Zvážit, jestli vůbec — udělat jen, pokud ta
duplicita začne reálně překážet při údržbě. Klidně odložit nebo vynechat.

---

## Doporučené pořadí a velikosti

| Fáze | Bod | Velikost | Riziko | Pozn. |
|---|---|---|---|---|
| A | A1 — practice time | XS | triviální | ✅ hotovo |
| A | A2 — chat kontext | S | nízké | ✅ hotovo |
| B | B1 — ladění profilu | M | střední | ✅ hotovo (varianta b) |
| B | B2 — AI čte profil | S–M | nízké–stř. | ✅ hotovo |
| C | C1 — knihovna a ladění | M | střední | ✅ hotovo |
| C | C2 — trenažéry a ladění | M | střední | ✅ hotovo |
| D | D1 — sdílený scheduler | L | vysoké | zvážit, zda vůbec |

Fáze A, B i C hotové (2026-05-25). Zbývá jen fáze D — otevřená otázka
(viz níže), sporná návratnost.

## Otevřené otázky k probrání s Jendou

1. ~~**B1 — kde žije „ladění"** jako jediný zdroj pravdy (varianta a/b).~~
   Vyřešeno (2026-05-25): varianta (b) — `settingsStore.tuningId`.
2. ~~**A1 —** počítat `/notation` a `/recorder` do odcvičených minut?~~
   Vyřešeno (2026-05-25): ne, zůstávají mimo.
3. **Fáze D —** dělat ji vůbec? Vysoké riziko, žádný přínos pro uživatele.
