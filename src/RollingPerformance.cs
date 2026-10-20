namespace EquinoxCompanion;

public sealed record PerformanceReading(string Stage,double LastMs,double AverageMs,double RecentPeakMs,double LifetimePeakMs,long RecentSamples,double AgeSeconds);
// One aggregate per second, not one allocation per frame. Stopwatch time is monotonic.
public sealed class RollingPerformance
{
    private sealed class Window
    {
        public readonly long[] Seconds=Enumerable.Repeat(long.MinValue,60).ToArray();
        public readonly long[] Counts=new long[60];
        public readonly double[] Sums=new double[60],Peaks=new double[60];
        public double Last,LifetimePeak,Updated;
    }
    private readonly Dictionary<string,Window> windows=[];
    public void Record(string stage,double milliseconds,double seconds)
    {
        if(!double.IsFinite(milliseconds)||milliseconds<0||!double.IsFinite(seconds)||seconds<0)return;
        if(!windows.TryGetValue(stage,out var w))windows[stage]=w=new();
        var second=(long)seconds;var slot=(int)(second%60);
        if(w.Seconds[slot]!=second){w.Seconds[slot]=second;w.Counts[slot]=0;w.Sums[slot]=w.Peaks[slot]=0;}
        w.Counts[slot]++;w.Sums[slot]+=milliseconds;w.Peaks[slot]=Math.Max(w.Peaks[slot],milliseconds);
        w.Last=milliseconds;w.Updated=seconds;w.LifetimePeak=Math.Max(w.LifetimePeak,milliseconds);
    }
    public PerformanceReading[] Snapshot(double seconds)
    {
        var result=new List<PerformanceReading>();
        foreach(var (stage,w) in windows)
        {
            long count=0;double sum=0,peak=0;
            for(var i=0;i<60;i++)if(seconds-w.Seconds[i]>=0&&seconds-w.Seconds[i]<60){count+=w.Counts[i];sum+=w.Sums[i];peak=Math.Max(peak,w.Peaks[i]);}
            result.Add(new(stage,w.Last,count>0?sum/count:0,peak,w.LifetimePeak,count,Math.Max(0,seconds-w.Updated)));
        }
        return result.OrderByDescending(r=>r.RecentPeakMs).ThenBy(r=>r.Stage,StringComparer.Ordinal).ToArray();
    }
    public void Clear()=>windows.Clear();
}
