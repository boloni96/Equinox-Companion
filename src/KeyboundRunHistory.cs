namespace EquinoxCompanion;

public sealed record KeyboundRunEvent(DateTimeOffset At,string Session,string Run,string Source,string Kind,
    ulong Character,string Status,uint? Item,string? ItemName,int? Before,int? Held,int? UnlockState,
    DateTimeOffset? Deadline,string[] Windows,string[] Conditions);
public sealed class KeyboundRunHistory
{
    public const int Limit=512;
    private readonly Queue<KeyboundRunEvent> entries=[];
    public void Add(KeyboundRunEvent entry){while(entries.Count>=Limit)entries.Dequeue();entries.Enqueue(entry);}
    public void Restore(IEnumerable<KeyboundRunEvent> history){foreach(var entry in history.TakeLast(Limit))Add(entry);}
    public KeyboundRunEvent[] Snapshot()=>entries.ToArray();
}
