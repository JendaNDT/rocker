# W12 — Převod PWA včetně nahrávek

Webová část má samostatné tlačítko „Exportovat pro Windows včetně nahrávek“.
Čte všech 11 skutečných localStorage skupin a validní IndexedDB nahrávky;
netransformuje zvuk ani neodstraňuje původní data. Migrační archiv v1 obsahuje
starou zálohu nastavení, původní audio bajty, ID, MIME, délku, čas vytvoření,
markery a SHA-256/velikost každé přílohy. AI klíč a strukturovaná tajná pole se
neexportují; známý klíč se odstraní i z textového obsahu. ZIP se vytváří bez
nové knihovny; limit webového exportu je 256 MiB, export může vyžadovat odpovídající
volnou paměť. Nejde o nový záznam ani o převod ztrátového zvuku do WAV.

Tato úprava je na pracovní větvi. Produkční `main` ani veřejný web nebyly
nasazeny. Pro závěrečný uživatelský průchod W37/W43 je potřeba export zpřístupnit
v původním webovém originu a profilu; offline HTML z jiného originu nemůže číst
jeho localStorage/IndexedDB. Stávající JSON zálohu lze importovat už bez této
webové úpravy, ale neobsahuje nahrávky. Nasazení webového exportu není zde
vydávané za provedené ani obcházené ručním čtením jiného profilu.

Windows import má systémový picker, náhled počtů/konfliktů, výchozí sloučení
se zachováním existujících ID a volitelné nahrazení. Převod nejprve vytvoří
validovaný izolovaný profil a nativní desktopovou zálohu, potom použije stejnou
obnovitelnou cestu jako W11. Původní soubor ani webová data se nepřepisují.
Workspace se po zrušení nebo dokončení uklidí. Neplatná skupina či položka se
vypíše v reportu; checksum chybných použitých médií zastaví převod.

Překlady dat:

- Barevný režim auto/dark/light → systémový/tmavý/světlý Vintage. Webové
  mobilní rozložení, materiál Studio/Glassy a vibrace nahrazuje nativní chování.
- Profil, levá ruka, názvy tónů, Zen, ladění, brána, reference a dokončené lekce
  se zachovávají. Původní lekce nemají uložený krok; index se nevymýšlí.
- Pokrok zachovává dny/minuty, BPM a odměny; dvě stará riffová ID se přeloží
  na vlastní náhradní cvičení podle runtime katalogu.
- Aparát zachovává i vybrané ID presetu, uživatelské presety své původní ID.
- Metronom zachovává konfiguraci, vlastní presety, zvuky subdivizí a volbu
  teček/kyvadla. Vlastní ladění a video smyčky zachovávají ID a hodnoty.
- AI historie zachovává pořadí a role; staré zprávy nemají datum, nové datum
  import nepřidává. Klíč ze staré v1/v2 JSON zálohy se drží samostatně a jeho
  převzetí do DPAPI je výslovná volba, výchozí vypnutá. SQLite, projekty a
  mezilehlý nativní archiv jej neobsahují. Selhání klíče po úspěšném převodu dat
  je samostatný výsledek, nedává se do stejného hlášení jako selhání dat.
- Nahrávky zachovávají skutečné původní formáty WAV, WebM/Opus, Ogg/Opus,
  M4A/AAC a MP3, timestamp, MIME a značky. Média se deduplikují podle SHA-256,
  opakované sloučení stejné ID nepřepisuje. Přehrávání formátů ověřuje W04;
  zde se kontroluje bezeztrátové přenesení původních bajtů, nikoli poslech.

## Ověření

[Referenční archiv](fixtures/reference.rockermigration) skutečně vytvořil
aktuální PWA exporter z nezměněných umělých W01 dat. Skript
`node scripts/export-windows-migration-fixture.mjs --check` jej reprodukuje
bajt po bajtu. Windows používá tentýž skript a .NET ZIP reader, ne alternativní
ručně vyrobený archiv. Obsahuje všech 11 skupin a 5 starých zvukových formátů.

6 Vitest scénářů ověřuje CRC-32, audio/metadatovou identitu, SHA-256, redakci
klíče včetně JSON znaků, duplicity, traversal a odmítnutí velkého vstupu před
alokací zvuku. Celá PWA reference: 567/567 testů, typy a lint.
Native kontroly ověřují v1/v2, částečnou/poškozenou/cizí zálohu, každý migrovaný
formát a původní čas/marker, deduplikaci, starý klíč a cleanup. Linux prošel 28
kontrolami migrace; DPAPI převzetí se skutečně provede až ve Windows CI.
Ruční systémové pickery, nasazený původní origin a poslech na M-Track Solo
zůstávají závěrečnou přejímkou, nejsou zde označené jako provedené.
