namespace EquinoxCompanion;
internal sealed record RoutineStatusEntry(DateTimeOffset At,string Routine,bool Active,string Detail);
internal sealed class RoutineStatusHistory
{
    private readonly Dictionary<string,(bool Active,string Detail)> last=[];
    private readonly Queue<RoutineStatusEntry> entries=[];
    internal long Dropped{get;private set;}
    internal RoutineStatusEntry[] Entries=>entries.ToArray();
    internal void Observe(DateTimeOffset now,string routine,bool active,string? detail)
    {
        detail=string.IsNullOrWhiteSpace(detail)?"No status reason was supplied by this routine.":detail.Trim();
        if(last.TryGetValue(routine,out var previous)){
            if(previous.Active==active&&previous.Detail==detail)return;
            if(!previous.Active&&!active){last[routine]=(active,detail);return;}
            if(previous.Active&&!active&&previous.Detail==detail)
                detail="Routine ended without a new reason. Last reported state: "+detail;
        }else if(!active){last[routine]=(active,detail);return;}
        // Keep comparison against the original status, not the explanatory fallback.
        last[routine]=(active,string.IsNullOrWhiteSpace(detail)?"No status reason was supplied by this routine.":detail);
        while(entries.Count>=200){entries.Dequeue();Dropped++;}
        entries.Enqueue(new(now,routine,active,detail));
    }
}
