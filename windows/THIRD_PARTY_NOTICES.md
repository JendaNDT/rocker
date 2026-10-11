# Licence technického prototypu

Nové vlastní soubory v této složce jsou pod AGPL-3.0-only; viz [LICENSE](LICENSE).
Původní PWA a její kořenová MIT licence se nemění.

- JUCE 9.0.3: vybrána AGPLv3, původní copyright a licence ve zdrojovém vydání.
- ASIO hlavičky přibalené k JUCE: vybrána GPLv3; nepřebírá se licence zvukového ovladače M-Audio.
- JUCE obsahuje další komponenty s vlastními oznámeními; referencí je SPDX soupis tohoto vydání.
- .NET a Windows App SDK mají vlastní licence a podmínky distribuovaných součástí.
- AlphaTab .NET 1.8.3 (MPL-2.0), AlphaSkia 3.4.135 a jeho Windows knihovna
  (BSD-3-Clause, oznámení Skia a HarfBuzz); používají se nezměněné NuGet balíčky.
  Přesné zdroje AlphaTab a AlphaSkia jsou v samostatném zdrojovém balíku.
- Bravura zabudovaná do AlphaTab a Noto Sans zabudované do Rocker.Notation
  mají SIL Open Font License; úplné texty jsou přibalené v `licenses`.

Přesné podklady jsou v rozhodnutí W02 v repozitáři. Toto je interní prototyp,
nikoli hotový distribuční balík pro bratra. Předběžný spustitelný ZIP uchovává
licenční podklady ve složce `licenses`; odpovídající zdroje Rockeru a celého
připnutého JUCE jsou nabídnuté zvlášť v `Rocker-Windows-sources.zip` u stejného
vydání. Identifikaci sestavení obsahuje `build-info.json`.
Před předáním bratrovi ve W43 musí vzniknout
úplný soupis skutečně přibalených komponent a jejich licenčních textů, odpovídající
zdrojový archiv a dostupný samostatný odkaz vedle instalátoru. Pouhý tento stručný
soubor finální licenční povinnosti nenahrazuje.
