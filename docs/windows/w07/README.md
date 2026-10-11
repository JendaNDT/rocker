# W07 — Nativní hlavní okno a navigace

WinUI `NavigationView` seskupuje všech 32 pracovních pohledů. Menu, fokus,
listy, textová pole, tlačítka, systémový titulek a souborové dialogy jsou
skutečné Windows komponenty. Native notový renderer a WebView2 jsou zachované
ve vlastních panelech; YouTube se inicializuje až při otevření svého pohledu.
WebView2 nepřebírá vykreslení ostatních obrazovek.

Společný C++ host vzniká jednou pro celé okno. Transport je stále dostupný;
přepnutí pohledu nemění načtené PCM/MIDI ani stav zvuku. Historie vrací
konkrétní úlohu včetně ID lekce; nový cíl zahodí historii vpřed. Dynamické
WinUI stránky mají vlastní cache, vyhledávání se zachovává a krok lekce je
v odděleném doménovém session stavu. Dokončení lekce je zatím jen pro aktuální
spuštění; trvalé ukládání je další fáze. Denní plán vede přímo do cíle.

Okno ukládá vlastní normální obdélník, maximalizaci a režim vzhledu. Při obnově
se obdélník ve fyzických pixelech přizpůsobí skutečné pracovní ploše nejbližšího
monitoru, včetně záporných souřadnic nebo odpojeného druhého monitoru. Vadné
nastavení nezastaví start. Zavření čeká na zápis geometrie a potom uvolní zvuk.
Budoucí projekt/nahrávání registruje skutečný close guard: uložit, zahodit nebo
zrušit; nahrávání ani neuložený dokument se pro demonstraci nepředstírají.

Při užším okně přejde nativní navigace do kompaktního režimu, tlačítkové lišty
se skládají svisle a transport může rolovat. Téma lze měnit v menu i Nastavení;
systémový vysoký kontrast používá systémové barvy. Společné Vintage karty
používají systémové fonty a theme resources. Kompletní katalog komponent,
přístroje, všechny DPI/přístupnostní scénáře a finalizace designu jsou W08/W37.

## Funkční rozsah

Přehled, parametry navigace, textový průchod lekcí, katalogy/hledání, vzhled,
obnova okna, noty, přehrávání média a lokální video bridge mají implementaci.
Zbývající stránky otevřeně uvádějí svůj nedokončený stav. Otevření 32 pohledů
neznamená hotový Live, ladičku, nahrávání, Studio, Looper nebo AI tutor.

CI používá izolovanou datovou složku. Publikovaná aplikace při diagnostice
skutečně vytvoří/přepne všech 32 pohledů, ověří návrat ke konkrétní lekci a
počet vytvořených audio hostů; výstup je `shell-navigation.json`. Následně
smoke vyžaduje skutečný notový renderer, lokální WebView2 bridge a běžné
ukončení procesu. Neprohlašuje tím fyzické ovládání myší/klávesnicí nebo zvuk
za ručně ověřený. Windows sestavení tohoto navazujícího řezu právě probíhá.
