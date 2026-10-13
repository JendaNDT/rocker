# D04 — identity, hostitelé a vlastnictví zdrojů

Tento dokument zachycuje původní D04. Modely jsou nyní zapojené do
produkční pracovní plochy; aktuální stav a navazující Windows důkazy jsou
v [implementaci](implementation.md). Následující přenosné důkazy zůstávají
historickou evidencí konkrétní revize D04.

Implementované čisté modely jsou v
[DesktopWorkspace.cs](../../../windows/Rocker.Domain/DesktopWorkspace.cs).
Neobsahují WinUI, HWND, NativeAudio, timer nebo souborové operace.
[models-validation.json](models-validation.json) zachycuje 72 čerstvých
kontrol plus původní zamrazené doménové případy.

## Implementované hranice

- DesktopRoutes překládá všech 32 starých NavigationTarget. Knihovna nahrávek a konkrétní vybraný záznam jsou odlišné cíle.
- Čtyři nástroje mají stabilní globální identitu. Průvodce a export jsou vlastněné dialogy, ne dokumentové editory.
- DesktopDocumentKey rozlišuje object ID a canonical file ID. Stejný projekt má stejný klíč; různý projekt nebo jiný druh dokumentu má jiný.
- Object ID jsou case-sensitive podle stávajícího katalogu. Canonical Windows file ID používá case-insensitive rovnost a odpovídající hash.
- Souborová identita odmítá relativní/nenormalizovanou cestu, nepovolený typ dokumentu a control characters. Skutečnou validaci typu, cesty, existence a oprávnění dál provádí Infrastructure.
- MoveTo vrací stav se stejným klíčem, dirty a source ownership; změna hostitele neprovádí audio akci.
- CanClose odmítá Cancel, neúspěšné uložení dirty dokumentu, neuvolněný zdroj a zahození aktivního take.
- DecideSwitch vrací rozhodnutí; nikdy nemění zdroj. Rozlišuje stávajícího vlastníka, konflikt capture, konflikt looperu a připravený přechod.
- Layout rozlišuje fyzické souřadnice oken a DIP šířky panelů; záměrně umožňuje záporné souřadnice druhého monitoru.
- Validate odmítá neznámé schéma, neplatné/duplicitní role, dva hostitele jednoho dokumentu, duplicitní nástroj a neplatné rozměry.
- Validace vytváří kopie kolekcí, aby následná mutace vstupní kolekce nezměnila přijaté rozložení.

Owned nahrávka nebo importovaný projekt používá své již uložené object ID.
Otevření jeho souboru musí nejprve vyřešit tuto identitu a aktivovat stejnou
relaci; samotná cesta nesmí vytvořit paralelního editora stejného projektu.
Canonical file klíč slouží externím dokumentům, které žádné owned ID nemají.
Překlad těchto aliasů doplní DocumentManager v D16/D29.

Restored layout je omezený na 12 oken a 64 dokumentů. Konkrétní minimum
každého nástroje a fitting na skutečnou work area doplní window layer v
D10/D15; generická validace nepoužívá původní jednotné minimum 640×480.
Žádná obnova modelu nespouští poslech, přehrávání nebo nahrávání.

## Ověření

[DesktopWorkspaceChecks.cs](../../../windows/Rocker.Domain.Checks/DesktopWorkspaceChecks.cs)
testuje identity/rovnost, přesun, neúspěšné zavření, konflikty zdrojů,
staré adresy, poškozené rozložení a izolaci kolekcí. Běží přes stávající
Domain Checks, s vlastním počtem desktopWorkspaceChecks. Původních 69
zamrazených případů a 184 doplňujících assertionů si zachovalo původní
význam; přidáním desktopových kontrol se tyto počty nepřepsaly.

C# build prošel bez chyb a warnings; lokální semantic kontrola stávající
WinUI aplikace také prošla. Tato evidence je portable doménová kontrola.
Windows důkaz D03 patří starší revizi 11242c9 a není vydáván za sestavení
nových D04 modelů. Nové modely zatím produkční navigaci neřídí.

Navazuje D05: ApplicationServices a registry operací, poté jediný
AudioSession/TransportCoordinator. Až D09/D16 zapojí modely do nového shellu
a dokumentových hostitelů; netvrdit, že samotná definice klíče dokončila
UI nebo per-document ukládání.
