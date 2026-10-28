using System.Text.Json;
namespace EquinoxCompanion;

public enum RetrievalMode { FinalItems, AllMaterials }
public sealed record RetrievalEntry(uint ItemId, string Name, int Quantity, uint RecipeId = 0);
public sealed record RetrievalPreset(string Name, RetrievalEntry[] Items, int Multiplier = 1);
internal sealed record RetrievalRecipe(uint Id,uint ItemId,int Yield,RetrievalEntry[] Ingredients,string Job);
internal sealed record RetrievalPreview(uint ItemId,string Name,int Required,int Held,int? Available)
{
    public int Need=>Math.Max(0,Required-Held);
    public int? Missing=>Available.HasValue?Math.Max(0,Need-Available.Value):null;
}
internal static class RetainerRetrieval
{
    internal const int MaxQuantity=1_000_000, MaxEntries=200;
    internal static RetrievalEntry[] Combine(IEnumerable<RetrievalEntry> entries)
    {
        var result=new Dictionary<uint,RetrievalEntry>();
        foreach(var e in entries){if(e.ItemId==0||e.Quantity is <1 or >MaxQuantity)throw new ArgumentException("Invalid item or quantity.");
            if(result.TryGetValue(e.ItemId,out var old)){if(old.RecipeId!=0&&e.RecipeId!=0&&old.RecipeId!=e.RecipeId)throw new ArgumentException("Choose one recipe for "+e.Name);result[e.ItemId]=old with {Quantity=checked(old.Quantity+e.Quantity),RecipeId=old.RecipeId==0?e.RecipeId:old.RecipeId};}
            else result[e.ItemId]=e;
            if(result[e.ItemId].Quantity>MaxQuantity||result.Count>MaxEntries)throw new ArgumentException("List exceeds 200 items or 1,000,000 per item.");}
        return result.Values.ToArray();
    }
    internal static RetrievalEntry[] Expand(IEnumerable<RetrievalEntry> entries,int multiplier,RetrievalMode mode,IReadOnlyDictionary<uint,RetrievalRecipe[]> recipes)
    {
        if(multiplier is <1 or >999)throw new ArgumentException("Multiplier must be 1–999.");
        var requested=Combine(entries.Select(e=>e with {Quantity=checked(e.Quantity*multiplier)}));
        if(mode==RetrievalMode.FinalItems)return requested;
        var leaves=new List<RetrievalEntry>();var spare=new Dictionary<uint,int>();
        void Visit(RetrievalEntry e,HashSet<uint> path){
            if(!recipes.TryGetValue(e.ItemId,out var choices)||choices.Length==0){leaves.Add(e with{RecipeId=0});return;}
            var r=e.RecipeId!=0?choices.FirstOrDefault(r=>r.Id==e.RecipeId):choices.Select(x=>x.Yield+"|"+string.Join(";",x.Ingredients.OrderBy(i=>i.ItemId).Select(i=>i.ItemId+":"+i.Quantity))).Distinct().Count()==1?choices[0]:null;
            if(r is null)throw new ArgumentException("Multiple recipes for "+e.Name+". Select an explicit top-level recipe, or use Final items with an explicit material list.");
            if(r.Yield<1||!path.Add(e.ItemId)||path.Count>32)throw new ArgumentException("Invalid or cyclic recipe for "+e.Name);
            var needed=Math.Max(0,e.Quantity-spare.GetValueOrDefault(e.ItemId));spare[e.ItemId]=Math.Max(0,spare.GetValueOrDefault(e.ItemId)-e.Quantity);
            var crafts=checked((needed+r.Yield-1)/r.Yield);spare[e.ItemId]+=crafts*r.Yield-needed;
            if(crafts>0)foreach(var i in r.Ingredients)Visit(i with{Quantity=checked(i.Quantity*crafts)},new(path));
        }
        foreach(var e in requested)Visit(e,[]);return Combine(leaves);
    }
    internal static string Export(RetrievalPreset list)=>JsonSerializer.Serialize(new {Format="EquinoxRetrieval",Version=1,List=list},new JsonSerializerOptions{WriteIndented=true});
    internal static RetrievalPreset Import(string text,Func<string,RetrievalEntry?> resolve)
    {
        if(text.Length>100_000)throw new ArgumentException("Import is limited to 100,000 characters.");
        if(text.TrimStart().StartsWith('{')){using var doc=JsonDocument.Parse(text);var root=doc.RootElement;if(root.GetProperty("Format").GetString()!="EquinoxRetrieval"||root.GetProperty("Version").GetInt32()!=1)throw new ArgumentException("Unsupported preset format.");var p=root.GetProperty("List").Deserialize<RetrievalPreset>()??throw new ArgumentException("Missing list.");if(string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>100)throw new ArgumentException("List name must be 1–100 characters.");if(p.Multiplier is <1 or >999)throw new ArgumentException("Invalid preset multiplier.");return p with{Items=Combine(p.Items.Select(e=>{var found=resolve(e.ItemId.ToString())??throw new ArgumentException("Unknown item ID: "+e.ItemId);return e with{Name=found.Name};}))};}
        var parsed=MarketShopping.Parse(text);if(parsed.Errors.Length>0)throw new ArgumentException(string.Join("\n",parsed.Errors));
        return new("Imported list",Combine(parsed.Items.Select(e=>(resolve(e.Name)??throw new ArgumentException("Unknown or ambiguous item: "+e.Name)) with{Quantity=e.Quantity})));
    }
}
internal sealed record RetrievalSlot(uint ItemId,int Quantity,int Container,int Slot);
internal interface IRetrievalPort
{
    bool ContextMatches {get;}
    bool Prepare();
    bool Select(ulong retainer);
    bool OpenInventory(ulong retainer);
    RetrievalSlot[]? Read(ulong retainer);
    int Held(uint item);
    bool Withdraw(ulong retainer,RetrievalSlot slot,int quantity);
    bool Close(ulong retainer);
    void Release();
}
internal sealed class RetrievalRun(IRetrievalPort port)
{
    internal enum Phase{Idle,Prepare,Select,Open,Read,Withdraw,Confirm,Close}
    internal Phase State{get;private set;}
    internal bool Running=>State!=Phase.Idle;
    internal string Status{get;private set;}="Ready";
    private ulong[] retainers=[];private int index,before,quantity;private RetrievalSlot? slot;private Dictionary<uint,int> targets=[];private DateTimeOffset deadline;
    internal void Start(ulong[] ids,RetrievalEntry[] plan,bool subtractHeld,DateTimeOffset now){if(Running)throw new InvalidOperationException("Retrieval already running.");if(ids.Length==0||plan.Length==0)throw new ArgumentException("Select items and open the retainer list first.");retainers=ids;index=0;targets=plan.ToDictionary(e=>e.ItemId,e=>checked((subtractHeld?0:port.Held(e.ItemId))+e.Quantity));Transition(Phase.Prepare,now);}
    internal void Stop(string reason){State=Phase.Idle;Status=reason;port.Release();}
    private void Transition(Phase phase,DateTimeOffset now){State=phase;deadline=now.AddSeconds(20);Status=phase+" · retainer "+(index+1)+"/"+retainers.Length;}
    internal void Tick(DateTimeOffset now){if(!Running)return;try{
        if(!port.ContextMatches){Stop("Stopped: character, world or bell changed.");return;}if(now>deadline){Stop("Stopped: "+State+" was not confirmed. Check the game window.");return;}
        var id=retainers[index];switch(State){
        case Phase.Prepare:if(port.Prepare())Transition(Phase.Select,now);break;
        case Phase.Select:if(port.Select(id))Transition(Phase.Open,now);break;
        case Phase.Open:if(port.OpenInventory(id))Transition(Phase.Read,now);break;
        case Phase.Read:
            var stock=port.Read(id);if(stock is null)return;slot=stock.FirstOrDefault(s=>targets.TryGetValue(s.ItemId,out var target)&&port.Held(s.ItemId)<target);
            if(slot is null){Transition(Phase.Close,now);break;}before=port.Held(slot.ItemId);quantity=Math.Min(slot.Quantity,targets[slot.ItemId]-before);Transition(Phase.Withdraw,now);break;
        case Phase.Withdraw:if(port.Withdraw(id,slot!,quantity))Transition(Phase.Confirm,now);break;
        case Phase.Confirm:if(port.Held(slot!.ItemId)>=before+quantity)Transition(Phase.Read,now);break;
        case Phase.Close:if(port.Close(id)){if(targets.All(x=>port.Held(x.Key)>=x.Value)){Stop("Complete · requested quantities are in your bags.");return;}if(++index>=retainers.Length){Stop("Finished checking retainers · some requested items are still missing.");return;}Transition(Phase.Select,now);}break;
        }
    }catch(Exception e){Stop("Stopped: "+e.Message);}}
}
