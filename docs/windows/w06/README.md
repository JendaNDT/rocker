# W06 — Obsah a čistá C# doména

`windows/content/catalog.json` převádí všech 15 původních skupin bez ručního
přepisování. Verze a SHA256 kanonických LF zdrojů chrání před neúmyslnou změnou;
W01 reference zůstává zmrazená. Loader kontroluje ID, duplicity, lekční vazby,
šestistrunné ladění, akordy, intervaly, rytmické sekvence, riffové noty a presety.

Doména nyní obsahuje hudební výpočty, tempo, swing, metronomové konfigurace,
aparátové konfigurace, hmatník, stupnice, power chordy, denní plán, dokončení
lekcí, Surgical, Power Chord/Gallop, palm-mute kalibraci a drill, pokrok,
odměny a přestávky. FFT, detekce vstupu a časování zvuku zůstávají v C++;
portované C# kalibrační pravidlo neznamená, že už funguje živé měření.

`LessonSession` zachovává původní průchod kroky včetně návratu a dokončení.
Potvrzení tónu používá pitch class a neblokuje Další, stejně jako původní
`LessonPlayer`/`NoteCheck`. Nově lze předat uloženou pozici; vlastní ukládání
navazuje v datové vrstvě. Chord krok ukazuje skutečnou úroveň vstupu;
neslibuje rozpoznání celého akordu monofonním detektorem.

## Kontroly vůči původnímu kódu

- 69/69 zmrazených doménových případů W01.
- 37/37 zmrazených stavů přesného tónového trenažéru.
- 372/372 dalších případů získaných spuštěním skutečných TS funkcí.
- 350/350 stavů deseti rytmických, čtyř palm-mute a jedné přestávkové sekvence.
- 66 dalších kontraktů celkem, včetně 37 Surgical stavů: poškozená data,
  neplatné vazby, nečíselné vstupy, nezměněný původní stav a lekční navigace.

Zlatá data W06 jsou zvlášť v `domain-reference.json`; původní W01 se nepřepisuje.
`export-windows-domain-reference.mjs --check` znovu provede původní TS a
porovná celý JSON. Windows `--check-source` ověří zdrojové hashe bez potřeby
instalovat PWA; C# konzolové kontroly provádějí stejná data v nové doméně.
Čísla tolerují pouze numerickou odchylku do 1e-9 relativně; řetězce, pořadí,
null a všechny vlastnosti musí odpovídat přesně.

Záměrné opravy neplatných vstupů: NaN/nekonečné centroidy se nepoužijí pro
kalibraci; prázdné sekvence a neplatná ladění/ID jsou odmítnuté před startem;
neplatné datum nezhodí historii. Pro platné původní vstupy se chování zachovává.

Převod persistence všech 11 stores je W09–W12. Doménové části specifické pro
Studio/Looper/AI a konkrétní pracovní UI navazují při příslušných funkcích;
nevytvářejí se zde prázdné náhražky. Dva převzaté riffy zatím slouží jako
reference, jejich zařazení do distribučního obsahu je stále bod W02/W43.

```sh
node scripts/export-windows-content.mjs --check
node scripts/export-windows-domain-reference.mjs --check
dotnet run --project windows/Rocker.Domain.Checks -c Release -- \
  docs/windows/baseline/fixtures/domain-cases.json windows/content/catalog.json
```

Linux kontroly prošly. Windows ověření této celé navazující domény se provádí
v následném běhu; předchozí Windows protokol W05 potvrzuje pouze tehdejší řez.
