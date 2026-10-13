# Přestavba Rockeru na desktopové pracovní prostředí

Hlavní zadání je [WINDOWS_DESKTOP_REBUILD_PLAN.md](../../../WINDOWS_DESKTOP_REBUILD_PLAN.md).
Obsahuje 36 navazujících kroků, 7 milníků, převod všech 32 současných funkcí
a 22 povinných desktopových přejímacích scénářů.

Stav k 13. 10. 2026: realizace D01–D36 schválená; [D01 dokončený](baseline.md),
včetně [výchozích automatických kontrol](baseline-validation.json). Navazuje D02.
Poslední sestavený kandidát zůstává RC6. Nové samostatné Live, plovoucí
nástroje, dokumentová okna a galerie jejich návrhů mají vzniknout podle
plánu. Tato složka zatím neobsahuje hotový víceokenní program ani nové
vizuální návrhy.

Při implementaci sem ukládat desktopovou inventuru, rozhodnutí o hostování,
specifikaci vzhledu, společnou galerii návrhů, migrační postup a čerstvé
automatické důkazy. Skutečné screenshoty hotového Windows runtime odlišit
od návrhů a označit použitou revizí, Windows během a případným umělým signálem.

[plan-validation.json](plan-validation.json) je kontrola úplnosti a místních
odkazů tohoto dokumentu. Neověřuje Windows chování, grafiku ani zvuk.
