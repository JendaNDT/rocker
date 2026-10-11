// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;
public sealed record ToneRecommendation(string Pickup,string Gain,string Eq,string Note);
public static class ToneAdvisor
{
    public static readonly IReadOnlyDictionary<string,string> Styles=new Dictionary<string,string>{["clean"]="Čistý zvuk",["blues"]="Blues",["rock-rhythm"]="Rock · rytmika",["rock-lead"]="Rock · sólo",["metal-rhythm"]="Metal · rytmika",["metal-lead"]="Metal · sólo",["funk"]="Funk"};
    public static readonly IReadOnlyDictionary<string,string> Positions=new Dictionary<string,string>{["neck"]="Krk",["middle"]="Střed",["bridge"]="Kobylka"};
    public static readonly IReadOnlyDictionary<string,string> Types=new Dictionary<string,string>{["humbucker"]="Humbucker",["single"]="Single",["p90"]="P90"};
    private static readonly IReadOnlyDictionary<string,(string Pickup,string Gain,string Eq,bool Humbucker)> Advice=new Dictionary<string,(string,string,string,bool)>{
        ["clean"]=("neck","Nízký","Basy mírně nahoru, výšky zkrotit — plný a kulatý čistý zvuk.",false),
        ["blues"]=("neck","Nízký","Středy nahoru, nech aparát jemně breakovat na silnější úhoz.",false),
        ["rock-rhythm"]=("bridge","Střední","Basy a středy vyvážené, výšky lehce stáhnout, ať to neřeže.",true),
        ["rock-lead"]=("neck","Střední","Středy nahoru, ať sólo prořízne kapelu; basy ubrat.",true),
        ["metal-rhythm"]=("bridge","Vysoký","Středy ubrat (scoop), basy pevné, výšky pro definici riffů.",true),
        ["metal-lead"]=("bridge","Vysoký","Středy zpět nahoru proti scoopnuté rytmice, ať je sólo slyšet.",true),
        ["funk"]=("bridge","Nízký","Výšky nahoru, basy mírně dolů — ostrý a perkusivní zvuk.",false)};
    public static ToneRecommendation Recommend(InstrumentProfile profile,string style)
    {
        if(!Advice.TryGetValue(style,out var rule))throw new ArgumentException("Neznámý styl.");string[] order=["neck","middle","bridge"];var pickup=profile.Pickups.FirstOrDefault(p=>p.Position==rule.Pickup)??order.OrderBy(p=>Math.Abs(Array.IndexOf(order,p)-Array.IndexOf(order,rule.Pickup))).Select(p=>profile.Pickups.FirstOrDefault(v=>v.Position==p)).FirstOrDefault(p=>p is not null);
        if(pickup is null)return new(rule.Pickup,rule.Gain,rule.Eq,"Profil nemá zadané snímače — doplň je v kartě kytary a poradím přesněji.");var notes=new List<string>();if(pickup.Position!=rule.Pickup)notes.Add($"Ideální by byl snímač u {Positions[rule.Pickup].ToLowerInvariant()}, ten v profilu nemáš — beru nejbližší ({Positions[pickup.Position].ToLowerInvariant()}).");
        notes.Add(rule.Humbucker && pickup.Type=="single"?"V téhle pozici máš single — pro nářez bude tenčí a při vysokém gainu šumí. Drž gain o stupeň níž, nebo zvaž humbucker do kobylky.":rule.Humbucker && pickup.Type=="p90"?"P90 v téhle pozici má víc kousavosti než single, ale míň než humbucker — gain klidně přidej, jen hlídej zpětnou vazbu.":!rule.Humbucker && pickup.Type=="humbucker"?"Máš tu humbucker — pro jiskřivější zvuk stáhni knoflík tónu nebo použij split coil, pokud ho kytara umí.":"Snímač v profilu tomuhle stylu sedí — jedeš naplno.");return new(pickup.Position,rule.Gain,rule.Eq,string.Join(" ",notes));
    }
}
