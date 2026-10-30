namespace EquinoxCompanion;
// Never carry a candidate across characters, retainers, closed windows or incomplete reads.
public sealed class RetainerCaptureStability
{
    private ulong character,retainer;
    private ChestStack[]? candidate;
    private DateTimeOffset since;
    private bool verified;
    public void ConfirmTransfer(ulong owner,ulong selected){if(owner==0||selected==0)return;if(character!=owner||retainer!=selected){Reset();character=owner;retainer=selected;}verified=true;candidate=null;}
    public void Reset(){character=retainer=0;candidate=null;since=default;verified=false;}
    public bool Ready(ulong owner,ulong selected,ChestStack[] slots,int used,DateTimeOffset now)
    {
        if(character!=owner||retainer!=selected)Reset();
        var occupied=slots.Count(x=>x.Container is >=10000 and <=10006&&x.Item>0);
        if(owner==0||selected==0||!StorageVisit.RecordedMatches(slots,verified?occupied:used)){Reset();return false;}
        if(character!=owner||retainer!=selected||candidate==null||!slots.SequenceEqual(candidate)){
            character=owner;retainer=selected;candidate=slots.ToArray();since=now;return false;
        }
        if(now-since<TimeSpan.FromSeconds(1))return false;
        verified=true;return true;
    }
}
