# W01 — inventura funkcí a přejímacích scénářů

Zdroj pravdy pro tuto čitelnou podobu: [inventory.json](inventory.json). [Souhrn a ověření W01](README.md).

Stav u všech pohledů: chování ověřené čtením zdrojů a uvedenými existujícími testy. Přejímací scénáře popisují, co bude nutné ověřit při přepisu; nejsou tvrzením o již provedené ruční nebo hardwarové kontrole.

## 01 — Přehled

Současná sekce: `/`. Nativní kroky: W32, W33.

Denní plán o 17 minutách, další nedokončená lekce, rotace hlavního cvičení, statistiky, série a odměny; Zen skrývá gamifikaci.

**Data:** rocker-progress,rocker-lesson-progress,rocker-settings.

**Přejímací scénář:** Z profilu se dvěma dokončenými lekcemi nabídnout správnou následující lekci; ověřit součet minut a režim Zen.

**Rozdíl nebo omezení:** Odkaz plánu dnes vede pouze na obecnou obrazovku; přímé otevření konkrétní úlohy je plánovaná oprava.

**Zdroje:** [Dashboard.tsx](../../../src/components/dashboard/Dashboard.tsx), [dailyPlan.ts](../../../src/lib/dailyPlan.ts), [achievements.ts](../../../src/lib/achievements.ts).

**Existující automatické kontroly:** [dailyPlan.test.ts](../../../src/lib/dailyPlan.test.ts), [achievements.test.ts](../../../src/lib/achievements.test.ts).

## 02 — Live

Současná sekce: `/live`. Nativní kroky: W33.

Velká ladička, metronom, nahrávání a wake lock při aktivním používání; sdílený aparát.

**Data:** rocker-settings,rocker-metronome,rocker-amp; transport a záznam v paměti.

**Přejímací scénář:** Změnit tempo, spustit/zastavit záznam, odejít a vrátit se bez zdvojení audio hostu; ověřit platný uložený take.

**Rozdíl nebo omezení:** Live se dnes nezapočítává do practiceTime; skutečný hardware ani odezva nejsou ověřené tímto baseline.

**Zdroje:** [page.tsx](../../../src/app/live/page.tsx), [useWakeLock.ts](../../../src/hooks/useWakeLock.ts), [ampEngine.ts](../../../src/audio/ampEngine.ts).

**Existující automatické kontroly:** [ampEngine.test.ts](../../../src/audio/ampEngine.test.ts).

## 03 — Ladička

Současná sekce: `/tuner`. Nativní kroky: W19.

Vedený/chromatický režim, 9 továrních a nejvýše 20 vlastních šestistrunných ladění, centy, confidence a šumová brána s kalibrací; referenční tón struny, jeho hlasitost a volitelná zvuková/haptická konfirmace; značení H/B.

**Data:** rocker-settings,rocker-custom-tunings.

**Přejímací scénář:** Přehrát syntetické E2 a A4, ověřit tóny a ticho; obnovit vlastní ladění a H/B; referenční tón zastavit při uvolnění tlačítka.

**Rozdíl nebo omezení:** Frekvenční výpočty mají pevnou referenci A4=440 Hz; nastavitelná reference by byla nová funkce. Detekce běží na hlavním vlákně, ne v AudioWorkletu.

**Zdroje:** [GuidedTuner.tsx](../../../src/components/tuner/GuidedTuner.tsx), [TunerSettings.tsx](../../../src/components/tuner/TunerSettings.tsx), [pitchDetector.ts](../../../src/audio/pitchDetector.ts), [tunings.ts](../../../src/audio/tunings.ts), [CustomTuningEditor.tsx](../../../src/components/tuner/CustomTuningEditor.tsx).

**Existující automatické kontroly:** [pitchDetector.test.ts](../../../src/audio/pitchDetector.test.ts), [tunings.test.ts](../../../src/audio/tunings.test.ts), [referenceTone.test.ts](../../../src/audio/referenceTone.test.ts), [customTuningsStore.test.ts](../../../src/stores/customTuningsStore.test.ts).

## 04 — Metronom

Současná sekce: `/metronome`. Nativní kroky: W18.

40–240 BPM, tap, 7 taktových měr, subdivize, akcent/mute a zvuky jednotlivých dob, 5 kliků, drum mode, swing, předtakt, rampy, tiché takty, polyrytmus, 6 továrních a vlastní presety, kyvadlo nebo tečky.

**Data:** rocker-metronome; runtime transport.

**Přejímací scénář:** Znovu načíst vlastní preset; ověřit hranice předtaktu, rampy a ticha i součet swingových dob; přepnutí vizualizace nemění časování.

**Rozdíl nebo omezení:** Zvukové události používají Web Audio clock; nativní callback, výpadky a společný transport vyžadují novou implementaci.

**Zdroje:** [MetronomeWidget.tsx](../../../src/components/metronome/MetronomeWidget.tsx), [TrainerPanel.tsx](../../../src/components/metronome/TrainerPanel.tsx), [PolyrhythmPanel.tsx](../../../src/components/metronome/PolyrhythmPanel.tsx), [metronome.ts](../../../src/audio/metronome.ts), [metronomeConfig.ts](../../../src/lib/metronomeConfig.ts).

**Existující automatické kontroly:** [metronomeConfig.test.ts](../../../src/lib/metronomeConfig.test.ts), [metronomePresets.test.ts](../../../src/lib/metronomePresets.test.ts), [metronome.test.ts](../../../src/audio/metronome.test.ts).

## 05 — Výběr lekce

Současná sekce: `/practice`. Nativní kroky: W06, W25.

3 etapy, 18 lekcí, dokončení, další nedokončená lekce.

**Data:** rocker-lesson-progress.

**Přejímací scénář:** Každou lekci najít podle stabilního ID; po importu jsou stejné dokončené lekce.

**Rozdíl nebo omezení:** Uložen je seznam dokončených ID, ne pozice uvnitř lekce.

**Zdroje:** [page.tsx](../../../src/app/practice/page.tsx), [lessons.ts](../../../src/lib/lessons.ts), [lessonEngine.ts](../../../src/lib/lessonEngine.ts).

**Existující automatické kontroly:** [lessonEngine.test.ts](../../../src/lib/lessonEngine.test.ts), [lessonProgress.test.ts](../../../src/lib/lessonProgress.test.ts).

## 06 — Průběh lekce

Současná sekce: `/practice`. Nativní kroky: W25.

Textové, akordové a tónové kroky; kontrola tónu; dokončení lekce.

**Data:** rocker-lesson-progress; aktuální krok v paměti.

**Přejímací scénář:** Projít každý typ kroku, opakovaným potvrzením nevytvořit duplicitu dokončení; bez vstupu se tónová kontrola netváří úspěšně.

**Rozdíl nebo omezení:** Monofonní detekce nevyhodnocuje celý akord; obnovení rozpracovaného kroku je desktopové doplnění.

**Zdroje:** [LessonPlayer.tsx](../../../src/components/practice/LessonPlayer.tsx), [StepView.tsx](../../../src/components/practice/StepView.tsx), [NoteCheck.tsx](../../../src/components/practice/NoteCheck.tsx).

**Existující automatické kontroly:** [lessonEngine.test.ts](../../../src/lib/lessonEngine.test.ts).

## 07 — Vlastní skladba

Současná sekce: `/practice`. Nativní kroky: W21.

MP3/WAV a prohlížečem podporovaný zvuk, waveform, A/B, zpomalení bez změny výšky, předtakt a postupné zrychlení.

**Data:** Soubor, A/B a transport v paměti.

**Přejímací scénář:** Otevřít referenční WAV/MP3, nastavit A/B a rychlost, opakovaně seekovat; ověřit délku a ukončení starého přehrávání.

**Rozdíl nebo omezení:** Přehrávač využívá browser preservesPitch; trvalé pojmenované lokální úseky je třeba doplnit.

**Zdroje:** [RiffTrainer.tsx](../../../src/components/practice/RiffTrainer.tsx), [Waveform.tsx](../../../src/components/practice/Waveform.tsx), [riffPlayer.ts](../../../src/audio/riffPlayer.ts).

**Existující automatické kontroly:** [riffPlayer.test.ts](../../../src/audio/riffPlayer.test.ts), [waveform.test.ts](../../../src/lib/waveform.test.ts).

## 08 — YouTube cvičení

Současná sekce: `/practice`. Nativní kroky: W04, W30.

Oficiální iframe, URL/video ID, A/B a dostupné rychlosti, pojmenování a uložení smyček.

**Data:** rocker-yt-loops.

**Přejímací scénář:** Obnovit zkušební smyčku bez síťového požadavku; samostatně ověřit povolené živé video, chybu vložení a offline stav.

**Rozdíl nebo omezení:** Syntetické video ID není reálné video. Souběh s ASIO a aktuální pravidla embedu čekají na W04/W30.

**Zdroje:** [YouTubePlayer.tsx](../../../src/components/youtube/YouTubePlayer.tsx), [useYouTubePlayer.ts](../../../src/components/youtube/useYouTubePlayer.ts), [youtube.ts](../../../src/lib/youtube.ts), [youtubeApi.ts](../../../src/lib/youtubeApi.ts).

**Existující automatické kontroly:** [youtube.test.ts](../../../src/lib/youtube.test.ts), [ytLoops.test.ts](../../../src/lib/ytLoops.test.ts).

## 09 — Přesný trénink

Současná sekce: `/practice`. Nativní kroky: W26.

Průchod po tónech podle pitch class, chyby na pozicích, nejhorší okno nejvýše 3 tónů, 3 čisté repetice; chyba v drillu resetuje sérii.

**Data:** 5 cvičení; stav drillu v paměti.

**Přejímací scénář:** Přehrát zaznamenané stopy čistého průchodu, chyby a tří drillů; ověřit i tón o oktávu výše.

**Rozdíl nebo omezení:** UI neumí rozlišit sousední stejné tóny; cvičení se tomu vyhýbají. Waveforma v návrhu je druhý režim, ne náhrada stávajícího.

**Zdroje:** [SurgicalTrainer.tsx](../../../src/components/practice/SurgicalTrainer.tsx), [surgicalEngine.ts](../../../src/lib/surgicalEngine.ts), [exercises.ts](../../../src/lib/exercises.ts).

**Existující automatické kontroly:** [surgicalEngine.test.ts](../../../src/lib/surgicalEngine.test.ts).

## 10 — Power chordy

Současná sekce: `/metal`. Nativní kroky: W27.

3 cvičení, diagramy, změny poloh, další akord, metronom, kola, zrychlování a BPM rekordy.

**Data:** rocker-progress; trénink v paměti.

**Přejímací scénář:** Projít kolo proti metronomu, správně změnit akord i tempo a uložit pouze lepší rekord.

**Rozdíl nebo omezení:** Automatické hodnocení správnosti celého akordu není doložené; nevytvářet takový slib.

**Zdroje:** [PowerChordTrainer.tsx](../../../src/components/metal/PowerChordTrainer.tsx), [powerChordTrainer.ts](../../../src/lib/powerChordTrainer.ts), [metalExercises.ts](../../../src/lib/metalExercises.ts).

**Existující automatické kontroly:** [powerChordTrainer.test.ts](../../../src/lib/powerChordTrainer.test.ts), [powerChords.test.ts](../../../src/lib/powerChords.test.ts).

## 11 — Gallop

Současná sekce: `/metal`. Nativní kroky: W27.

2 vzory včetně reverzního, synchronní náhled, kola a zrychlování, tempo rekordy.

**Data:** rocker-progress; transport v paměti.

**Přejímací scénář:** Ověřit časové pozice patternu a změnu kola/tempa; zastavení ukončí všechny naplánované události.

**Rozdíl nebo omezení:** Pocit rytmu při skutečné kytaře čeká na hardware.

**Zdroje:** [GallopTrainer.tsx](../../../src/components/metal/GallopTrainer.tsx), [gallopPlayer.ts](../../../src/audio/gallopPlayer.ts), [gallopTrainer.ts](../../../src/lib/gallopTrainer.ts).

**Existující automatické kontroly:** [gallopTrainer.test.ts](../../../src/lib/gallopTrainer.test.ts), [gallopPlayer.test.ts](../../../src/audio/gallopPlayer.test.ts).

## 12 — Palm mute

Současná sekce: `/metal`. Nativní kroky: W19, W27.

Centroid a onset, kalibrace tlumeného/otevřeného úhozu, 2 drilly a skóre.

**Data:** Kalibrace a drill v paměti; sdílené nastavení vstupu.

**Přejímací scénář:** Ověřit čistou logiku kalibrace a skóre, ticho a nedostatečnou kalibraci; konečné rozlišení na konkrétní kytaře.

**Rozdíl nebo omezení:** Syntetický tón nepotvrzuje rozpoznání techniky; úspěšný klasifikátor nelze prohlásit bez kytary.

**Zdroje:** [PalmMuteStudio.tsx](../../../src/components/metal/PalmMuteStudio.tsx), [muteAnalyzer.ts](../../../src/audio/muteAnalyzer.ts), [palmMute.ts](../../../src/lib/palmMute.ts).

**Existující automatické kontroly:** [palmMute.test.ts](../../../src/lib/palmMute.test.ts).

## 13 — Aparát

Současná sekce: `/amp`. Nativní kroky: W17.

4 tovární a vlastní presety, compression, drive, 3 typy zkreslení, 3 pásma EQ, 3 charaktery kabinetu, delay/reverb/chorus/level, scope/metr, singleton napříč navigací a záznam po efektech.

**Data:** rocker-amp,rocker-amp-presets.

**Přejímací scénář:** Obnovit vlastní preset, změnit parametr, přejít do jiné obrazovky a zaznamenat zaaparátovaný zvuk.

**Rozdíl nebo omezení:** Kompresor zůstává v řetězci i při nulové intenzitě. Nativní bypass a nová DSP nejsou součástí W01.

**Zdroje:** [AmpStudio.tsx](../../../src/components/amp/AmpStudio.tsx), [AmpHost.tsx](../../../src/components/amp/AmpHost.tsx), [ampChain.ts](../../../src/audio/ampChain.ts), [ampEngine.ts](../../../src/audio/ampEngine.ts), [ampPresets.ts](../../../src/lib/ampPresets.ts).

**Existující automatické kontroly:** [ampEngine.test.ts](../../../src/audio/ampEngine.test.ts), [ampPresets.test.ts](../../../src/lib/ampPresets.test.ts).

## 14 — Nahrávky

Současná sekce: `/recorder`. Nativní kroky: W22.

MediaRecorder, seznam/přejmenování/mazání, poslech, A/B dvou take-ů, pojmenované časové značky, skok na značku, export původního kodeku.

**Data:** IndexedDB rocker/recordings v1.

**Přejímací scénář:** Načíst všech 5 referenčních záznamů, obnovit značku a přehrát; názvy souborů a MIME souhlasí; export není přejmenovaný blob.

**Rozdíl nebo omezení:** Délka se dnes měří nástěnným časem; desktop potřebuje počet vzorků a průběžný bezpečný zápis.

**Zdroje:** [RecordingItem.tsx](../../../src/components/recorder/RecordingItem.tsx), [AbCompare.tsx](../../../src/components/recorder/AbCompare.tsx), [recorder.ts](../../../src/audio/recorder.ts), [recordingsDb.ts](../../../src/lib/recordingsDb.ts).

**Existující automatické kontroly:** [recordings.test.ts](../../../src/lib/recordings.test.ts), [recordingsDb.test.ts](../../../src/lib/recordingsDb.test.ts).

## 15 — Studio

Současná sekce: `/recorder`. Nativní kroky: W24.

Záznam dalších stop při přehrávání projektu, gain/mute, odstranění stopy a WAV mixdown.

**Data:** Projekt a stopy pouze v paměti.

**Přejímací scénář:** Vrstvit známé signály, ztlumit jednu stopu, exportovat mix a ověřit délku a kanály.

**Rozdíl nebo omezení:** Trvalé projekty, pan, solo, přesný posun, undo a kompenzace latence jsou nové desktopové části.

**Zdroje:** [Studio.tsx](../../../src/components/studio/Studio.tsx), [multitrackEngine.ts](../../../src/audio/multitrackEngine.ts), [multitrack.ts](../../../src/lib/multitrack.ts).

**Existující automatické kontroly:** [multitrackEngine.test.ts](../../../src/audio/multitrackEngine.test.ts), [multitrack.test.ts](../../../src/lib/multitrack.test.ts), [wav.test.ts](../../../src/lib/wav.test.ts).

## 16 — Looper

Současná sekce: `/recorder`. Nativní kroky: W23.

První záznam určí délku; přehrávání, overdub, mix vrstev a vymazání.

**Data:** Smyčka a vrstvy v paměti.

**Přejímací scénář:** Ověřit délku první smyčky a opakované vrstvení, zastavení a reset; posun vrstev přiznat.

**Rozdíl nebo omezení:** Dnešní vrstvy mohou být posunuté o audio latenci. Undo samostatné vrstvy a trvalá session jsou nové.

**Zdroje:** [LoopPedal.tsx](../../../src/components/looper/LoopPedal.tsx), [loopPedal.ts](../../../src/audio/loopPedal.ts), [loopMix.ts](../../../src/lib/loopMix.ts).

**Existující automatické kontroly:** [loopPedal.test.ts](../../../src/audio/loopPedal.test.ts), [loopMix.test.ts](../../../src/lib/loopMix.test.ts).

## 17 — Akordy

Současná sekce: `/library`. Nativní kroky: W28.

Kategorie open/barré/power, prstoklad, barré, poslech, levá ruka a notace.

**Data:** rocker-settings; 26 akordů.

**Přejímací scénář:** Zkontrolovat všechny diagramy a MIDI tóny, zrcadlení pro leváka a české H.

**Rozdíl nebo omezení:** Nativní kreslení a přístupná textová alternativa čekají na implementaci.

**Zdroje:** [ChordCard.tsx](../../../src/components/library/ChordCard.tsx), [ChordDiagram.tsx](../../../src/components/library/ChordDiagram.tsx), [chordPlayer.ts](../../../src/audio/chordPlayer.ts), [chords.ts](../../../src/lib/chords.ts).

**Existující automatické kontroly:** [chords.test.ts](../../../src/lib/chords.test.ts).

## 18 — Stupnice

Současná sekce: `/library`. Nativní kroky: W28.

Tonika, typ, hmatník, kořenové tóny, vybrané ladění.

**Data:** rocker-settings,rocker-custom-tunings; 6 stupnic.

**Přejímací scénář:** Změnit toniku a vlastní ladění; zkontrolovat polohy v pravé i levé orientaci.

**Rozdíl nebo omezení:** Další polohy či rozsahy z návrhu nejsou automaticky nynější schopnost.

**Zdroje:** [ScaleExplorer.tsx](../../../src/components/library/ScaleExplorer.tsx), [Fretboard.tsx](../../../src/components/library/Fretboard.tsx), [scales.ts](../../../src/lib/scales.ts), [resolveTuning.ts](../../../src/lib/resolveTuning.ts).

**Existující automatické kontroly:** [scales.test.ts](../../../src/lib/scales.test.ts), [resolveTuning.test.ts](../../../src/lib/resolveTuning.test.ts).

## 19 — Riffy

Současná sekce: `/library`. Nativní kroky: W28.

Tabulatura, tempo, ladění, technika, obtížnost a náhled; 2 pojmenované skladby a 12 riffů označených jako původní.

**Data:** 14 datových riffů; volba v paměti.

**Přejímací scénář:** Zkontrolovat ID, noty a časování všech riffů; samostatně prověřit distribuční práva k pojmenovaným skladbám.

**Rozdíl nebo omezení:** Licence repozitáře sama nedokládá práva k oběma cizím skladbám; zařazeno do rozhodnutí W02.

**Zdroje:** [RiffLibrary.tsx](../../../src/components/library/RiffLibrary.tsx), [metalRiffs.ts](../../../src/lib/metalRiffs.ts), [riffPlayer.ts](../../../src/audio/riffPlayer.ts).

**Existující automatické kontroly:** [metalRiffs.test.ts](../../../src/lib/metalRiffs.test.ts).

## 20 — Noty a tabulatury

Současná sekce: `/notation`. Nativní kroky: W04, W29.

Webový AlphaTab, Guitar Pro a MusicXML, stopy, zoom, přehrávání/tempo, lokální Bravura a SONiVOX.

**Data:** Soubor a přehrávání v paměti; public/alphatab.

**Přejímací scénář:** Importovat vlastní MusicXML a samostatně vzorky GP formátů; správně zobrazit tóny a přehrát zvolenou stopu.

**Rozdíl nebo omezení:** Nativní renderer a jeden společný audio výstup nejsou ověřené; skutečné GP vzorky je třeba doplnit ve W04.

**Zdroje:** [NotationViewer.tsx](../../../src/components/notation/NotationViewer.tsx).

**Existující automatické kontroly:** Samostatný test pohledu chybí; ruční a integrační ověření je výslovně nutné..

## 21 — Hmatník

Současná sekce: `/games`. Nativní kroky: W28.

Hledání tónů, otázky, odpovědi, skóre, vybrané ladění a notace.

**Data:** Volba a skóre v paměti; rocker-settings.

**Přejímací scénář:** Správná i chybná odpověď mění skóre podle současné logiky; vlastní ladění odpovídá hmatníku.

**Rozdíl nebo omezení:** Nativní klávesnice/UI Automation se teprve doplní.

**Zdroje:** [FretboardGame.tsx](../../../src/components/games/FretboardGame.tsx), [fretboardGame.ts](../../../src/lib/fretboardGame.ts).

**Existující automatické kontroly:** [fretboardGame.test.ts](../../../src/lib/fretboardGame.test.ts).

## 22 — Sluch

Současná sekce: `/games`. Nativní kroky: W28.

Přehrání intervalů, opakování, volba odpovědi, skóre.

**Data:** Otázka a skóre v paměti.

**Přejímací scénář:** Ověřit intervaly a vyhodnocení odpovědí; fungovat jen s výstupním zařízením.

**Rozdíl nebo omezení:** Sluchový a hardwarový poslech zůstává ruční kontrolou.

**Zdroje:** [EarTraining.tsx](../../../src/components/games/EarTraining.tsx), [earTraining.ts](../../../src/lib/earTraining.ts).

**Existující automatické kontroly:** [earTraining.test.ts](../../../src/lib/earTraining.test.ts).

## 23 — AI — rozhovor

Současná sekce: `/tutor`. Nativní kroky: W10, W31.

BYOK Gemini, stream odpovědi, kontext kytary, historie a nová konverzace; chyby a zrušení.

**Data:** rocker-ai,rocker-chat.

**Přejímací scénář:** Mock server: úspěch, zrušení, síť a neplatný klíč; obnovit stejné role/texty z referenční zálohy.

**Rozdíl nebo omezení:** Historie nemá ID ani čas zprávy. Klíč je nyní v localStorage a exportu; native jej musí oddělit.

**Zdroje:** [ChatPanel.tsx](../../../src/components/tutor/ChatPanel.tsx), [geminiClient.ts](../../../src/lib/ai/geminiClient.ts), [context.ts](../../../src/lib/ai/context.ts), [chatStore.ts](../../../src/stores/chatStore.ts).

**Existující automatické kontroly:** [geminiClient.test.ts](../../../src/lib/ai/geminiClient.test.ts), [context.test.ts](../../../src/lib/ai/context.test.ts), [types.test.ts](../../../src/lib/ai/types.test.ts).

## 24 — AI — plán

Současná sekce: `/tutor`. Nativní kroky: W31, W33.

Doba a zaměření, validace JSON odpovědi, přehled bloků a odkazy na sekce.

**Data:** rocker-ai; vygenerovaný plán v paměti.

**Přejímací scénář:** Odmítnout neplatné JSON a neznámé cíle; součet minut odpovídá blokům.

**Rozdíl nebo omezení:** Trvalé uložení, editace před přijetím a odkazy do konkrétních úloh jsou desktopová doplnění.

**Zdroje:** [PlanPanel.tsx](../../../src/components/tutor/PlanPanel.tsx), [planGenerator.ts](../../../src/lib/ai/planGenerator.ts).

**Existující automatické kontroly:** [planGenerator.test.ts](../../../src/lib/ai/planGenerator.test.ts).

## 25 — AI — zpětná vazba

Současná sekce: `/tutor`. Nativní kroky: W31.

Souhrn statistik, sérií, minut a rekordů pro AI; rady k dalšímu cvičení.

**Data:** rocker-progress,rocker-lesson-progress,rocker-ai.

**Přejímací scénář:** Vstupní text musí odpovídat testovací historii a nesmí tvrdit, že byly analyzované audio vzorky.

**Rozdíl nebo omezení:** Není to rozbor skutečného zvuku. Dostupnost/kvóty aktuální služby nebyly v W01 online ověřené.

**Zdroje:** [FeedbackPanel.tsx](../../../src/components/tutor/FeedbackPanel.tsx), [feedback.ts](../../../src/lib/ai/feedback.ts).

**Existující automatické kontroly:** [feedback.test.ts](../../../src/lib/ai/feedback.test.ts).

## 26 — Moje kytara

Současná sekce: `/profile`. Nativní kroky: W32.

Jméno, tělo, snímače, menzura, struny, poznámky; 7 stylů doporučení zvuku; ladění sdílené přes settings.

**Data:** rocker-instrument,rocker-settings.

**Přejímací scénář:** Obnovit umělý profil, změnit snímače a ověřit doporučení; ladění nesmí mít dvě různé uložené hodnoty.

**Rozdíl nebo omezení:** Není potřeba sbírat skutečné osobní údaje pro baseline.

**Zdroje:** [ProfileCard.tsx](../../../src/components/instrument/ProfileCard.tsx), [ToneAdvisor.tsx](../../../src/components/instrument/ToneAdvisor.tsx), [instrument.ts](../../../src/lib/instrument.ts).

**Existující automatické kontroly:** [instrument.test.ts](../../../src/lib/instrument.test.ts).

## 27 — Pokrok

Současná sekce: `/profile`. Nativní kroky: W32.

Denní minuty, aktuální/nejdelší série, 11 odměn, BPM rekordy, přestávky, reset; počítá se čas viditelné cvičební stránky.

**Data:** rocker-progress,rocker-lesson-progress.

**Přejímací scénář:** Ověřit půlnoc, sérii do včerejška, deduplikaci odměn a zachování historie po importu.

**Rozdíl nebo omezení:** Staré /studio a /looper jsou v seznamu, ale přesměrují na nezapočítávaný /recorder. Nová session musí zahrnout Live.

**Zdroje:** [ProgressDetail.tsx](../../../src/components/profile/ProgressDetail.tsx), [PracticeTimeTracker.tsx](../../../src/components/PracticeTimeTracker.tsx), [AchievementWatcher.tsx](../../../src/components/AchievementWatcher.tsx), [BreakReminder.tsx](../../../src/components/BreakReminder.tsx), [progress.ts](../../../src/lib/progress.ts).

**Existující automatické kontroly:** [progress.test.ts](../../../src/lib/progress.test.ts), [practiceTime.test.ts](../../../src/lib/practiceTime.test.ts), [breakReminder.test.ts](../../../src/lib/breakReminder.test.ts).

## 28 — Nastavení

Současná sekce: `/profile`. Nativní kroky: W34.

Téma Studio/Vintage/Glassy, light/dark/auto, desktop/mobile/auto, levá ruka, H/B, Zen, zvuk, AI a zálohy.

**Data:** Všech 11 perzistentních stores dle oblasti.

**Přejímací scénář:** Obnovit všechny smysluplné preference; Zen a notace přežijí restart, neplatná data se opraví.

**Rozdíl nebo omezení:** Native má Vintage a systémové rozložení; mobilní přepínač a haptika nejsou povinná shoda.

**Zdroje:** [page.tsx](../../../src/app/profile/page.tsx), [ThemePicker.tsx](../../../src/components/ThemePicker.tsx), [LayoutModeSwitcher.tsx](../../../src/components/LayoutModeSwitcher.tsx), [AiSettings.tsx](../../../src/components/AiSettings.tsx), [settingsStore.ts](../../../src/stores/settingsStore.ts).

**Existující automatické kontroly:** [customTuningsStore.test.ts](../../../src/stores/customTuningsStore.test.ts), [metronomeSettingsStore.test.ts](../../../src/stores/metronomeSettingsStore.test.ts).

## 29 — Zvuková zařízení

Současná sekce: `/profile`. Nativní kroky: W14, W15, W16, W20.

getUserMedia bez echo/noise/AGC, zařízení, měřič, šumová kalibrace; mono nahrávací větev sčítá dostupné vstupní kanály; ukazatel latence je baseLatency + outputLatency.

**Data:** Vybrané zařízení a AudioContext v paměti.

**Přejímací scénář:** Ověřit start/stop/závody a chyby oprávnění; referenční dvoukanálové WAV rozlišují každý vstup.

**Rozdíl nebo omezení:** Vstupní kanál se dnes nevolí samostatně. Ukazatel nezahrnuje celý vstup; ASIO a jeho konfigurace jsou nové.

**Zdroje:** [AudioInputSettings.tsx](../../../src/components/AudioInputSettings.tsx), [audioEngine.ts](../../../src/audio/audioEngine.ts), [audioStore.ts](../../../src/stores/audioStore.ts).

**Existující automatické kontroly:** [audioEngine.test.ts](../../../src/audio/audioEngine.test.ts).

## 30 — Zálohy a soubory

Současná sekce: `/profile`. Nativní kroky: W09, W10, W11, W12, W37.

Export a obnova nastavení přes soubor, podpora částečných/starších sad, omezení známých klíčů.

**Data:** settings-backup v2; 11 JSON stringů; IndexedDB zvlášť.

**Přejímací scénář:** Prohlédnout úplnou, starší, částečnou i chybnou umělou zálohu; samostatně ověřit nahrávky a markery.

**Rozdíl nebo omezení:** Export vynechává audio a obsahuje AI klíč, pokud je vyplněný. Kompletní přenosový archiv dosud neexistuje.

**Zdroje:** [DataBackup.tsx](../../../src/components/DataBackup.tsx), [backup.ts](../../../src/lib/backup.ts), [download.ts](../../../src/lib/download.ts).

**Existující automatické kontroly:** [backup.test.ts](../../../src/lib/backup.test.ts).

## 31 — První spuštění

Současná sekce: `AppShell`. Nativní kroky: W34.

Průvodce vzhledem, zapojením/nástroji; dokončení nebo přeskočení se uloží.

**Data:** rocker-settings.onboarded.

**Přejímací scénář:** Spustit čistý profil i profil s onboarded=true; další start nesmí znovu vynutit průvodce.

**Rozdíl nebo omezení:** Nativní import a výběr ASIO/vstupního kanálu jsou nové kroky; musí jít pokračovat bez kytary.

**Zdroje:** [Onboarding.tsx](../../../src/components/Onboarding.tsx), [AppShell.tsx](../../../src/components/AppShell.tsx).

**Existující automatické kontroly:** Samostatný test pohledu chybí; ruční a integrační ověření je výslovně nutné..

## 32 — Export nahrávky

Současná sekce: `/recorder`. Nativní kroky: W22, W24, W35.

Stažení originálního blobu se správnou příponou; Studio exportuje WAV mix; řízené uvolnění URL.

**Data:** Nahrávka z IndexedDB nebo mix projektu v paměti.

**Přejímací scénář:** Zkontrolovat MIME, skutečný obsah souboru, diakritiku a zakázané znaky; WAV má správné kanály/délku.

**Rozdíl nebo omezení:** Nativní systémový dialog, výběr formátu/rozsahu, zrušení a přepsání jsou desktopové úkoly.

**Zdroje:** [RecordingItem.tsx](../../../src/components/recorder/RecordingItem.tsx), [download.ts](../../../src/lib/download.ts), [wav.ts](../../../src/lib/wav.ts), [Studio.tsx](../../../src/components/studio/Studio.tsx).

**Existující automatické kontroly:** [recordings.test.ts](../../../src/lib/recordings.test.ts), [wav.test.ts](../../../src/lib/wav.test.ts).

## Společné funkce

### navigation

13 hlavních sekcí, 4 historická přesměrování, centrální AmpHost a nastavení.

**Přejímka:** Žádná sekce ani podsekce se neztratí; navigace nesmí restartovat host.

**Zdroje:** [AppShell.tsx](../../../src/components/AppShell.tsx), [screens.ts](../../../src/lib/screens.ts).

### offline-pwa

Statický export, service worker, instalace PWA, oznámení aktualizace.

**Přejímka:** Desktopovou náhradou je instalátor, aktualizace a offline obsah, nikoli přenos service workeru.

**Zdroje:** [sw.js](../../../public/sw.js), [stamp-sw.mjs](../../../scripts/stamp-sw.mjs), [ServiceWorkerRegister.tsx](../../../src/components/ServiceWorkerRegister.tsx).

### appearance-accessibility

Tři vzhledy, barevné režimy, responzivita, focus-visible a reduced motion.

**Přejímka:** Vintage, systémové DPI/vysoký kontrast, klávesnice a skutečné Windows prvky.

**Zdroje:** [theme-tokens.css](../../../src/app/theme-tokens.css), [KnobControl.tsx](../../../src/components/widgets/KnobControl.tsx).

### runtime-only-state

Transporty, zařízení a pracovní stav nejsou v záloze localStorage.

**Přejímka:** Import nevymýšlí chybějící sessions; native zavede perzistenci projektů výslovně.

**Zdroje:** [audioStore.ts](../../../src/stores/audioStore.ts), [metronomeStore.ts](../../../src/stores/metronomeStore.ts), [riffPlayerStore.ts](../../../src/stores/riffPlayerStore.ts), [gallopPlayerStore.ts](../../../src/stores/gallopPlayerStore.ts), [ampEngineStore.ts](../../../src/stores/ampEngineStore.ts).
