# W09 — Ukládání a přenosné projekty

SQLite ukládá 14 verzovaných typů dokumentů: nastavení, profil kytary, pokrok,
pozice lekcí, aparát a presety, metronom a presety, vlastní ladění, video smyčky,
AI nastavení, historii, metadata nahrávek a index projektů. Tovární katalog
zůstává oddělený. Tajná pole jsou odmítána před zápisem; klíč bude samostatně
chráněný ve W10. API klíč se nepřidává do běžných dokumentů.

Databáze používá transakce, WAL a synchronous FULL. Neznámé/future schéma se
odmítne před změnou. Nynější migrace vytváří v1 pouze v nové prázdné databázi;
žádný existující profil se nepřevádí ani nemaže. Budoucí migrace musí nejprve
vytvořit konzistentní zálohu. Kontrola při načtení izoluje neplatné dokumenty,
uvádí varování a původní řádky nepřepisuje. Zápis a čtení běží mimo UI a audio.

Zvukové soubory jsou vlastněné, pojmenované SHA-256, ověřované a neduplikují
se při stejném obsahu. Přerušená kopie se nepublikuje. Projekt má stabilní ID,
`project.rockerproj`, poslední validní `previous.rockerproj` a vlastní `media/`.
Manifest se nahrazuje atomicky; index lze obnovit z manifestů. Chybějící nebo
změněná stopa se hlásí, projekt se nemaže. Relativní cesty umožňují přenést
celou složku projektu. Traversal, absolutní cesty a existující symlinky se
odmítají. Obnova chybného JSON může použít předchozí validní manifest; novější
schema se nesmí přepsat starší aplikací. Netvrdíme odolnost celého souborového
systému vůči výpadku napájení ani ochranu proti souběžné manipulaci cizího procesu.

Autosave slučuje poslední hodnoty dokumentů a před zavřením čeká na zápis.
Neúspěšný zápis drží změny v paměti a zabrání běžnému zavření. Lekce a vzhled
jsou připojené ke skutečnému úložišti; zbytek feature editorů použije stejnou
vrstvu při svém dokončování. Projektový editor patří do W27, backup do W11.

`Rocker.Infrastructure.Checks` ověřuje 11 souborových a 37 datových scénářů:
všech 14 typů po restartu, rollback celé neplatné dávky, zrušení, poslední
autosave, skutečné násilné ukončení vlastního procesu uprostřed otevřené SQLite
transakce, kontrolní součty, deduplikaci, obnovu manifestu, přenos projektu,
chybějící stopu, izolaci vadných řádků a zachování future schémat.
Windows smoke zapisuje lekci i vzhled do izolovaného profilu a znovu otevírá DB.
Výsledky Windows sestavení se doplní až po jeho dokončení.

## Závislosti

Microsoft.Data.Sqlite/Core 10.0.12 (MIT), SQLitePCLRaw 2.1.12 (Apache-2.0 podle
skutečných NuGet nuspec), SQLite engine (public domain podle upstream).
Verze a obsah jsou v uzamčených NuGet souborech. Balení přidává notices skutečně
obnovených balíčků a úplný [Apache text](evidence/Apache-2.0.txt).
Zdroje pro kontrolu: NuGet flatcontainer metadata skutečně obnovených verzí,
https://github.com/ericsink/SQLitePCL.raw a https://sqlite.org/copyright.html.
