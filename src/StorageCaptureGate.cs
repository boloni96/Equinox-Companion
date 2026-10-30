namespace EquinoxCompanion;
public readonly record struct StorageSlotValue(uint Item,int Quantity,bool Hq);
public sealed class StorageCaptureGate
{
    public static bool IsBag(string key)=>key is "bag:0" or "bag:1" or "bag:2" or "bag:3";
    public static TimeSpan RefreshInterval(string key)=>TimeSpan.FromMinutes(IsBag(key)?10:30);
    public static bool FreshSnapshotRequested(string key,DateTimeOffset observedAt,DateTimeOffset requestedAt)=>IsBag(key)&&requestedAt!=default&&observedAt<requestedAt;
    public void InvalidateBags(){foreach(var key in last.Keys.Where(IsBag).ToArray())last.Remove(key);}
    private readonly Dictionary<string,(Actor Actor,StorageSlotValue[] Slots,DateTimeOffset At)> last=[];
    public bool Needs(Actor actor,string key,ReadOnlySpan<StorageSlotValue> slots,DateTimeOffset now)=>
        !last.TryGetValue(key,out var old)||old.Actor!=actor||now-old.At>=RefreshInterval(key)||!slots.SequenceEqual(old.Slots);
    public void Remember(Actor actor,string key,ReadOnlySpan<StorageSlotValue> slots,DateTimeOffset now)=>last[key]=(actor,slots.ToArray(),now);
    public void Clear()=>last.Clear();
}
