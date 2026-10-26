using System.Text.Json;
using System.Text.RegularExpressions;
namespace EquinoxCompanion;

public sealed record RetainerMarketItem(int Slot,uint ItemId,string Name,bool Hq,uint Quantity,int Price,DateTimeOffset ListedAt);
public sealed record RetainerMarketRow(string Id,string Name,int Order,uint Gil,DateTimeOffset ObservedAt,DateTimeOffset? LastVisit,RetainerMarketItem[] Items,uint? JobId=null,int? Level=null,string Job="",DateTimeOffset? ProfileAt=null);
public sealed record RetainerMarketDetails(RetainerMarketRow[] Retainers);

public static class RetainerMarketSnapshot
{
    private static bool Text(string? value,int length)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=length&&!value.Any(char.IsControl);
    private static bool At(DateTimeOffset at,DateTimeOffset now)=>at.Year>=2020&&at<=now.AddMinutes(5);
    public static bool Valid(RetainerMarketDetails? value,DateTimeOffset now)=>value?.Retainers is {Length:>0 and <=10} rows&&
        rows.All(r=>r!=null&&Regex.IsMatch(r.Id??"","^[1-9]\\d{0,19}$")&&Text(r.Name,80)&&r.Order is >=0 and <=9&&At(r.ObservedAt,now)&&
            (r.LastVisit==null||At(r.LastVisit.Value,now))&&r.JobId is null or <=255&&r.Level is null or >=0 and <=200&&(r.Job??"").Length<=100&&(r.ProfileAt==null||At(r.ProfileAt.Value,now))&&r.Items is {Length:<=20}&&r.Items.All(i=>i!=null&&i.Slot is >=0 and <20&&i.ItemId is >0 and <1000000&&Text(i.Name,100)&&i.Quantity is >0 and <=9999&&i.Price is >0 and <=999999999&&At(i.ListedAt,now))&&r.Items.Select(i=>i.Slot).Distinct().Count()==r.Items.Length)&&rows.Select(r=>r.Id).Distinct().Count()==rows.Length;
}

// Bounded current state, not a second activity history. Replacing an unsent
// snapshot keeps only the newest observed state for each character/retainer.
public sealed class RetainerMarketOutbox
{
    public string PairingScope {get;set;}="";
    public Dictionary<string,SyncEvent> Snapshots {get;set;}=[];
    public Dictionary<string,string> Receipts {get;set;}=[];
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<SyncEvent> Pending=>Snapshots.Where(x=>Receipts.GetValueOrDefault(x.Key)!=x.Value.Id).Select(x=>x.Value);
    public bool Scope(string scope){if(PairingScope==scope)return false;PairingScope=scope;Receipts.Clear();return true;}
    public bool Put(Actor actor,RetainerMarketDetails details,DateTimeOffset now)
    {
        if(!SyncValidation.ActorReady(actor)||!RetainerMarketSnapshot.Valid(details,now))return false;
        var changed=false;
        // Each retainer has its own envelope: even 20 Unicode item names fit the
        // upload budget and a busy retainer cannot duplicate unchanged siblings.
        foreach(var row in details.Retainers){
            var key=actor.ContentId+":"+row.Id;var one=new RetainerMarketDetails([row]);
            if(Snapshots.TryGetValue(key,out var old)&&old.Actor==actor&&JsonSerializer.Serialize(old.Market)==JsonSerializer.Serialize(one))continue;
            Snapshots[key]=new(Guid.NewGuid().ToString("N"),"market.retainers",now,actor,null,Market:one);Receipts.Remove(key);changed=true;
        }
        return changed;
    }
    public bool Acknowledge(string id)
    {
        foreach(var pair in Snapshots)if(pair.Value.Id==id){Receipts[pair.Key]=id;return true;}
        return false;
    }
    public RetainerMarketOutbox Snapshot()=>new(){PairingScope=PairingScope,Snapshots=new(Snapshots),Receipts=new(Receipts)};
}
