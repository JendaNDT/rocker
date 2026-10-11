# W02 — Platforma, licence a předání bratrovi

Stav k 10. 10. 2026: **W02 je dokončený jako rozhodnutí o platformě a předání.**
Vybrána bezplatná AGPLv3 cesta JUCE a GPLv3 pro přibalené ASIO hlavičky.
Bratr stáhne jen hotový instalátor; odpovídající zdroje budou dostupné zvlášť
samostatným odkazem. Uživatel tento způsob předání potvrdil. Samotný W02
nezahrnoval založení ani sestavení aplikace; na něj navazuje [technický řez W03](../../../windows/README.md).
Tento dokument nepřelicencovává původní repozitář.

Navazuje na [roadmapu](../../../WINDOWS_ROADMAP.md#w02) a
[inventuru W01](../baseline/README.md). Konkrétní verze a ověřené zdroje jsou v
[záznamu výběru](platform-selection.json). Ten není výsledkem Windows buildu
ani náhradou budoucích zámků všech tranzitivních balíčků.

## Co má dostat bratr

Hotovou instalovatelnou aplikaci. Nemusí mít vývojové prostředí, nic sestavovat,
číst zdrojové kódy ani absolvovat průběžné testy nehotových částí. První plánované
předání zůstává až po W43; M-Track Solo se s kytarou otestuje ve W44.

Jde o osobní projekt pro jednoho příjemce. Microsoft Store, veřejné vydání,
automatický aktualizační server a nákup podpisového certifikátu nejsou součástí
první verze. Aktualizace se budou předávat ručně, se zálohou dat a možností návratu.
Účet a síť nejsou potřeba pro základní cvičení; AI a YouTube zůstávají volitelné
online funkce s vlastními podmínkami služeb.

## Zvolená licence a samostatně dostupné zdroje

Ověřený [licenční soubor JUCE](evidence/juce-license.txt) nabízí AGPLv3 nebo
komerční licenci. Bezplatné předání příbuznému samo o sobě neruší podmínky knihovny.

| Potřeba | Konkrétní způsob předání | Stav |
| --- | --- | --- |
| Bratr instaluje jen hotovou aplikaci; kód nemusí stahovat | Soukromé místo ke stažení nabídne instalátor a vedle něj odpovídající zdrojový archiv zdarma. AGPLv3 §6(d) výslovně nevyžaduje společné stažení. | Vybráno a potvrzeno uživatelem. |
| Příjemci nebude zpřístupněn ani zdrojový archiv | Vyžadovalo by jinou licenci JUCE a ASIO, nebo jiné audio řešení bez příslušného copyleftu. | Není požadováno ani vybráno; cenu a způsobilost komerční varianty jsme neověřili. |

První varianta nevyžaduje zveřejnit projekt celému internetu. Přístup ke zdrojům
musí být pro příjemce skutečný a srovnatelný s přístupem k instalátoru; odkaz na
nedostupný repozitář nestačí. Příjemci zůstávají práva daná licencí, včetně dalšího
šíření za jejích podmínek. „Jen pro bratra“ vymezuje zamýšlené předání, ne zákaz
vykonávat tato práva.

Pro vybranou variantu plánujeme síťové stažení podle §6(d), nikoli pouhý příslib
„zdroje někdy na vyžádání“. Archiv odpovídá přesně vydanému instalátoru, obsahuje
potřebné zdroje, úpravy, rozhraní a skripty sestavení/instalace v rozsahu licence;
neobsahuje osobní data, API klíče ani soukromé podpisové klíče. Při jiném způsobu
předání, například USB, se zvolí odpovídající postup §6. Texty
[AGPLv3](evidence/AGPL-3.0-only.txt) a [GPLv3](evidence/GPL-3.0-only.txt) jsou
uloženy jako podklady, ne jako nová licence původní PWA.

Repozitář má nadále [MIT licenci](../../../LICENSE). Původní vlastní soubory
si ponechají jejich oznámení. Ve vybrané AGPL cestě výsledná kombinovaná
desktopová aplikace musí splnit její podmínky; samotné oddělení JUCE do DLL
automaticky neomezuje povinnosti jen na tuto DLL. Konkrétní licenční oznámení
nové desktopové části se přidá při jejím založení před začleněním závislosti.

## ASIO je samostatná součást rozhodnutí

JUCE 9.0.3 obsahuje hlavičky ASIO SDK 2.3. Jejich
[licence](evidence/asio-license.txt) nabízí vlastní Steinberg podmínky nebo
GPLv3. Pro vybranou variantu AGPL/GPL lze použít přibalené hlavičky; kombinování GPLv3
a AGPLv3 upravuje §13. Externí SDK pro tuto cestu není potřeba.

Pokud se vybere uzavřená distribuce, nelze pouze zaplatit JUCE a ponechat ASIO
pod GPL. Je potřeba samostatně ověřit a splnit zvolenou necopyleftovou licenci
ASIO, případně zvolit WASAPI. Zaškrtnutí podpory ASIO v buildu licenci neřeší.
Ovladač M-Audio je jiný produkt než SDK: nebudeme ho přibalovat, jeho získání
a podporu ověříme u výrobce. Dostupné buffery a skutečná odezva zůstávají neověřené.

Náhradní možnost k posouzení při odmítnutí JUCE: PortAudio pro zařízení,
vlastní DSP a vhodné samostatné knihovny. PortAudio má permisivní licenci,
ale nenahrazuje celé JUCE DSP a neodstraňuje podmínky ASIO SDK. Není zatím
vybrané ani ověřené náhradní jádro. Změnu je nutné rozhodnout před W03.

## Technický základ pro první ověření

| Součást | Výběr pro W03/W04 | Co ještě ověřit |
| --- | --- | --- |
| Systém | Windows 11 x64; výchozí minimum 24H2 | Bratrův systém dosud neznáme. Kompatibilitu potvrdit před cíleným instalačním balíkem; při starším systému rozhodnutí upravit podle podpory, ne podle domněnky. |
| UI | C#, WinUI 3, Windows App SDK 2.5.1 | Skutečné sestavení, ovládání, DPI a životnost okna ve W03. Verze metabalíčku není číslem generace WinUI. |
| Spravované prostředí | .NET 10 LTS, SDK 10.0.401, runtime 10.0.12 | SDK je vydané; kombinace s vybraným WinUI dosud není tímto projektem otestována. |
| C++ | C++20, MSVC z Visual Studia 18.10.0, CMake 3.31.10 | Na Windows uložit přesný seznam komponent a verzi kompilátoru; žádný místní MSVC build zatím neproběhl. |
| Windows SDK | Řada 10.0.26100, balíčky C++ a x64 10.0.26100.9169 | Rozlišit verzi balíčku od cílové API verze 10.0.26100.0; ověřit integraci se sestavením. |
| Audio/DSP | JUCE 9.0.3, C++ DLL a verzované C ABI | První běžící technický řez je W03; licence vybrána výše. |
| Noty | AlphaTab .NET 1.8.3 a AlphaSkia 3.4.135 | Nativní vykreslení, výběr taktů, přehrávání a potřebné x64 binárky prokáže W04. |
| Zpomalení | Kandidát Signalsmith Stretch 1.4.0 + Linear 0.6.4 | Kvalita v celém požadovaném rozsahu, latence a paměť ve W04; nejde o hotovou integraci. |
| YouTube | WebView2 SDK 1.0.3719.77 a Evergreen runtime | SDK odpovídá závislosti zvoleného WinUI. Runtime se aktualizuje samostatně; testy zaznamenají jeho skutečnou verzi. |

Verze Visual Studia je doložena v metadatech vybraného .NET SDK. Je to výběr
nástrojové řady, nikoli tvrzení, že je již reprodukovatelně nainstalován konkrétní
MSVC. Přesný obraz Windows runneru, komponenty a hashe doplní W03/W05.
Použití IDE/build nástrojů se řídí jejich vlastními podmínkami; nepředávají se
jako součást aplikace. Žádná komerční licence nebyla objednána.

Linuxové prostředí ověřilo podklady a současnou PWA. Nenahrazuje Windows runner,
nepotvrzuje nativní UI ani podporu zvukovky. Běžný Windows 10 zde není slíbený
podporovaný cíl; podpora frameworku a aktuální podpora .NET nejsou totéž.

## Závislosti, obsah a oznámení

| Oblast | Podmínky a zvolený postup |
| --- | --- |
| JUCE moduly a jejich závislosti | Vybrána AGPLv3. Z vydaného JUCE SBOM vybrat skutečně linkované komponenty a zachovat jejich licence. VST/AAX hosting není součást zadání. |
| AlphaTab | Balíček uvádí MPL-2.0. Uchovat oznámení a dostupnost příslušných zdrojů i změn; ověřit kompatibilitu konkrétního zdrojového vydání a jeho tranzitivních závislostí ve W04. |
| AlphaSkia / Skia | AlphaSkia balíček uvádí BSD-3-Clause. Převzít i oznámení skutečně distribuované Skia binárky a jejích komponent. Nezaměňovat automaticky za SkiaSharp. |
| Signalsmith | Stretch i Linear mají MIT. Zachovat oznámení; další FFT backend se bez nové kontroly nepřidává. |
| Staré WebM nahrávky | Kandidát libwebm 1.0.0.31 (BSD-3-Clause + patentový grant) a Opus. JUCE má Opus podporu, ale z toho neplyne automatické dekódování WebM kontejneru. W04 ověří skutečné vzorky z W01 a uzamkne výslednou integraci. |
| MP4/AAC a ostatní média | Pro MP4/AAC prověřit systémový Media Foundation dekodér včetně systému bez volitelných mediálních součástí. WAV/MP3/Ogg prověřit na skutečných JUCE formátech. FFmpeg z tvorby referenčních souborů není vybraná distribuovaná závislost aplikace. |
| Windows App SDK | Zdrojový repozitář uvádí MIT, binární NuGet má samostatné Microsoft podmínky. Neoznačovat jeho binárky automaticky za MIT ani je nepřelicencovat pod AGPL. Rozlišit systémové knihovny, redistribuovatelné runtimy a aplikační zdroje. |
| WebView2 | Stažený SDK balíček obsahuje permisivní oznámení; runtime má vlastní podmínky. Použít oficiální distribuci a její licence. WebView slouží pouze oficiálnímu YouTube embedu, ne jako náhrada nativního shellu. |
| Fonty a soundfont | Bravura: OFL-1.1. Ověřený SONiVOX soundfont: Apache-2.0, podklad v inventuře W01. Uchovat licenční oznámení; systémové Segoe fonty používat přes Windows, nekopírovat jejich soubory do aplikace. |
| Výukový obsah | Dva převzaté riffy mají v inventuře nevyřešený původ práv. Pro nový balík navrhnout dvě vlastní náhrady se stejným výukovým cílem; při W06 výslovně zkontrolovat vazby a případnou migraci ID. Originální obsah a zdrojová reference se nyní nemění. |

U binárek Microsoftu respektovat pravidla redistribuce a jejich samostatné
licence. Pokud konkrétní finální kombinace nebude vyhovovat všem podmínkám,
nesmí projít přejímkou W43; samotný MIT soubor ve zdrojovém repozitáři dodavatele
není důkaz pro libovolný sestavený balík. Registr v tomto kroku neprohlašuje
ještě neexistující sestavení za licenčně zkontrolované.

## Instalace a aktualizace

Výchozí model pro osobní předání: unpackaged WinUI aplikace, samostatně dodané
potřebné runtimy podle podporovaného způsobu nasazení, jednoduchý instalátor
pro daného uživatele a ruční aktualizace. Konkrétní nástroj pro instalátor se
vybere při W41 podle výsledků W03/W05 a jeho licence. Cíl je spustit aplikaci
bez instalace vývojového prostředí, pokud možno bez administrátorských práv.

MSIX ani placený certifikát nejsou výchozí podmínkou. Nepodepsaný balík může
vyvolat upozornění Windows; neslibujeme jeho nepřítomnost ani nepožadujeme
vypnutí ochrany. Pokud se později ukáže MSIX jako vhodnější, zaznamená se důvod
a ověří konkrétní způsob důvěry k podpisu. Kontrolní součet ověřuje integritu,
sám nenahrazuje důvěryhodný původ balíku.

Ve W41/W42 otestovat čistý podporovaný systém, chybějící runtimy, start offline,
aktualizaci, návrat a odinstalaci se zachováním uživatelských projektů.
Instalátor ani zdroje se v tomto kroku nikam neposílají a veřejně nepublikují.

## Podmínky dokončení W02 a navazující kroky

- Ověřeny vydané verze a licenční podklady; výběr je zaznamenán odděleně od důkazu funkčnosti.
- Soukromé předání, hotová aplikace a ruční aktualizace odpovídají zadání.
- Uživatel potvrdil samostatnou dostupnost zdrojů, které se nebudou přibalovat k instalátoru. AGPL/GPL cesta je tím zvolená; nákup licence není pro tuto variantu potřeba.
- Při založení desktopové části uchovat odpovídající oznámení a licence. Změna se nesmí vydávat za přelicencování referenční PWA.
- Další implementace je W03, nikoli automaticky celý přepis.
- Bratrova verze Windows, finální ovladač, nativní build, renderer, kodeky, skutečný distribuovaný soupis a měření na zařízení mají své výslovné navazující kontroly.

Webové stránky s aktuální komerční licencí a ceníkem JUCE nebyly z tohoto
prostředí přístupné. Ověřená existence duální licence se opírá o soubor konkrétního
vydání; nemáme ověřeno, zda a za jakých podmínek by jeho komerční varianta byla
pro tento osobní projekt zdarma. Tuto možnost nelze zaměňovat s ověřenou AGPL cestou.
