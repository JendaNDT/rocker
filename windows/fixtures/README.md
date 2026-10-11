# Podklady notového prototypu

- `notation-scale.musicxml`: vlastní čtyřtónová ukázka z W01, kopie souboru
  `docs/windows/baseline/fixtures/notation-scale.musicxml` beze změny.
- `notation-phrases.gp`: vlastní dvoutaktová technická ukázka, vytvořená
  `Gp7Exporter` v AlphaTab 1.8.3 z tohoto AlphaTex vstupu:

```text
\title "Rocker - dve cvicne fraze" \tempo 90 \instrument 25 \tuning E4 B3 G3 D3 A2 E2 . 0.6.4 2.6.4 3.6.4 0.5.4 | 2.5.4 3.5.4 0.4.4 2.4.4
```

Nejde o převzatou komerční skladbu. Oba vytvořené podklady jsou pod kořenovou
MIT licencí projektu. Exportní nástroj se do aplikace nepřidává jako jiná
závislost — používá stejný již zvolený AlphaTab.

W29 adds original four-note GP3/GP4/GP5 binaries, a BCFS GPX container with
the same original GPIF phrases, compressed MusicXML and a three-track MusicXML
score with two guitars, percussion, repeat traversal, a bend and tempo changes.
`scripts/create-windows-notation-fixtures.py` reproducibly generates the legacy
binaries/GPX/MXL. Their format bytes follow AlphaTab's pinned importers; they are
not renamed files or third-party songs. New authored fixtures use AGPL-3.0-only.
The existing GP7 ZIP was repackaged with valid CRC-32 fields; its score XML is
unchanged. No copyrighted artist recordings or external soundfonts are bundled.
