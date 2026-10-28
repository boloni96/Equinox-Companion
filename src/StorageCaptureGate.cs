namespace EquinoxCompanion;
public readonly record struct StorageSlotValue(uint Item,int Quantity,bool Hq);
public sealed class StorageCaptureGate
{
    private readonly Dictionary<string,(Actor Actor,StorageSlotValue[] Slots,DateTimeOffset At)> last=[];
    public bool Needs(Actor actor,string key,ReadOnlySpan<StorageSlotValue> slots,DateTimeOffset now)=>
        !last.TryGetValue(key,out var old)||old.Actor!=actor||now-old.At>=TimeSpan.FromMinutes(30)||!slots.SequenceEqual(old.Slots);
    public void Remember(Actor actor,string key,ReadOnlySpan<StorageSlotValue> slots,DateTimeOffset now)=>last[key]=(actor,slots.ToArray(),now);
    public void Clear()=>last.Clear();
}
