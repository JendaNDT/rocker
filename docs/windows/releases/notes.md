# Rocker 0.2.0 RC4 — pohodlnější hraní a nové nástroje

Sestavená nativní Windows aplikace s Vintage vzhledem, všemi 32 pracovními pohledy a společným C++/JUCE zvukovým jádrem.

## Nově

- **Jen hraju (F11 / Esc):** větší ladička a hlavní akce, skrytá navigace a podrobnosti. Běžné odměny a oznámení ustoupí hraní; zvukové chyby zůstávají viditelné.
- **Sestavy cvičení:** uložený pohled/úloha, aparát, ladění, metronom a případně skladba s rychlostí, pozicí a A/B. Otevření připraví nastavení a nechá zvuk zastavený; monitoring ani přehrávání se samy nezapnou. Přípravu lze zrušit.
- **Společné hledání a oblíbené (Ctrl+K):** obrazovky, konkrétní lekce, akordy, stupnice, riffy, trenažéry, vlastní skladby, nahrávky i sestavy; české názvy bez diakritiky. Oblíbené a sestavy jsou součástí úplných záloh.
- **Zvuková vlna nahrávek:** skutečný dekódovaný průběh, výběr úseku myší nebo poli A/B, opakování a uložené značky. Předběžné otevření bez přehrávání a ukazatel pozice původního zdroje i při zpomalení.
- **Aktualizace:** kontrola soukromých Windows vydání, stažení a ověření instalátoru podle SHA-256. Instalace se spustí až po potvrzení a uložení práce. Přímá kontrola/stažení vyžaduje vlastní GitHub token s Contents: read pro tento repozitář, chráněný DPAPI; bez něj použij přihlášený systémový prohlížeč.
- Sjednocené barvy nových dialogů a zvukové vlny, nativní přechody a kompaktní akce u nadpisu. Tmavý i světlý Vintage.

## Spuštění

Stáhni **Rocker-Windows-Setup-0.2.0-x64.exe**, zavři předchozí Rocker a spusť instalátor. Vývojové nástroje ani sestavování nejsou potřeba. Přenosná aplikace je v Rocker-Windows-x64.zip; odpovídající zdroje jsou zvlášť v Rocker-Windows-sources.zip a ke spuštění je nepotřebuješ.

Vyžaduje Windows 11 x64 od 24H2. M-Audio ovladač dodává výrobce; pro kytaru i sluchátka použij stejný interfejs a jediný mono vstup. YouTube potřebuje internet a WebView2. Volitelná AI potřebuje vlastní OpenAI klíč; žádné nahrávky neposílá.

## Ověření a hranice

Automatické Windows kontroly zahrnují 18 nových kontrol skutečného okna, všechny původní nativní/doménové/datové kontroly, 32 pracovních pohledů, UI Automation a instalační cyklus se zachováním dat. Kontrolní profil a zvukové vzorky jsou syntetické; nejde o skutečný poslech kytary.

Skutečná odezva a stabilita M-Track Solo, online YouTube/OpenAI, fyzická přístupnost a přihlášení/spuštění aktualizace na uživatelově Windows zůstávají závěrečnou ruční přejímkou. Balík není podepsaný certifikátem; potvrzená kompatibilita s aktivním Smart App Control není součástí automatických výsledků. Data zůstávají při aktualizaci i odinstalaci zachovaná.
