# Přestavba Rockeru na desktopové pracovní prostředí

Hlavní zadání je [WINDOWS_DESKTOP_REBUILD_PLAN.md](../../../WINDOWS_DESKTOP_REBUILD_PLAN.md).
Obsahuje 36 navazujících kroků, 7 milníků, převod všech 32 současných funkcí
a 22 povinných desktopových přejímacích scénářů.

Stav k 13. 10. 2026: realizace D01–D36 schválená; [D01 dokončený](baseline.md),
včetně [výchozích automatických kontrol](baseline-validation.json).
[D02 — galerie všech 32 návrhů](design-gallery.html) a
[specifikace](design-spec.md) jsou připravené; [prohlížečová kontrola](design-validation.json)
ověřila 64 kombinací pohledů a vzhledů, filtry, poslechové stavy a mobilní prohlížení.
Navazuje D03 — skutečný Windows experiment.
Poslední sestavený kandidát zůstává RC6. Nové samostatné Live, plovoucí
nástroje a dokumentová okna mají vzniknout podle plánu.
Galerie obsahuje návrhy s ukázkovými daty, tato složka zatím neobsahuje
hotový víceokenní program.

Při implementaci sem ukládat desktopovou inventuru, rozhodnutí o hostování,
specifikaci vzhledu, společnou galerii návrhů, migrační postup a čerstvé
automatické důkazy. Skutečné screenshoty hotového Windows runtime odlišit
od návrhů a označit použitou revizí, Windows během a případným umělým signálem.

[plan-validation.json](plan-validation.json) je kontrola úplnosti a místních
odkazů tohoto dokumentu. Neověřuje Windows chování, grafiku ani zvuk.
