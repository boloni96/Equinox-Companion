namespace EquinoxCompanion;
internal sealed class ChestSnapshotGate
{
    private (ulong Character,ulong Company,uint World,uint Page) identity;
    private ChestStack[]? sample;
    private DateTimeOffset since;
    internal void Reset(){sample=null;since=default;}
    internal bool Observe(ulong character,ulong company,uint world,uint page,ChestStack[] slots,DateTimeOffset now)
    {
        var current=(character,company,world,page);
        if(sample==null||identity!=current||!sample.AsSpan().SequenceEqual(slots)){
            identity=current;sample=slots.ToArray();since=now;return false;
        }
        return now-since>=TimeSpan.FromMilliseconds(750);
    }
}
