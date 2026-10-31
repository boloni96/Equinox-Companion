using System.Text.Json;
namespace EquinoxCompanion;

// A durable intent is written before touching the purchase UI. An uncertain
// purchase stays reserved across Stop, reload and character switches.
public sealed record KeyboundPurchaseReceipt(ulong Character,uint Item,int Cost,uint Balance,int Held,bool Confirmed=false);
public sealed class KeyboundPurchaseBook
{
    private readonly string path;
    public List<KeyboundPurchaseReceipt> Entries { get; private set; }
    public KeyboundPurchaseBook(string path){
        this.path=path;
        Entries=File.Exists(path)?JsonSerializer.Deserialize<List<KeyboundPurchaseReceipt>>(File.ReadAllText(path))??throw new IOException("Empty purchase history."):[];
        if(Entries.Any(e=>e.Character==0||e.Item==0||e.Cost<=0)||Entries.GroupBy(e=>(e.Character,e.Item)).Any(g=>g.Count()>1))throw new IOException("Invalid purchase history.");
    }
    public KeyboundPurchaseReceipt? Find(ulong character,uint item)=>Entries.FirstOrDefault(e=>e.Character==character&&e.Item==item);
    public void Put(KeyboundPurchaseReceipt receipt){
        if(receipt.Character==0||receipt.Item==0||receipt.Cost<=0)throw new ArgumentException("Invalid receipt.");
        var next=Entries.Where(e=>e.Character!=receipt.Character||e.Item!=receipt.Item).Append(receipt).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){
            JsonSerializer.Serialize(stream,next);stream.Flush(true);
        }
        File.Move(temp,path,true);Entries=next;
    }
}
public static class KeyboundBuyingPolicy
{
    public static bool Confirmed(KeyboundPurchaseReceipt intent,uint balance,int held)=>
        intent.Balance>=intent.Cost&&balance==intent.Balance-intent.Cost&&held>=intent.Held+1;
    public static bool ExactCost(uint currency,uint count,int expected,int received,int quantity)=>currency==52322&&count==expected&&expected>0&&received==1&&quantity==1;
    public static string MenuName(string text)=>KeyboundShoppingPolicy.Normalize(text).TrimEnd('.','…').Trim();
    public static bool CategoryMatches(string choice,string shop){
        var a=MenuName(choice);var b=MenuName(shop);
        if(a.Length==0||b.Length==0)return false;
        if(a==b)return true;
        var open=b.LastIndexOf('(');
        return open>=0&&b.EndsWith(')')&&a==b[(open+1)..^1].Trim();
    }
}
