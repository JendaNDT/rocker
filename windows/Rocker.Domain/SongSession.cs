// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;
public sealed record SongSection(string Id,string Label,double Start,double End);
public sealed record SongSession(string Id,string Name,string SourcePath,double Duration,double Position=0,double Speed=1,
    IReadOnlyList<SongSection>? Sections=null,uint CountInBars=0,uint Beats=4,double Bpm=100,bool Ramp=false,double RampStep=.05,uint RampEvery=4,double RampTarget=1);
public static class SongRules
{
    public static SongSession Validate(SongSession value)
    {
        static bool Finite(double n,double min,double max)=>double.IsFinite(n) && n>=min && n<=max;
        if(!Guid.TryParse(value.Id,out _) || string.IsNullOrWhiteSpace(value.Name) || value.Name.Length>512 || value.SourcePath is null || value.SourcePath.Length is <1 or >32768 || value.SourcePath.Any(char.IsControl) || !(value.SourcePath.StartsWith('/') || value.SourcePath.StartsWith("\\\\",StringComparison.Ordinal) || value.SourcePath.Length>2 && char.IsAsciiLetter(value.SourcePath[0]) && value.SourcePath[1]==':' && value.SourcePath[2]=='\\') || !Finite(value.Duration,.000001,24*60*60) || !Finite(value.Position,0,value.Duration) || !Finite(value.Speed,.25,2) || value.CountInBars>2 || value.Beats is <1 or >16 || !Finite(value.Bpm,40,240) || !Finite(value.RampStep,.01,.25) || value.RampEvery is <1 or >32 || !Finite(value.RampTarget,.25,2))throw new InvalidDataException("Údaje skladby nejsou platné.");
        var sections=value.Sections??[];
        if(sections.Count>128 || sections.Any(s=>!UserData.ValidId(s.Id) || string.IsNullOrWhiteSpace(s.Label) || s.Label.Length>128 || !Finite(s.Start,0,value.Duration) || !Finite(s.End,0,value.Duration) || s.End<=s.Start) || sections.Select(s=>s.Id).Distinct(StringComparer.Ordinal).Count()!=sections.Count)throw new InvalidDataException("Úseky skladby nejsou platné.");
        return value with{Sections=sections};
    }
}
