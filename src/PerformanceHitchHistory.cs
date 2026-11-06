namespace EquinoxCompanion;
public sealed record PerformanceHitch(long Frame,string Stage,double Milliseconds,double ClockSeconds,DateTimeOffset AtUtc,object? Context);
// Fixed-capacity memory only. Recording never writes files or grows a history list.
public sealed class PerformanceHitchHistory
{
    public const int Capacity=256;
    public const double ThresholdMs=16,RetentionSeconds=600;
    private readonly PerformanceHitch?[] rows=new PerformanceHitch?[Capacity];
    private int next,count;
    public long TotalRecorded {get;private set;}
    public void Record(long frame,string stage,double milliseconds,double clock,DateTimeOffset at,object? context)
    {
        if(!double.IsFinite(milliseconds)||milliseconds<ThresholdMs||!double.IsFinite(clock)||clock<0)return;
        rows[next]=new(frame,stage,milliseconds,clock,at,context);next=(next+1)%Capacity;count=Math.Min(Capacity,count+1);TotalRecorded++;
    }
    public PerformanceHitch[] Snapshot(double clock)
    {
        var result=new List<PerformanceHitch>(count);
        for(var i=0;i<count;i++)if(rows[(next-count+i+Capacity)%Capacity] is {} row&&clock>=row.ClockSeconds&&clock-row.ClockSeconds<=RetentionSeconds)result.Add(row);
        return result.ToArray();
    }
    public void Clear(){Array.Clear(rows);next=count=0;TotalRecorded=0;}
}
