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

## Ověření

Lokálně prošly typy webové aplikace, 567 testů, doménové kontroly a
datové kontroly včetně rozpoznání starého rozložení a uchování nové revize.
Windows přejímka musí navíc ověřit skutečné otevření nástrojových oken,
výšku přehrávače při třech šířkách, editor nahrávky, všech 32 pohledů,
instalaci a opravu. Samotné snímky a tyto kontroly nepotvrzují fyzický
poslech, celkovou odezvu, Narrator ani práci na více monitorech.

Nový instalátor lze nabídnout teprve po úspěšném Windows běhu a vizuální
kontrole jeho snímků. Předchozí galerie a kandidát 0.3.0 nejsou důkazem
tohoto vzhledu.
