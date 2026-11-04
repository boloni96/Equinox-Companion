namespace EquinoxCompanion;
internal sealed class QuestLevelingProgress
{
    private DateTimeOffset started,lastGain;
    private long initial,last;
    private int completions;
    public void Begin(long total,DateTimeOffset now){initial=last=total;started=lastGain=now;completions=0;}
    public void Observe(long total,DateTimeOffset now){if(total<last){Begin(total,now);return;}if(total>last)lastGain=now;last=total;}
    public void Complete()=>completions++;
    public double? SecondsRemaining(long remaining,DateTimeOffset now)
    {
        if(remaining<=0)return 0;
        var elapsed=(now-started).TotalSeconds;var gained=last-initial;
        if(completions<2||gained<=0||elapsed<60||now-lastGain>TimeSpan.FromMinutes(10))return null;
        var estimate=remaining*elapsed/gained;
        return double.IsFinite(estimate)&&estimate>=0?estimate:null;
    }
    public static long? TotalBefore(int level,Func<int,long> cost)
    {
        if(level<1||level>255)return null;
        long total=0;
        for(var i=1;i<level;i++){var next=cost(i);if(next<=0)return null;total=checked(total+next);}
        return total;
    }
    public static long? Remaining(int level,long xp,int target,Func<int,long> cost)
    {
        if(level>=target)return 0;
        var start=TotalBefore(level,cost);var end=TotalBefore(target,cost);
        if(start==null||end==null||xp<0||xp>=cost(level))return null;
        return Math.Max(0,end.Value-start.Value-xp);
    }
}
