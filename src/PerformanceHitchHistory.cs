namespace EquinoxCompanion;
public sealed record PerformanceHitch(long Frame,string Stage,double Milliseconds,double ClockSeconds,DateTimeOffset AtUtc,object? Context);
// Fixed-capacity memory only. Recording never writes files or grows a history list.
public sealed class PerformanceHitchHistory
{
    public const int Capacity=256;
    public const double ThresholdMs=16,RetentionSeconds=600;
    private readonly PerformanceHitch?[] rows=new PerformanceHitch?[Capacity];
    private int next,count;
    public const int WorstCapacity=32;
    private readonly List<PerformanceHitch> worst=new(WorstCapacity);
    public PerformanceHitch[] WorstSnapshot(double clock)=>worst.Where(r=>clock>=r.ClockSeconds&&clock-r.ClockSeconds<=RetentionSeconds).OrderByDescending(r=>r.Milliseconds).ToArray();
    public long TotalRecorded {get;private set;}
    public void Record(long frame,string stage,double milliseconds,double clock,DateTimeOffset at,object? context)
    {
        if(!double.IsFinite(milliseconds)||milliseconds<ThresholdMs||!double.IsFinite(clock)||clock<0)return;
        var sample=new PerformanceHitch(frame,stage,milliseconds,clock,at,context);
        // Keep one largest sample per frame so nested timers cannot crowd out other freezes.
        worst.RemoveAll(r=>clock-r.ClockSeconds>RetentionSeconds);
        var same=worst.FindIndex(r=>r.Frame==frame);
        if(same>=0){if(milliseconds>worst[same].Milliseconds)worst[same]=sample;}
        else if(worst.Count<WorstCapacity)worst.Add(sample);
        else {var smallest=0;for(var i=1;i<worst.Count;i++)if(worst[i].Milliseconds<worst[smallest].Milliseconds)smallest=i;
            if(milliseconds>worst[smallest].Milliseconds)worst[smallest]=sample;}
        rows[next]=sample;next=(next+1)%Capacity;count=Math.Min(Capacity,count+1);TotalRecorded++;
    }
    public PerformanceHitch[] Snapshot(double clock)
    {
        var result=new List<PerformanceHitch>(count);
        for(var i=0;i<count;i++)if(rows[(next-count+i+Capacity)%Capacity] is {} row&&clock>=row.ClockSeconds&&clock-row.ClockSeconds<=RetentionSeconds)result.Add(row);
        return result.ToArray();
    }
    public void Clear(){Array.Clear(rows);next=count=0;TotalRecorded=0;worst.Clear();}
}
