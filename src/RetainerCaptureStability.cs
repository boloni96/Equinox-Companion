namespace EquinoxCompanion;
// Never carry a candidate across characters, retainers, closed windows or incomplete reads.
public sealed class RetainerCaptureStability
{
    private ulong character,retainer;
    private ChestStack[]? candidate;
    private DateTimeOffset since;
    public void Reset(){character=retainer=0;candidate=null;since=default;}
    public bool Ready(ulong owner,ulong selected,ChestStack[] slots,int used,DateTimeOffset now)
    {
        if(owner==0||selected==0||!StorageVisit.RecordedMatches(slots,used)){Reset();return false;}
        if(character!=owner||retainer!=selected||candidate==null||!slots.SequenceEqual(candidate)){
            character=owner;retainer=selected;candidate=slots.ToArray();since=now;return false;
        }
        return now-since>=TimeSpan.FromSeconds(1);
    }
}
