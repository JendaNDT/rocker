# W03 — ruční přejímka Windows prototypu

Testovaný balík: `windows-w03-preview-20261011`, sestavená revize
`79fb06bcb535fd457e6620da5ea5265711ef0001`.

## Potvrzeno Jendou 11. 10. 2026

- Okno se otevřelo a testovací tón byl slyšitelný.
- Zastavení, opětovné spuštění a zavření během přehrávání zastavily zvuk.
- Jenda následně potvrdil všechny tři vyžádané kontroly: Tab/Enter, 20
  spuštění a zastavení se zvukem v obou sluchátkách a nové spuštění po zavření
  během přehrávání. Přístupové klávesy a Správce úloh nebyly součástí tohoto
  krátkého zadání; jejich ověření z potvrzení neodvozujeme.
- Smart App Control původně blokoval `rocker_audio.dll` chybou `0x800711C7`.
  Uživatel vypnul ochranu a následně potvrdil přehrávání. Nejde o ověření
  podepsaného balíku se zapnutou ochranou.

Potvrzení pochází z chatu, nikoli z automatického měření. Zařízení, přesná
verze Windows, buffer a vzorkování nebyly zaznamenány. M-Track Solo ani
latenci z tohoto testu nelze odvodit.

## Zbývající průchod na stejném balíku

Začít nízkou hlasitostí. U každého scénáře zaznamenat výsledek nebo přesnou
chybu; neprovedený scénář není úspěšný test.

1. **Klávesnice:** po spuštění je ticho. Tab přesouvá fokus na tlačítka;
   Enter nebo mezerník aktivuje vybrané tlačítko. Přístupové klávesy po Alt
   nabízejí P pro spuštění a Z pro zastavení.
2. **Opakování a stereo:** dvacetkrát spustit a zastavit tón. Pokaždé musí
   nastat ticho bez chyby; ověřit slyšitelný tón v obou sluchátkách.
3. **Nové spuštění:** zavřít během tónu a znovu otevřít aplikaci. Nové okno
   začíná bez zvuku a dovolí další spuštění a zastavení. Ve Správci úloh po
   zavření nezůstává proces Rocker.Probe.exe. Samotné ticho nedokazuje
   ukončení všech vláken ani uvolnění zařízení.
4. **USB zařízení, pouze pokud je právě výstupem:** odpojit během tónu.
   Okno nesmí spadnout; zkusit zastavit, připojit a znovu spustit.
   Pokud USB výstup není k dispozici, zaznamenat neprovedeno. Ověřit, zda
   Windows nepřesměrovaly výchozí výstup na jiné zařízení.
5. **Chybějící DLL:** v samostatné kopii rozbaleného balíku, při zavřené
   aplikaci, přejmenovat rocker_audio.dll na rocker_audio.dll.disabled.
   Spuštění má ukázat chybu bez pádu a deaktivovat spuštění tónu.
   Zavřít, vrátit původní název a ověřit další běžné spuštění.

Nesoulad ABI a simulované chyby zařízení již mají samostatné automatické
kontroly v CI. Testovací DLL se do uživatelského balíku nepřidávají.

## Evidence k doplnění

| Položka | Stav |
| --- | --- |
| Tab a Enter | Potvrzeno uživatelem |
| Přístupové klávesy Alt/P/Z | Neověřeno |
| 20 opakování a oba sluchátkové výstupy | Potvrzeno uživatelem |
| Nové spuštění po zavření během tónu | Potvrzeno uživatelem |
| Kontrola nezůstávajícího procesu ve Správci úloh | Neověřeno |
| Odpojení a obnova skutečného zařízení | Neprovedeno — uživatel test výslovně odmítl 11. 10. 2026 |
| Chybějící DLL a následná obnova | Neověřeno |
| Verze Windows, výstupní zařízení, vzorkování, buffer | Nezaznamenáno |

W03 je uzavřený v rozsahu technického řezu podle kritéria roadmapy.
Odpojení zařízení uživatel výslovně odmítl testovat; další neprovedené scénáře
v tabulce zůstávají neověřené a nejsou automaticky označené jako úspěšné. Bratrův kytarový test, ASIO a skutečná celková odezva patří do
pozdějších kroků roadmapy.
