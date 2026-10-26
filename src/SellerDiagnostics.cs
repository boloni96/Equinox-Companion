namespace EquinoxCompanion;

public sealed record SellerTraceEntry(DateTimeOffset At,string Stage,string Message,object? Detail);

// Local, bounded evidence. Export receives a copy; no disk writes on game frames.
public sealed class SellerTrace
{
    private readonly Queue<SellerTraceEntry> entries=new();
    private readonly Dictionary<string,DateTimeOffset> recent=new();
    private readonly object gate=new();
    public const int Capacity=240;
    public string Latest { get; private set; }="No Seller activity recorded yet.";
    public void Add(DateTimeOffset now,string stage,string message,object? detail=null)
    {
        lock(gate){
        Latest=stage+": "+message;
        var key=stage+"\n"+message;
        if(recent.TryGetValue(key,out var at)&&now-at<TimeSpan.FromSeconds(3))return;
        if(recent.Count>=128)recent.Clear();recent[key]=now;
        while(entries.Count>=Capacity)entries.Dequeue();
        entries.Enqueue(new(now,stage,message,detail));
        }
    }
    public SellerTraceEntry[] Snapshot(){lock(gate)return entries.ToArray();}
    public void Clear(){lock(gate){entries.Clear();recent.Clear();Latest="Ready to record the next Seller action.";}}
}

public sealed class SellerBellSession
{
    private bool observed;
    private DateTimeOffset? absentSince;
    public bool Observe(bool atBell,DateTimeOffset now)
    {
        if(atBell){observed=true;absentSince=null;return false;}
        if(!observed)return false;
        absentSince??=now;
        if(now-absentSince.Value<TimeSpan.FromSeconds(1))return false;
        observed=false;absentSince=null;return true;
    }
}

public static class SellerLabels
{
    public static string Selling(int total,int updates,bool uncertain,int skipped=0,bool checking=false)=>total<=0?"0 selling":
        updates>0?$"{total} selling · {updates} {(updates==1?"update":"updates")}":checking?$"{total} selling · checking":uncertain?$"{total} selling · uncertain":skipped>0?$"{total} selling · {skipped} skipped":$"{total} selling · 0 updates";
}
