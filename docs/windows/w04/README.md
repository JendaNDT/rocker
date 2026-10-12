# W04 — první technický řez: nativní noty

Aktuální stav W43: navazující mediální transport, zpomalování, společná syntéza not a vyhrazený YouTube pohled jsou implementované. [Úplný kandidát a aktuální Windows důkaz](../w43/README.md). Online přehrávání a skutečný poslech jsou nadále odložené.

## Historický stav prvního prototypu

Stav: import, vykreslení, hit testing a MIDI data prošly Linux i Windows kontrolami; publikované WinUI okno načetlo notový obraz. [Protokol](validation.json). W04 jako
celek není dokončený; dekódování médií, zpomalování, syntéza not a WebView2 /
YouTube zatím nejsou součástí tohoto prototypu.

## Architektura a ověřovaný rozsah

`windows/Rocker.Notation` používá nezměněný .NET AlphaTab 1.8.3 a AlphaSkia
3.4.135. Vykreslí PNG nativní knihovnou Skia, WinUI jej zobrazí jako Image.
Noty nepoužívají HTML, JavaScript ani WebView. Při kliknutí se souřadnice
mapují přes skutečný BoundsLookup na takt, jeho obdélník a MIDI rozsah.
Celá partitura má standardní MIDI data; jejich zápis používá systémový
dialog Windows. Tento krok negeneruje zvuk a nezavádí druhý audio ovladač.

Renderování běží na workeru mimo UI; životnost obrázků a rendereru končí po
vytvoření náhledu. Klikání už používá pouze spravovaná data. Prototyp zobrazuje
první stopu a prvních 12 taktů, vstup omezuje na 4 MB, 16 stop a 128 taktů.
Zip vstupy mají samostatný limit počtu položek a rozbalené délky. Nejde o
kompletní zpevnění parseru pro všechny budoucí formáty.

## Knihovny a licence

| Součást | Verze / připnutá revize | Podklad |
| --- | --- | --- |
| AlphaTab .NET | 1.8.3 / 9d0142eee2b1175dfd36e839ddbd867aed9e454e | [NuGet](evidence/alphatab.nuspec), [MPL-2.0](evidence/alphatab-license.txt) |
| AlphaSkia / Windows native | 3.4.135 / c82e319faeba4155b45664766e9badf528c0fc49 | [NuGet](evidence/alphaskia-native-windows.nuspec), [BSD, Skia, HarfBuzz](evidence/alphaskia-license.txt) |
| Bravura | zabudovaná ve vybraném AlphaTab | [SIL OFL](evidence/bravura-license.txt) |
| Noto Sans | font z připnutých zdrojů AlphaTab, zabudovaný pro přenosné kontroly | [SIL OFL](../../../windows/Rocker.Notation/fonts/OFL.txt) |

Úplné zdroje AlphaTab a AlphaSkia balení nabízí samostatně vedle zdrojů
Rockeru a JUCE. Licenční texty jsou v binárním balíku. Nezměněná původní
PWA licence se nepřepisuje.

## Kontroly

`Rocker.Notation.Checks` importuje vlastní MusicXML a Guitar Pro 7 ukázku,
ověří počet stop/taktů a MIDI not, formát MIDI, PNG dekódované přes nativní
Skia a neprázdný obraz, klikatelnost každého taktu a dostupnost jeho MIDI
časování. Odmítá prázdný, neplatný a nadlimitní vstup. Výsledek a obrázky
zapisuje do reportů, ne do zdrojového repozitáře.

Linux kontrola vyžaduje `AlphaSkia.Native.Linux`; Windows aplikace a kontroly
vyžadují `AlphaSkia.Native.Windows`. Samotný AlphaTab NuGet nativní DLL
neobsahuje. Windows používá systémové fonty; Linux používá FreeType a
registrované Noto Sans. Použití OS fontů v Linuxu způsobilo nativní pád;
podporovaný FreeType backend ho odstranil. Linux výsledek nenahrazuje WinUI.

Windows smoke test nově čeká na dokončení importu, vykreslení a dekódování
obrázku v publikovaném WinUI okně. Žádný zvuk se nespouští automaticky; původní tón
se stále spouští pouze tlačítkem. Pro nový balík musí skutečně projít Windows
build, notové kontroly, UI smoke a balení před nabídnutím ke stažení.

## Navazující části W04

1. Zpřístupnit MIDI události společnému C++ časování a zvolit syntézu.
   Nezávislý AlphaTab/NAudio player se nyní nezavádí.
2. [Offline mediální modul](media.md) a [rozšíření původních nahrávek](legacy-media.md)
   dekódují WAV/MP3/Ogg/Opus/WebM/Opus a na Windows MP4/AAC. Zbývá
   připojení ke společnému transportu a přehrávači. Windows prošel 167/167
   mediálními kontrolami, Linux 138/138 s ASan/UBSan a detekcí úniků.
3. Zachování výšky, délky a PCM limitů zpomalení prochází automatickými
   kontrolami; skutečná kvalita na kytaře a dlouhé nahrávky zůstávají neověřené.
4. Integrovat oficiální YouTube embed jako vymezený WebView2 pohled; ověřit
   skutečné přehrávání a WASAPI souběh. Runtime a výrobní ovladač mají vlastní
   kontrolu. ASIO/M-Track Solo a skutečná odezva patří do hardwarové matice.

Ruční notová přejímka: zobrazení obou ukázek, výběr každého taktu, otevření
vlastního souboru, zrušení systémových dialogů a uložení MIDI. Bez potvrzení
uživatele ji nelze označit za provedenou. Neověřené GP3/4/5/GPX a MXL zůstávají
podporou parseru, nikoli otestovanými formáty Rockeru; důkaz se týká GP7 a
nekomprimovaného MusicXML.

Podle nového pokynu uživatele z 11. 10. 2026 se tato ruční přejímka přesouvá
až k ucelené aplikaci po W43. Další části W04 mohou pokračovat bez čekání
na uživatelské klikání v tomto prototypu; automatické kontroly se nevynechávají.
