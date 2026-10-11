// SPDX-License-Identifier: AGPL-3.0-only
namespace Rocker.Domain;
public enum PracticePhase{Idle,Running,Paused,Ended}
public sealed record PracticeObservation(double ChargedMs,bool Remind,bool GapDiscarded);
// Evidence and clocks are supplied by the shell. No audio device or wall-clock
// time alone can manufacture practice; pending daily totals drain exactly once.
public sealed class PracticeSession
{
    public PracticePhase Phase{get;private set;}
    public Guid Id{get;private set;}
    public double TotalMs{get;private set;}
    public BreakState Break{get;private set;}=new();
    private double? lastMonotonic;private DateTimeOffset lastWall;private bool lastEvidence;
    private readonly Dictionary<string,double> pending=[];
    public void Start(double monotonic,DateTimeOffset wall)
    {
        if(!double.IsFinite(monotonic) || Phase is PracticePhase.Running or PracticePhase.Paused || pending.Count>0)throw new InvalidOperationException("Před novým cvičením ukonči a ulož předchozí session.");
        Id=Guid.NewGuid();Phase=PracticePhase.Running;TotalMs=0;lastMonotonic=monotonic;lastWall=wall;lastEvidence=false;
    }
    public void Pause(){if(Phase==PracticePhase.Running)Phase=PracticePhase.Paused;lastEvidence=false;}
    public void Resume(){if(Phase==PracticePhase.Paused)Phase=PracticePhase.Running;lastEvidence=false;}
    public void End(){Phase=PracticePhase.Ended;lastEvidence=false;}
    public PracticeObservation Observe(double monotonic,DateTimeOffset wall,TimeZoneInfo zone,bool evidence)
    {
        if(!double.IsFinite(monotonic))return new(0,false,true);
        double delta=lastMonotonic is { } previous?monotonic-previous:0;double wallDelta=(wall-lastWall).TotalMilliseconds;
        bool gap=delta<0 || delta>2000 || delta>0 && wallDelta<=0 || Math.Abs(delta-wallDelta)>250;bool active=Phase==PracticePhase.Running && evidence;double charge=!gap && delta>0 && lastEvidence && active?delta:0;
        if(charge>0)
        {
            string Day(DateTimeOffset instant)=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant,zone).DateTime).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            string first=Day(lastWall),last=Day(wall);void Add(string day,double ms)=>pending[day]=pending.GetValueOrDefault(day)+ms/60000;
            if(first==last)Add(first,charge);else
            {
                // At most a two-second observed interval: locate the actual UTC
                // date boundary, including DST and non-hour offset time zones.
                long lo=lastWall.UtcTicks,hi=wall.UtcTicks;while(hi-lo>1){long mid=lo+(hi-lo)/2;if(Day(new(mid,TimeSpan.Zero))==first)lo=mid;else hi=mid;}double fraction=(hi-lastWall.UtcTicks)/(double)(wall.UtcTicks-lastWall.UtcTicks);Add(first,charge*fraction);Add(last,charge*(1-fraction));
            }
            TotalMs+=charge;
        }
        var rest=ProgressRules.AdvanceBreak(Break,Math.Max(0,delta),charge>0);Break=rest.State;lastMonotonic=monotonic;lastWall=wall;lastEvidence=active && !gap;
        return new(charge,rest.Remind,gap);
    }
    public IReadOnlyDictionary<string,double> Drain(){var result=new Dictionary<string,double>(pending);pending.Clear();return result;}
}
