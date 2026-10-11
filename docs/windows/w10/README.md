# W10 — AI klíč v systémovém úložišti

`SecretStore` používá skutečné Windows DPAPI `CurrentUser` přes Microsoft
System.Security.Cryptography.ProtectedData 10.0.12 (MIT). Verze a checksum
jsou v uzamčených NuGet souborech. Při Linux kontrole je výsledek výslovně
Unsupported; žádná náhrada s prostým textem se nevytváří.

Klíč má vlastní `secrets/ai-key.dpapi`, formát v1, oddělenou produktovou entropy,
omezení velikosti a atomické nahrazení. Pro běžnou migraci na jiný Windows účet
se zadává znovu; není to přenosný export. Výsledky čtení Missing/Ready/Unavailable
mají bezpečný `ToString` bez hodnoty. Chyby neobsahují hodnotu klíče. Po odstranění
nezůstává aktivní klíč ani pracovní soubor. Netvrdíme mazání historických sektorů
na disku ani utajení proti aplikaci běžící pod týmž uživatelským účtem.

Nastavení má skutečný PasswordBox, uložení, nahrazení, odstranění a stav.
Po zápisu se pole vyprázdní. Běžné zavření počká na dokončení operace.
Síťový AI požadavek zatím není připojený. Databáze odmítá strukturovaná secret
pole; backup whitelist a import starých klíčů navazují ve W11/W12.

Kontroly: Linux ověřuje 2 unsupported smlouvy. Windows ověří 8 DPAPI scénářů:
chybějící klíč, nepřítomnost canary v prostém souboru, obnovené dešifrování,
zrušenou změnu, nahrazení, odstranění, poškozené úložiště a čistý adresář.
Skutečný Windows výsledek se uloží po dokončení příslušného CI běhu.

Windows [38155352032](https://github.com/JendaNDT/rocker/actions/runs/38155352032)
prošel: 11 souborových, 37 datových a 8 skutečných DPAPI kontrol; dva WinUI
starty obnovily uloženou lekci a vzhled. [Důkaz](windows-validation.json).
