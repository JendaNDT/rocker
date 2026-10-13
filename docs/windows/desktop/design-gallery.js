/* D02 design sketches. No device, network, application data or audio access. */
"use strict";
const designs = [
  ["01","Přehled","document","Pokračování v přesné úloze","Denní plán otevírá konkrétní dokument; otevřené nástroje zůstávají dostupné."],
  ["02","Live","live","Samostatné okno pro hraní","Zavření Live skryje pohled. Poslech a aktivní záznam vlastní aplikace."],
  ["03","Ladička","tool","Plovoucí ladička","Ladění a analýza jsou společné s Live; přesun okna nespouští referenční tón."],
  ["04","Metronom","tool","Připojitelný přístroj","BPM, kyvadlo a očíslované doby jsou nahoře; vizualizace čte společné native hodiny."],
  ["05","Výběr lekce","document","Knihovna lekcí","Otevření lekce vytvoří nebo aktivuje konkrétní pracovní relaci."],
  ["06","Průběh lekce","document","Dokument s kroky a úlohou","Změna okna zachová krok, výběr a výsledky. Spuštění hodnocení je výslovné."],
  ["07","Vlastní skladba","document","Skladba jako dokument","Waveform a A/B zůstávají u obsahu; přepnutí záložky samo nepřebere přehrávání."],
  ["08","YouTube cvičení","document","Video vedle cvičení","Jeden skutečný embed; A/B a rychlost u videa. Video není součástí nahrávaného native mixu."],
  ["09","Přesný trénink","document","Konkrétní hodnoticí relace","Jednotlivé tóny a problémové místo; jedna aktivní relace nezávislá na fokusu."],
  ["10","Power chordy","document","Rytmický trénink","Úloha, společné časování a nastavení vedle sebe; bez nového slibu polyfonní detekce."],
  ["11","Gallop","document","Trénink gallopu","Hodnocení a tempo zůstávají u konkrétní relace; přepnutí zobrazení ji nevymění."],
  ["12","Palm mute","document","Kalibrace a trénink","Jasně odlišená kalibrovaná pomůcka; zachovaná původní pravidla."],
  ["13","Aparát","tool","Efektový nástroj","Preset, bypass a zvolený efekt pohromadě; jeden globální řetězec pro celou aplikaci."],
  ["14","Nahrávky","document","Knihovna a editor záznamu","Značky a A/B nad konkrétním RecordingId; export není závislý na novém výběru knihovny."],
  ["15","Studio","document","Projektový editor","Časová osa, stopy a mixer; inspektor i undo patří aktivnímu projektu."],
  ["16","Looper","tool","Nástroj jedné looper relace","Vrstva a transport mají svůj stav; skrytí nástroje nevymaže vrstvy."],
  ["17","Akordy","document","Referenční dokument","Knihovna vybírá objekt, diagram a prstoklad zůstávají v dokumentu."],
  ["18","Stupnice","document","Referenční dokument","Konkrétní stupnice, poloha a diagram; související trénink otevře přímý příkaz."],
  ["19","Riffy","document","Knihovna riffů","Výběr otevírá konkrétní riff nebo jeho trénink, nikoli obecnou stránku."],
  ["20","Noty a tabulatury","document","Partitura v dokumentovém okně","Noty využijí plochu. Stopa, takt, zoom a loop přežijí detach/attach."],
  ["21","Hmatník","document","Herní relace","Klávesnice a výběr patří konkrétní hře; čas se neúčtuje počtem oken."],
  ["22","Sluch","document","Sluchový trénink","Ukázky přehrává řízený preview zdroj; zavření relace ho bezpečně ukončí."],
  ["23","AI — rozhovor","document","Konverzační dokument","Požadavek a odpověď patří relaci. Změna fokusu nepřesměruje výsledek."],
  ["24","AI — plán","document","Pracovní návrh plánu","Použití plánu je výslovné; každá úloha má odkaz na skutečný dokument."],
  ["25","AI — zpětná vazba","document","Rady se zdrojovými statistikami","Statistiky nejsou měření celé hry. Omezení zůstává viditelné."],
  ["26","Moje kytara","system","Profil v nastavení","Změna profilu se promítne do otevřených nástrojů a diagramů."],
  ["27","Pokrok","document","Statistický dokument","Jedna skutečná aktivita, jeden započtený čas; režim bez odměn zachován."],
  ["28","Nastavení","system","Samostatné nastavení","Kategorie a běžné Windows formuláře; druhé vyvolání aktivuje existující okno."],
  ["29","Zvuková zařízení","system","Konkrétní karta a routing","Zvolený mono vstup a stereo výstup; změna zařízení má globální bezpečnostní přechod."],
  ["30","Zálohy a soubory","system","Správa dat a operací","Obnova kontroluje všechny relace; průběh a zrušení jsou v centru operací."],
  ["31","První spuštění","system","Vlastněný průvodce","Krok poslechu vysvětlí výslovné zapnutí monitoringu; obnovou se zvuk nespustí."],
  ["32","Export nahrávky","system","Export nad konkrétním objektem","Systémový picker má vlastníka; výstup a zrušení uchovají původní nahrávku."]
].map(function(d){return {id:d[0],title:d[1],kind:d[2],summary:d[3],behavior:d[4]};});

let theme = "dark";
let monitor = "off";
let filter = "all";
let activeDesign = null;
const escapeText = function(value){return String(value).replaceAll("&","&amp;").replaceAll("<","&lt;").replaceAll(">","&gt;").replaceAll('"',"&quot;");};
const wrap = function(name,cls,html){return "<"+name+' class="'+cls+'">'+html+"</"+name+">";};
const action = function(text,primary){return wrap("span","mock-action"+(primary?" primary":""),escapeText(text));};
const svg = function(body,w,h){return '<svg viewBox="0 0 '+w+" "+h+'" aria-hidden="true" xmlns="http://www.w3.org/2000/svg">'+body+"</svg>";};
function wave(seed){
  let lines="";
  for(let i=0;i<150;i++){
    const y=(.22+.78*Math.abs(Math.sin(i*.47+seed)*Math.sin(i*.075+seed)))*56;
    lines+='<path d="M '+(i*5+8)+" "+(65-y)+" V "+(65+y)+'" stroke="#b99665" stroke-width="2"/>';
  }
  return svg('<path d="M 0 65 H 770" stroke="#707b663b"/><rect x="260" y="0" width="290" height="130" fill="#b58c4020"/>'+lines+'<path d="M 260 0 V 130 M 550 0 V 130" stroke="#c9a769"/><path d="M 390 0 V 130" stroke="#dfd4c1"/>',770,130);
}
function gauge(){
  return svg('<path d="M25 88 Q125 -15 225 88" fill="none" stroke="#987e59" stroke-width="1.5"/><path d="M25 83v10 M60 49l5 8 M90 32l2 9 M125 24v10 M160 32l-2 9 M195 49l-5 8 M225 83v10" stroke="#6c573e"/><text x="14" y="108" font-size="11" fill="#6c573e">−50</text><text x="121" y="17" font-size="11" fill="#6c573e">0</text><text x="214" y="108" font-size="11" fill="#6c573e">+50</text><path d="M125 100L115 30" stroke="#493724" stroke-width="3"/><circle cx="125" cy="100" r="5" fill="#56432d"/>',250,115);
}
function pendulum(){
  return svg('<path d="M75 116 Q150 12 225 116" fill="none" stroke="#a48961"/><path d="M95 85l5 6 M121 54l3 7 M150 43v9 M178 54l-3 7 M205 85l-5 6" stroke="#8f714d"/><path d="M150 124L193 39" stroke="#665640" stroke-width="8"/><path d="M150 124L193 39" stroke="#c9b999" stroke-width="3"/><path d="M169 68l22 11-13 25-22-11z" fill="#a77441" stroke="#624d31"/><circle cx="150" cy="124" r="11" fill="#45382a"/><circle cx="150" cy="124" r="4" fill="#b49464"/>',300,144);
}
function knob(label,angle){
  return wrap("div","dial",svg('<circle cx="40" cy="40" r="34" fill="#24201b" stroke="#826843" stroke-width="3"/><circle cx="40" cy="40" r="28" fill="#322b23"/><path d="M40 40L'+(40+Math.sin(angle)*24)+" "+(40-Math.cos(angle)*24)+'" stroke="#e2cba4" stroke-width="3"/><circle cx="40" cy="40" r="6" fill="#49392a"/>',80,80)+escapeText(label));
}
function tuner(){
  return wrap("div","instrument",wrap("div","instrument-label","LADIČKA · UKÁZKA")+wrap("div","instrument-number","E₂")+gauge()+wrap("div","instrument-detail","−4 centy · standardní ladění"));
}
function metro(full){
  return wrap("div","instrument",wrap("div","instrument-label","METRONOM · UKÁZKA")+wrap("div","instrument-number","100")+wrap("div","instrument-detail","BPM · 4/4 · kyvadlo")+ (full?pendulum():"")+wrap("div","beat-row","<i>1</i><i>2</i><i>3</i><i>4</i>"));
}
function monitoring(){
  const unavailable=monitor==="unavailable",on=monitor==="on";
  const title=unavailable?"Zvukové zařízení není vybrané":on?"Poslech kytary je zapnutý":"Poslech kytary je vypnutý";
  const detail=unavailable?"Vyber kartu a vstup kytary.":on?"Ukázkový stav · hlasitost 15 %":"Signál za aparátem může být přítomný; do mixu se kytara nepřimíchává.";
  const command=unavailable?"Nastavit zvuk":on?"Vypnout poslech":"Zapnout poslech";
  return wrap("div","monitor-status "+(on?"on":unavailable?"unavailable":""),"<div><strong>"+title+"</strong>"+detail+"</div>"+action(command,!on));
}
function table(headers,rows){
  return '<table class="data-table"><thead><tr>'+headers.map(h=>"<th>"+escapeText(h)+"</th>").join("")+"</tr></thead><tbody>"+rows.map((row,i)=>'<tr class="'+(i===0?"active":"")+'">'+row.map(cell=>"<td>"+escapeText(cell)+"</td>").join("")+"</tr>").join("")+"</tbody></table>";
}
function properties(items){
  return items.map(item=>wrap("div","property","<span>"+escapeText(item[0])+"</span>"+wrap("div","mock-field",escapeText(item[1])))).join("");
}
function inspector(model){
  let values=[["Výběr",model.title],["Kontext","Aktivní dokument"],["Stav","Připraveno · ukázka"]];
  if(model.id==="15")values=[["Vybraná stopa","Kytara · hlavní"],["Hlasitost","−6,0 dB"],["Panorama","Střed"],["Začátek regionu","00:04.800"],["Smyčka","Takt 3–7"],["Historie","Zpět / znovu v projektu"]];
  if(["07","14"].includes(model.id))values=[["Zdroj",model.id==="14"?"Dnešní cvičení.wav":"Cvičná skladba.wav"],["Délka","01:24"],["Rychlost","75 %"],["A/B úsek","00:12–00:24"],["Značky","Problémové místo"]];
  if(["06","09","10","11","12"].includes(model.id))values=[["Úloha",model.title],["Tempo","100 BPM"],["Poslech","Výslovně podle Live"],["Hodnocení","Jedna aktivní relace"],["Výsledek","Ukázkové údaje"]];
  if(model.id==="20")values=[["Partitura","Cvičná fráze"],["Stopa","Kytara"],["Zvětšení","125 %"],["Vybraný takt","3"],["Smyčka","Takt 3–7"],["Zvuk","Syntetický poslech"]];
  return wrap("aside","inspector",wrap("div","pane-heading","VLASTNOSTI VÝBĚRU")+properties(values));
}
function library(model){
  const groups=["Lekce","Skladby","Nahrávky","Projekty","Akordy a stupnice","Riffy","Hry","AI tutor"];
  let selected=model.id==="15"?"Projekty":model.id==="14"?"Nahrávky":["05","06","09","10","11","12"].includes(model.id)?"Lekce":model.id==="07"?"Skladby":["23","24","25"].includes(model.id)?"AI tutor":["21","22"].includes(model.id)?"Hry":["17","18"].includes(model.id)?"Akordy a stupnice":"Riffy";
  return wrap("aside","library",wrap("div","pane-heading","KNIHOVNA")+wrap("div","search-box","Hledat v knihovně…")+groups.map(g=>wrap("div","library-row"+(g===selected?" selected":""),g)+(g===selected?wrap("div","library-row item","Moje cvičení")+wrap("div","library-row item","Poslední otevřené"):"")).join("")+wrap("div","library-row space","Oblíbené")+wrap("div","library-row","Sestavy cvičení"));
}
function score(){
  let staff="";
  for(let row=0;row<2;row++)for(let line=0;line<6;line++)staff+='<path d="M18 '+(20+row*80+line*10)+" H610"+'" stroke="#a99f8a" stroke-width=".8"/>';
  for(let i=0;i<12;i++)staff+='<text x="'+(50+i*46)+'" y="'+(33+(i%4)*10)+'" font-size="14" fill="#30291f">'+[0,2,3,2,0,5][i%6]+"</text>";
  staff+='<path d="M220 12V75 M420 12V75 M220 92V155 M420 92V155" stroke="#897e68"/>';
  return wrap("div","score-paper","<h4>Cvičná fráze · ilustrační tabulatura</h4>"+svg(staff,630,180));
}
function fretboard(){
  let lines="";
  for(let i=0;i<6;i++)lines+='<path d="M20 '+(20+i*25)+'H650" stroke="#baae91" stroke-width="'+(1+i*.3)+'"/>';
  for(let i=0;i<11;i++)lines+='<path d="M'+(30+i*60)+' 14V154" stroke="#897858" stroke-width="3"/>';
  for(let i=0;i<7;i++)lines+='<circle cx="'+(60+i*70)+'" cy="'+(20+(i%6)*25)+'" r="11" fill="'+(i%3===0?"#d1ab78":"#817155")+'"/>';
  return wrap("div","fretboard",svg(lines,680,175));
}
function training(model){
  const title=model.id==="06"?"Zahraj nízké E":model.id==="09"?"Problémové místo · pomalý průchod":model.id==="12"?"Kalibrovaný palm mute":"Drž rytmus a pokračuj";
  return wrap("div","steps",wrap("div","step-list",wrap("div","step","1 · Příprava")+wrap("div","step current","2 · "+model.title)+wrap("div","step","3 · Opakování")+wrap("div","step","4 · Výsledek"))+wrap("div","task-area",wrap("div","practice-target",title)+wrap("div","task-reading","Konkrétní úloha zůstává otevřená při práci s dalšími okny. Hodnocení spustíš výslovně.")+wrap("div","note-line",model.id==="06"?"E · E · E":"E · G · A")+wrap("div","subtoolbar",action("Spustit hodnocení",true)+action("Pozastavit")+action("Restartovat"))+wrap("div","progress-line","")+wrap("div","task-reading","Ukázkové údaje · relace připravená")+score()));
}
function studio(){
  const tracks=[["Kytara · hlavní","Mono / vstup 1"],["Kytara · doprovod","−9,0 dB"],["Podklad","Stereo / soubor"]];
  return wrap("div","timeline",wrap("div","timeline-ruler","<span>1</span><span>2</span><span>3</span><span>4</span><span>5</span><span>6</span><span>7</span><span>8</span>")+tracks.map((t,i)=>wrap("div","track",wrap("div","track-name",t[0]+"<small>"+t[1]+"</small>")+wrap("div","track-lane",wrap("div","clip",wave(i+1))))).join(""))+wrap("div","mixer",tracks.map(t=>wrap("div","mixer-strip","<span>"+t[0]+"</span>"+wrap("div","mixer-meter",""))).join(""));
}
function documentContent(model){
  switch(model.id){
    case "01":return wrap("div","stat-row",wrap("div","stat","<strong>17 min</strong><span>Dnešní plán · ukázka</span>")+wrap("div","stat","<strong>3 úlohy</strong><span>Připraveno ke cvičení</span>"))+table(["Pokračovat","Práce","Akce"],[["Nízké E","Lekce · krok 2","Otevřít úlohu"],["Rytmická jistota","Metronom · 100 BPM","Otevřít trénink"],["Vlastní riff","Skladba · A/B","Otevřít dokument"]])+monitoring()+wrap("div","empty-state","<strong>Pracovní plocha je tvoje</strong>Nástroje otevři vedle dokumentu nebo v samostatném okně.");
    case "05":return wrap("div","subtoolbar",action("Všechny lekce",true)+action("Začátečník")+action("Pokročilé")+action("Nedokončené"))+table(["Lekce","Oblast","Stav"],[["První tóny","Základy","Rozpracováno"],["Rytmus a úhozy","Základy","Připraveno"],["Power chordy","Rock","Připraveno"],["Palm mute","Metal","Připraveno"]])+wrap("div","empty-state","<strong>Otevřít konkrétní lekci</strong>Výběr v knihovně určuje úlohu; dokument uchová její krok.");
    case "06":case "09":case "10":case "11":case "12":return training(model);
    case "07":case "14":return wrap("div","subtoolbar",action("Přehrát / pauza",true)+action("A/B smyčka")+action("Přidat značku")+action("Exportovat…"))+wrap("div","wave-area",wave(4))+table(["Značka","Pozice","Úsek"],[["Začátek fráze","00:12.000","A"],["Problémové místo","00:18.250",""],["Konec opakování","00:24.000","B"]])+wrap("div","task-reading","Ilustrační waveform · skutečný dokument použije dekódované audio.");
    case "08":return wrap("div","video-placeholder","<span>Oficiální video přehrávač · návrh</span>")+wrap("div","subtoolbar",action("Načíst video…",true)+action("A · 00:12")+action("B · 00:24")+action("Rychlost · 75 %")+action("V prohlížeči"))+table(["Uložený úsek","A/B","Rychlost"],[["Cvičná fráze","00:12–00:24","75 %"],["Druhé opakování","00:32–00:48","100 %"]]);
    case "15":return wrap("div","subtoolbar",action("Importovat stopu…")+action("Připojit nahrávku")+action("Zpět")+action("Uložit projekt",true))+studio();
    case "17":case "18":case "19":return wrap("div","subtoolbar",action(model.id==="17"?"E moll":model.id==="18"?"E moll pentatonika":"Cvičný rockový riff",true)+action("Poloha · 1")+action("Otevřít trénink"))+fretboard()+table(["Objekt","Poloha","Akce"],[["Vybraný obsah","1","Otevřít"],["Další varianta","3","Otevřít"],["Související trénink","—","Cvičit"]]);
    case "20":return wrap("div","subtoolbar",action("Kytara · stopa 1")+action("Zoom · 125 %")+action("Takt · 3")+action("Sledovat přehrávání")+action("Smyčka · 3–7"))+score()+wrap("div","task-reading","Syntetický poslech not · ukázkový notový obsah.");
    case "21":return wrap("div","practice-target","Najdi tón G")+fretboard()+wrap("div","subtoolbar",action("Spustit hru",true)+action("Rozsah · 1–5")+action("Pauza"))+wrap("div","task-reading","Ukázka otázky · klávesnice míří do konkrétní herní relace.");
    case "22":return wrap("div","practice-target","Poznej interval")+wrap("div","subtoolbar",action("Poslechnout ukázku",true)+action("Znovu"))+wrap("div","note-line","1 · ?")+table(["Odpověď","Volba"],[["Malá tercie","1"],["Velká tercie","2"],["Kvinta","3"]])+wrap("div","task-reading","Ukázkové údaje · žádná zvuková ukázka se v galerii nepřehrává.");
    case "23":return wrap("div","chat-message","Jak si rozvrhnout krátké cvičení?")+wrap("div","chat-message answer","Návrh odpovědi: nejprve naladit, pak pomalá konkrétní úloha a krátké opakování. Skutečný dotaz se posílá až po tvé akci.")+wrap("div","composer","Napiš zprávu…"+action("Odeslat",true));
    case "24":return table(["Navržená úloha","Čas","Konkrétní práce"],[["Naladění","2 min","Ladička"],["Jednotlivé tóny","5 min","Lekce · krok 2"],["Rytmus","5 min","Gallop · základ"],["Opakování","5 min","Vlastní skladba"]])+wrap("div","subtoolbar",action("Použít plán",true)+action("Zrušit"))+wrap("div","task-reading","Ilustrační plán · AI návrh se použije výslovně.");
    case "25":return wrap("div","stat-row",wrap("div","stat","<strong>34 min</strong><span>Uložený čas · ukázka</span>")+wrap("div","stat","<strong>2 lekce</strong><span>Zdrojové statistiky</span>"))+wrap("div","chat-message answer","Zpětná vazba vychází ze statistik cvičení. Není vyhodnocením zvuku celé hry ani diagnózou techniky.")+action("Vyžádat radu",true);
    case "27":return wrap("div","stat-row",wrap("div","stat","<strong>34 min</strong><span>Aktivní čas · ukázka</span>")+wrap("div","stat","<strong>4 dny</strong><span>Pravidelnost · ukázka</span>"))+wrap("div","bar-chart",[35,55,42,76,30,68,49].map(v=>'<i style="height:'+v+'%"></i>').join(""))+table(["Činnost","Výsledek"],[["Lekce","Uložený postup"],["Cvičení","Jedna aktivní relace"],["Odměny","Lze vypnout"]]);
    default:throw new Error("Missing document design: "+model.id);
  }
}
function toolsContent(model){
  switch(model.id){
    case "03":return tuner()+wrap("div","subtoolbar",action("Standardní ladění",true)+action("Referenční A · 440 Hz"))+table(["Struna","Tón","Reference"],[["6","E₂","Poslechnout"],["5","A₂","Poslechnout"],["4","D₃","Poslechnout"]])+wrap("div","tool-tabs","<span>Kalibrace</span><span>Práh šumu</span><span>Vlastní ladění</span>");
    case "04":return metro(true)+wrap("div","subtoolbar",action("Spustit",true)+action("Zastavit")+action("Tap")+action("Doby / Kyvadlo"))+table(["Rytmus","Hodnota"],[["Takt","4/4"],["Předtakt","1 takt"],["Zvuk","Dřevo"]])+wrap("div","tool-tabs","<span>Rytmus a zvuky</span><span>Trénink</span>");
    case "13":return wrap("div","instrument",wrap("div","instrument-label","ROCKER / APARÁT")+wrap("div","tool-heading","Warm clean")+wrap("div","instrument-detail","Preset · ukázkové nastavení"))+wrap("div","chain",wrap("span","effect","Brána")+wrap("span","effect active","Drive")+wrap("span","effect","EQ")+wrap("span","effect","Delay")+wrap("span","effect","Reverb"))+wrap("div","dial-row",knob("Gain",-.8)+knob("Tone",.4)+knob("Výstup",.1))+wrap("div","subtoolbar",action("Aparát zapnutý",true)+action("Presety…")+action("Uložit preset…"))+monitoring();
    case "16":return wrap("div","instrument",wrap("div","instrument-label","ROCKER / LOOPER")+wrap("div","instrument-number","4 takty")+wrap("div","instrument-detail","Připravená relace · ukázka"))+wrap("div","subtoolbar",action("Nahrát vrstvu",true)+action("Overdub")+action("Přehrát")+action("Zastavit"))+table(["Vrstva","Hlasitost","Stav"],[["Základ","−3 dB","Ukázka"],["Doprovod","−6 dB","Ukázka"]])+wrap("div","subtoolbar",action("Uložit session…")+action("Vymazat…"));
    default:throw new Error("Missing tool design: "+model.id);
  }
}
function settingsContent(model){
  const row=function(label,value){return wrap("div","form-row","<span>"+label+"</span><strong>"+value+"</strong>");};
  switch(model.id){
    case "26":return row("Název kytary","Moje elektrická kytara")+row("Ruka","Pravá")+row("Ladění","Standardní")+row("Reference","440 Hz")+fretboard()+action("Uložit profil",true);
    case "28":return wrap("div","form-group-title","Rozhraní")+row("Vzhled","Systémový / Vintage")+row("Pracovní rozložení","Cvičení")+row("Obnovit otevřené pohledy","Ano · bez přehrávání")+row("Režim bez odměn","Vypnutý")+wrap("div","form-group-title","Okna")+row("Nástroje","Připojené nebo plovoucí")+action("Obnovit výchozí rozložení");
    case "29":return row("Ovladač","ASIO · příklad")+row("Zařízení","M-Audio M-Track Solo")+row("Kytara","Vstup 1 · mono")+row("Výstup","Kanály 1 + 2")+row("Vzorkování","44 100 Hz · příklad")+row("Buffer","Výchozí ovladače")+wrap("div","task-reading","Skutečné možnosti poskytne ovladač. Tyto hodnoty nejsou měření celkové odezvy.")+wrap("div","subtoolbar",action("Ověřit a použít",true)+action("Panel ovladače…"))+monitoring();
    case "30":return wrap("div","form-group-title","Úplná záloha")+row("Dokumenty a nastavení","Zahrnout")+row("Nahrávky a projekty","Zahrnout")+row("Rozložení oken","Samostatná volba")+wrap("div","subtoolbar",action("Vytvořit zálohu…",true)+action("Obnovit zálohu…"))+wrap("div","empty-state","<strong>Žádná probíhající operace</strong>Export, obnova a import mají svůj průběh a možnost zrušení.");
    case "31":return wrap("div","instrument",wrap("div","instrument-label","PRVNÍ SPUŠTĚNÍ")+wrap("div","tool-heading","Poslech a monitoring"))+wrap("div","task-reading","Kytaru a sluchátka připoj ke stejné kartě. Softwarový poslech zapneš výslovně v Live. Direct Monitor karty je samostatná cesta bez efektů Rockeru.")+monitoring()+wrap("div","subtoolbar",action("Zpět")+action("Pokračovat",true));
    case "32":return row("Nahrávka","Dnešní cvičení.wav")+row("Rozsah","A/B · 00:12–00:24")+row("Formát","WAV PCM24 · stereo")+row("Výstup","Vybrat systémovým dialogem…")+wrap("div","task-reading","Export patří této nahrávce i při přepnutí jiného okna.")+wrap("div","subtoolbar",action("Exportovat…",true)+action("Zrušit"));
    default:throw new Error("Missing system design: "+model.id);
  }
}
function buildWindow(model){
  const ownedDialog=model.id==="31" || model.id==="32";
  const caption=wrap("div","caption",wrap("span","app-glyph","R")+"Rocker — "+escapeText(model.title)+wrap("div","caption-actions","<i>−</i><i>□</i><i>×</i>"));
  const menu=wrap("div","menu-row","<span>Soubor</span><span>Úpravy</span><span>Zobrazení</span><span>Nástroje</span><span>Okno</span><span>Nápověda</span>");
  const toolbar=wrap("div","toolbar",action("Otevřít…")+action("Uložit")+wrap("span","divider","")+action("Zpět / znovu")+action("Hledat · Ctrl+K")+action("Live")+action("Rozložení · Cvičení"));
  const status=wrap("div","statusbar","<span>NÁVRH UI · UKÁZKOVÁ DATA</span><span>"+(monitor==="unavailable"?"Bez zařízení":monitor==="on"?"Poslech kytary zapnutý":"Poslech kytary vypnutý")+"</span>");
  const transport=wrap("div","transport",wrap("div","transport-source","Zdroj · "+(model.id==="15"?"Projekt: Moje cvičení":model.id==="07"?"Cvičná skladba":"Žádný aktivní zdroj"))+action("Přehrát / pauza")+action("Nahrávat")+action("Zastavit vše"));
  let content="";
  if(model.kind==="tool")content=wrap("div","tool-content",toolsContent(model))+status;
  else if(model.kind==="live")content=wrap("div","tool-content",monitoring()+wrap("div","instrument-row",tuner()+metro(true))+wrap("div","chain",wrap("span","effect active","Aparát · Warm clean")+action("Presety…")+action("Otevřít aparát"))+wrap("div","signal-route","<span>Kytara</span> → <span>Aparát</span> → <span>Poslech</span> → <span>Mix</span>")+wrap("div","subtoolbar",action("Nahrávat kytaru",true)+action("Zastavit a uložit")+action("Jen hraju · F11")))+transport+status;
  else if(model.kind==="system")content=wrap("div","workarea",(ownedDialog?"":wrap("aside","settings-navigation",["Obecné","Zvuková zařízení","Moje kytara","Klíče a AI","Zálohy a soubory","Aktualizace"].map(g=>wrap("div","library-row"+(model.title.startsWith(g)?" selected":""),g)).join("")))+wrap("div","document-body",wrap("div","document-title",model.title)+wrap("div","document-subtitle",ownedDialog?"Dialog se skutečným vlastníkem · návrh":"Samostatné systémové okno · návrh")+settingsContent(model)))+status;
  else content=toolbar+wrap("div","workarea",library(model)+wrap("div","document-area",wrap("div","tabs",wrap("div","tab active",escapeText(model.title)+(model.id==="15"?" *":"")+"<b>×</b>")+wrap("div","tab","Cvičná skladba <b>×</b>")+wrap("div","tab","Noty <b>×</b>"))+wrap("div","document-body",wrap("div","document-title",escapeText(model.title))+wrap("div","document-subtitle",escapeText(model.summary)+" · ukázkové údaje")+documentContent(model)))+inspector(model))+transport+status;
  return wrap("div","mock-window "+model.kind+(ownedDialog?" dialog-preview":"")+(theme==="light"?" light":""),caption+(model.kind==="tool" || ownedDialog?"":menu)+content);
}
function fit(container,inner){
  const node=inner.querySelector(".mock-window");
  if(!node)return;
  const scale=Math.min(1,container.classList.contains("thumb")?340/node.offsetHeight:1,Math.max(.1,(container.clientWidth-24)/node.offsetWidth));
  inner.style.transform="scale("+scale+")";
  inner.style.marginLeft=Math.max(0,(container.clientWidth-24-node.offsetWidth*scale)/2)+"px";
  container.style.height=(node.offsetHeight*scale+14)+"px";
}
function refreshFits(){
  document.querySelectorAll(".thumb").forEach(function(t){fit(t,t.firstElementChild);});
  const scene=document.querySelector(".desktop-scene"),inner=document.getElementById("desktop");
  const scale=scene.clientWidth/1320;inner.style.transform="scale("+scale+")";scene.style.height=(890*scale)+"px";
  const detail=document.querySelector(".detail-preview");
  if(document.getElementById("detail").open)fit(detail,document.getElementById("detail-window"));
}
function openDesign(model){
  activeDesign=model;
  document.getElementById("detail-title").textContent=model.id+" · "+model.title;
  document.getElementById("detail-description").textContent=model.summary;
  document.getElementById("detail-behavior").textContent=model.behavior;
  document.getElementById("detail-window").innerHTML=buildWindow(model);
  document.getElementById("detail").showModal();refreshFits();
}
function render(){
  const gallery=document.getElementById("gallery");gallery.replaceChildren();
  for(const model of designs){
    const card=document.createElement("article");card.className="design-card";card.dataset.id=model.id;card.dataset.kind=model.kind;card.hidden=filter!=="all" && filter!==model.kind;
    card.innerHTML=wrap("div","card-heading",'<div><small>'+model.id+" / "+({"document":"DOKUMENT","live":"SAMOSTATNÉ LIVE","tool":"NÁSTROJ","system":"SYSTÉMOVÉ OKNO"})[model.kind]+"</small><h3>"+escapeText(model.title)+'</h3></div><button type="button" aria-label="Zvětšit návrh '+escapeText(model.title)+'">Zvětšit</button>')+'<div class="thumb" aria-hidden="true"><div class="thumb-inner">'+buildWindow(model)+"</div></div>"+wrap("div","card-copy","<strong>"+escapeText(model.summary)+"</strong>"+escapeText(model.behavior));
    card.querySelector("button").addEventListener("click",function(){openDesign(model);});gallery.append(card);
  }
  document.getElementById("desktop").innerHTML=wrap("div","desktop-main",buildWindow(designs.find(d=>d.id==="15")))+wrap("div","desktop-live",buildWindow(designs.find(d=>d.id==="02")))+wrap("div","desktop-metro",buildWindow(designs.find(d=>d.id==="04")));
  if(activeDesign && document.getElementById("detail").open)document.getElementById("detail-window").innerHTML=buildWindow(activeDesign);
  const count=designs.filter(d=>filter==="all" || d.kind===filter).length;
  document.getElementById("count").textContent=count+" z 32 návrhů · společná sada D02";
  requestAnimationFrame(refreshFits);
}
document.querySelectorAll("[data-filter]").forEach(function(button){
  button.addEventListener("click",function(){filter=button.dataset.filter;document.querySelectorAll("[data-filter]").forEach(b=>b.setAttribute("aria-pressed",String(b===button)));render();});
});
document.getElementById("theme").addEventListener("change",function(e){theme=e.target.value;render();});
document.getElementById("monitor").addEventListener("change",function(e){monitor=e.target.value;render();});
document.getElementById("close").addEventListener("click",function(){document.getElementById("detail").close();});
window.addEventListener("resize",refreshFits);
render();
