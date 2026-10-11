# W04 — Oficiální YouTube ve WebView2

YouTube je jediná dohodnutá webová část nativního Rockeru. WinUI stále vlastní
okno, nabídky, formuláře, soubory i všechny ostatní funkce. Místní wrapper
používá oficiální IFrame Player API; neodstraňuje ovládání ani branding,
nestahuje a neextrahuje zvuk a nezačíná přehrávat automaticky.

Implementované je načtení videa z ověřeného ID/HTTPS odkazu, přehrávání,
pozastavení, zastavení, dostupné rychlosti z API a zobrazení skutečné pozice.
Chyby YouTube 2/5/100/101/150/153 mají srozumitelný stav a ruční otevření
v prohlížeči. A/B úseky a knihovna videí patří do W30.

## Identifikace a hranice

Wrapper je přes `SetVirtualHostNameToFolderMapping` dostupný pod místním
`https://jendandt.rocker/player.html`. Tato doména se pro místní obsah nevolá
na síti. Stálá identita `JendaNDT.Rocker` je v manifestu a AppUserModelID
procesu, bez verze a architektury. Žádosti k oficiálnímu YouTube mají
`Referer: https://jendandt.rocker/`, `origin` odpovídá místnímu wrapperu.
Finální instalátor W41 musí stejnou identitu zaregistrovat u OS; interní
rozbalený prototyp není vydávaný za dokončenou instalaci.

Bridge přijímá pouze omezené typované zprávy z přesné místní stránky. Události
přímo z iframe, neznámé zprávy, neplatná čísla, velké či poškozené JSON se
zahodí. Host objekty a oprávnění mikrofonu/kamery nejsou povolené. Prohlížeč
nenaviguje vrchní stránku na cizí web a nevytváří další okna; uživatel může
výslovně otevřít dané video v systémovém prohlížeči. CSP a standardní TLS
zůstávají zapnuté. Profil WebView2 je v uživatelském LocalAppData.

## Ověření a neprovedené části

- 30 C# kontrol ověřilo odkazy, původ a typy zpráv, neplatné vstupy a chyby.
- 6 Node testů skutečného wrapperu ověřilo nulové síťové požadavky při startu,
  oficiální API, chybové stavy, volbu skutečných rychlostí a opětovné načítání.
  API v těchto kontraktech nahrazuje testovací objekt, nikoli běžící YouTube.
- Windows smoke musí otevřít skutečný WebView2, provést místní bridge a
  zaznamenat skutečnou verzi runtime. Žádné online video nespouští.
- CI při chybějícím runtime používá oficiální Microsoft bootstrapper a
  před spuštěním ověří jeho Authenticode podpis. Neobchází zabezpečení.
- Skutečné online video, dostupnost konkrétního videa a současný poslech
  YouTube/WASAPI/ASIO/M-Track Solo zůstávají neprovedené do závěrečné přejímky.
  Dvě různá audio zařízení ani ASIO multi-client se tímto negarantují.
  Nativní host má ve fázi W04 profil WASAPI shared; YouTube si zachovává
  vlastní systémový zvuk. Finální volba profilů zařízení následuje ve W14/W30.

Tento řez prokazuje nativní integraci a bezpečné ovládání. Online/hardwarové
scénáře jsou podle pokynu uživatele odložené po W43 a nejsou označené za prošlé.
W04 se nesmí prezentovat jako úplná hardwarová přejímka.

## Ověřené zdroje, 11. 10. 2026

- [IFrame Player API](https://developers.google.com/youtube/iframe_api_reference):
  `onReady`, stavy a chyby, `getAvailablePlaybackRates`, změna rychlosti a `origin`.
- [Požadované chování a identifikace desktopové aplikace](https://developers.google.com/youtube/terms/required-minimum-functionality#embedded-player-api-client-identity):
  `Referer`, identita Windows mimo obchod, viditelné ovládání a žádné překryvy.
- [Virtuální hostitel pro místní obsah WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content#virtual-host-name-mapping):
  místní HTTPS origin, nikoli `file://` s chybějícím referrerem.
