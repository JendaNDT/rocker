// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Video;

public sealed record VideoRange(double Start,double End);
// The browser owns approximate A/B timing. This class owns trusted native state.
public sealed class VideoPractice
{
    public long Token{get;private set;}
    public string? Id{get;private set;}
    public bool Ready{get;private set;}
    public double Position{get;private set;}
    public double Duration{get;private set;}
    public double Rate{get;private set;}=1;
    public IReadOnlyList<double> Rates{get;private set;}=Array.Empty<double>();
    public VideoRange? Loop{get;private set;}
    public int State{get;private set;}=-1;
    public void Begin(string id)
    {
        if(VideoProtocol.ExtractId(id)!=id)throw new ArgumentException("Neplatné ID videa.");
        Id=id;++Token;Ready=false;Position=Duration=0;Rate=1;Rates=Array.Empty<double>();Loop=null;State=-1;
    }
    public bool Observe(VideoEvent message)
    {
        if(Id is null || message.Token!=Token)return false;
        switch(message.Kind)
        {
            case "ready":Ready=true;break;
            case "state":State=message.Code;break;
            case "rates":Rates=Array.AsReadOnly(message.Rates??[]);break;
            case "progress":Position=message.Position;Duration=message.Duration;Rate=message.Rate;break;
            case "error":Ready=false;State=-1;break;
            default:return false;
        }
        return true;
    }
    public void SetLoop(double start,double end)
    {
        if(!double.IsFinite(start) || !double.IsFinite(end) || start<0 || end<=start || end>864000 || (Duration>0 && end>Duration+.25))throw new ArgumentException("B musí být za A a uvnitř videa.");
        Loop=new(start,end);
    }
    public void ClearLoop()=>Loop=null;
    public void Suspend(){Ready=false;State=-1;}
}
