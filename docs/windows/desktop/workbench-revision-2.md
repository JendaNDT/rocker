# Oprava pracovní plochy po přejímce 0.3.0

Jenda potvrdil, že běží 0.3.0, ale rozložení stále připomíná původní
aplikaci. Kontrola kódu a skutečných snímků potvrzuje nedostatečnou změnu
UI. Úspěšné sestavení a funkční kontroly 0.3.0 nepotvrzují splnění
požadované vizuální přestavby. Předchozí označení celé přestavby za hotovou
bylo předčasné.

## Změny pro 0.3.1

- Pevná pracovní lišta pod nabídkou: soubory, Studio a hudební nástroje.
- Průzkumník rozdělený na pracovní plochu, výuku, soubory a reference.
  Celý katalog zůstává prohledatelný napříč oblastmi.
- Přehrávač v jedné kompaktní liště. Měření cvičení a denní plán jsou
  dostupné v jeho vlastní nabídce; stav cvičení zůstává společný.
- Ladička, metronom, aparát a Looper se poprvé otevřou jako samostatná
  Windows okna. Volitelně je lze připojit zpět. Zavření zachová stav nástroje.
- Hudební nástroje mají pevné příkazy, samostatně posuvné vlastnosti
  a hlavní přístroj. Kyvadlo a tempo metronomu jsou vedle sebe.
- Nahrávka má editor s velkou zvukovou vlnou, časovým pravítkem, seznamem
  značek, pevnou lištou a současně viditelnými hranicemi A a B.
- Studio má časové pravítko, oddělené dráhy a regiony se skutečným začátkem,
  délkou, mute/solo a výběrem stopy. Regiony nesimulují zvukové vlny.
- Stará konfigurace oken se převede na revizi rozhraní 2. Hudební soubory,
  dokumenty a jejich nastavení zůstanou zachované; nová uživatelská úprava
  rozložení se při dalším spuštění znovu nepřepisuje.
- Dialog O aplikaci ukazuje verzi, revizi pracovní plochy a sestavení.

## Ověření a předání

[Windows běh 38491131359](https://github.com/JendaNDT/rocker/actions/runs/38491131359) nad `3ca755016f54f2fc36ef5e2f5d4147afa35018e7` prošel včetně sestavení, obou UI profilů, 32 funkcí v obou motivech a nainstalované aplikace. Celkem 49 kontrol oken/interakcí, tři různé skutečné šířky a 22 native sad. Instalační cyklus ověřil opravu DLL, Unicode aktivaci projektu a zachování dat po odinstalaci.

Regresní kontroly zachytily a odstranily dvojí rodičovství A/B a neobnovený obsah právě vybrané záložky. Editor po načtení má skutečný načtený vizuální strom a měřenou výšku zvukové vlny. Oba motivy mají ověřený kontrast křivky proti jejímu skutečnému pozadí (7,66 a 5,51). Při přepnutí nastavení se mění také viditelný obsah, nejen záhlaví.

Lokálně prošlo 567 testů, typy, lint, přenosné doménové a datové kontroly. Galerie prošla dekódováním všech 64 obrázků, 64 zvětšenými pohledy, hledáním, návratem fokusu a kontrolou šířky 390 px. Prohlížečová kontrola běžela přes interní HTTP; souborový protokol blokuje politika zdejšího spravovaného prohlížeče, jeho offline ověření se netvrdí.

[Soukromé předání 0.3.1 RC1](https://github.com/JendaNDT/rocker/releases/tag/untagged-b2ab831b9bb79eaacd0c), [ověření přesných nahraných souborů](../releases/delivery-validation.json), [skutečná galerie](gallery.html). Starší 0.3.0 zůstává oddělené. Poslech, celková odezva, fyzické monitory, Narrator, online služby a Jendovo posouzení vzhledu zůstávají samostatnou přejímkou.
