# Vintage — dokončení grafiky Windows aplikace

Zadání Jendy: propracovaný vzhled celého nativního programu. Navazuje na rozložení v [UI/UX](../ux/README.md).

- Klidnější uhlové pozadí, měděné akcenty, jemně odstupňované povrchy a sjednocené nadpisy. Barvy všech přístrojů mají varianty pro světlý a systémový kontrastní režim.
- Vlastní vektorová značka Rockeru a vícevelikostní ikona v okně, spustitelném souboru a zástupcích. Značka je vytvořená pro projekt, bez cizího obrazového obsahu.
- Ovladače aparátu: kovový knoflík, obvodové značky a ukazatel skutečné hodnoty. Při změně šířky okna 1–3 sloupce. Přesný NumberBox, Slider, klávesnice a automatizační jména zůstávají funkční.
- Live i samostatná ladička: společná stupnice −50 až +50 centů, zvýrazněný střed a ukazatel jen při platném aktuálním měření. Prázdný vstup nesmí vypadat jako naladěná kytara.
- Měřáky: segmenty na stupnici −60 až 0 dBFS, barevná pásma, číselný údaj a nativní přístupný ukazatel. Segmenty zobrazují skutečný signál; žádná ozdobná simulace.
- Metronom: velký skutečný údaj BPM na Vintage přístrojové ploše. Akordy: čitelná čísla prstů a strun, nultý pražec; hmatník: struny, pražce a zaoblená tlačítka s ponechanou orientací pro leváky.

Ověření: C# s reálnými WinUI referencemi, doménové reference, Windows kompilace a běžné kontroly celé aplikace. Vedle všech 32 standardních snímků pořizuje runner devět detailních snímků při zavřeném souborovém přehrávači, v tmavé i světlé variantě (`visual-finish.json`). Skutečné snímky a revize se uloží po úspěšném sestavení.

Přijetí vzhledu Jendou, fyzické DPI, Narrator a systémový vysoký kontrast zůstávají ručními kontrolami; automatická konstrukce není jejich náhradou. Zvuková cesta, formáty uživatelských dat a funkce nejsou touto grafickou úpravou změněné.
