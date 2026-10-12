# Rocker 0.2.0 RC3 — propracovaný Vintage vzhled

Nativní WinUI 3 aplikace ve Vintage designu se všemi 32 pracovními pohledy a společným C++/JUCE zvukovým jádrem. Obsahuje Live, ladičku, rozšířený metronom, aparát, lekce a trenažéry, knihovny a hry, vlastní skladby/noty, nahrávání, Looper, Studio, profil/pokrok, volitelnou AI, YouTube, převod webových dat a úplné zálohy.

## Nový vzhled

- Kovové knoflíky aparátu s obvodovými stupnicemi a adaptivním rozložením.
- Společná přesná ladička pro Live a samostatný pohled; bez měřeného tónu je ukazatel skrytý.
- Segmentové měřáky −60 až 0 dBFS, velký údaj tempa a propracovanější akordy/hmatník.
- Sjednocené povrchy, měděné akcenty, typografie a vlastní ikona aplikace. Tmavá i světlá varianta.
- Standardní Windows ovládání, klávesnice a automatizační jména zůstávají funkční.

## Změny rozložení

- Čitelné názvy navigace na desktopu a přímý vstup do Přehledu, Live a Nastavení.
- Spodní panel zobrazuje jen relevantní cvičení a otevřený soubor; prázdný přehrávač nezabírá místo. Zastavení zvuku zůstává dostupné.
- Přehled zdůrazňuje další konkrétní krok, postup plánu a bloky v adaptivní mřížce.
- Live upřednostňuje spuštění, poslech kytary a vstupní měřák; nastavení nahrávky a diagnostika se rozbalují.
- Přehrát/pozastavit je jeden označený příkaz, úplné chyby zůstávají čitelné.

## Stažení a spuštění

1. Stáhni **Rocker-Windows-Setup-0.2.0-x64.exe**, spusť jej a potom otevři **Rocker** z nabídky Start. Vyžaduje Windows 11 x64 od 24H2 (build 26100).
2. Vývojové nástroje ani zdroje nepotřebuješ. .NET a Windows App SDK jsou přibalené. Instalace je pro aktuálního uživatele bez administrátorských oprávnění.
3. M-Track Solo a sluchátka nastav v průvodci; vyber jediný vstup s kytarou. Monitoring zapni výslovně s nízkou hlasitostí. Ovladač M-Audio se stahuje od výrobce.
4. Návod je v **Nápověda → Návod k používání**. Předchozí prototyp zavři před instalací. Instalátor zálohuje existující profil a při chybě přípravy zastaví aktualizaci před změnou původní instalace. Odinstalace ponechá uživatelská data.

Volitelný **Rocker-Windows-x64.zip** je přenosná varianta: Extrahovat vše, potom Rocker.Probe.exe. **Code → Download ZIP** není sestavená aplikace.

**Rocker-Windows-sources.zip** obsahuje odpovídající zdroje a sestavovací postupy zvlášť. Jejich stažení není pro používání potřebné. Licenční oznámení zůstávají v aplikaci. Jde o soukromé předání v tomto repozitáři.

## Ověření a hranice

Windows běh 38301809142, revize 6be7ff9, prošel kontrolou UX layoutu a devíti detailními snímky nového vzhledu. Tato konkrétní revize prošla automatickým sestavením na Windows, všemi 22 nativními sadami, doménovými/datovými/migračními a mediálními kontrolami, konstrukcí všech 32 pohledů a UI Automation. Instalační cyklus ověřil přechod verzí, blokování neplatného profilu bez změny zástupce/asociace, shodu všech souborů s přenosným balíkem, opravu DLL, aktivaci projektu a odinstalaci se zachováním dat. SHA256SUMS.txt a candidate-validation.json identifikují ověřený balík.

Skutečné hraní, celkovou odezvu a stabilní ASIO buffer na bratrově M-Track Solo je ještě potřeba potvrdit. Online YouTube/OpenAI, fyzické uspání/odpojení, Narrator/DPI a čistá offline VM nebyly ručně ověřené. Zrychlený hodinový render není hodina skutečného hraní. Přechod verzí používá označené instalační fixture se stejnými kandidátními binárkami; netvrdí kompatibilitu se všemi historickými verzemi.

Balík je nepodepsaný; kompatibilita se zapnutým Smart App Control není potvrzená. YouTube vyžaduje internet a Evergreen WebView2; ostatní hlavní nástroje jej nepotřebují. AI je volitelná s vlastním OpenAI klíčem, bez posílání audio nahrávek. Osobní Gemini cesta je blokovaná podle zaznamenaných podmínek. Noty používají vlastní strunnou/bicí syntézu, nikoli celou banku General MIDI. Detektor hodnotí jednotlivé tóny a neslibuje obecné polyfonní/rytmické skóre.

Úplná matice, návod, protokoly a připravený test s bratrem jsou v docs/windows/w43. W04–W43 jsou dokončené; reálná přejímka a následné stabilní vydání navazují ve W44–W48.
