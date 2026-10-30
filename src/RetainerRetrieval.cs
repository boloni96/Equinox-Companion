using System.Text.Json;
namespace EquinoxCompanion;

public enum RetrievalQuality { Any, NqOnly, HqOnly }
public enum RetrievalMode { FinalItems, AllMaterials, CompletedItems, DirectIngredients }
public sealed record RetrievalEntry(uint ItemId, string Name, int Quantity, uint RecipeId = 0, bool ReadyMaterial = false);
public sealed record RetrievalPreset(string Name, RetrievalEntry[] Items, int Multiplier = 1, bool IncludesSubcrafts = false, bool ArtisanPreset = false, RetrievalMode Mode = RetrievalMode.CompletedItems, Dictionary<uint,uint>? RecipeChoices = null, RetrievalQuality Quality = RetrievalQuality.Any, ulong DestinationRetainer = 0, uint[]? SendCategories = null, EntrustSelection? Entrust = null, string PlanId = "", int CategoryWithdrawQuantity = 1, int CategoryKeepQuantity = 0, bool? FilterPlanCategories = null);
internal sealed record RetrievalRecipe(uint Id,uint ItemId,int Yield,RetrievalEntry[] Ingredients,string Job);
internal sealed record RetrievalPreview(uint ItemId,string Name,int Required,int Held,int? Available)
{
    public int Need=>Math.Max(0,Required-Held);
    public int? Missing=>Available.HasValue?Math.Max(0,Need-Available.Value):null;
}
internal sealed class RetrievalRecipeChoiceException(uint itemId,string name):ArgumentException("Choose the recipe to use for "+name+"."){internal uint ItemId=>itemId;}
internal static class RetainerRetrieval
{
    internal sealed record ArtisanLists(RetrievalPreset[] Lists, string[] Warnings);
    internal static ArtisanLists ReadArtisan(string json, IReadOnlyDictionary<uint,RetrievalRecipe> recipes, IReadOnlyDictionary<uint,string> names)
    {
        using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip});
        var root=doc.RootElement;
        if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("Payload",out var payload)&&payload.ValueKind==JsonValueKind.Object)root=payload;
        var lists=root;
        if(root.ValueKind!=JsonValueKind.Array&&!root.TryGetProperty("NewCraftingLists",out lists))throw new IOException("This Artisan file has no saved crafting lists (NewCraftingLists).");
        static JsonElement Array(JsonElement value)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty("$values",out var values)?values:value;
        var result=new List<RetrievalPreset>();var warnings=new List<string>();
        foreach(var list in Array(lists).EnumerateArray())
        {
            var name=list.TryGetProperty("Name",out var n)?n.GetString()??"Artisan list":"Artisan list";
            try
            {
                var entries=new List<RetrievalEntry>();
                foreach(var item in Array(list.GetProperty("Recipes")).EnumerateArray())
                {
                    if(item.TryGetProperty("ListItemOptions",out var options)&&options.ValueKind==JsonValueKind.Object&&options.TryGetProperty("Skipping",out var skip)&&skip.ValueKind==JsonValueKind.True)continue;
                    var id=item.GetProperty("ID").GetUInt32();var count=item.GetProperty("Quantity").GetInt32();if(count<=0)continue;
                    if(!recipes.TryGetValue(id,out var recipe))throw new IOException("unknown recipe "+id);
                    entries.Add(new(recipe.ItemId,names.GetValueOrDefault(recipe.ItemId,"Item "+recipe.ItemId),checked(count*recipe.Yield),id));
                }
                // Equivalent job recipes can produce the same item. Finished retrieval needs the total,
                // while material expansion still checks ambiguity if no unique recipe was chosen.
                var merged=entries.GroupBy(e=>e.ItemId).Select(g=>new RetrievalEntry(g.Key,g.First().Name,checked(g.Sum(e=>e.Quantity)),g.Select(e=>e.RecipeId).Distinct().Count()==1?g.First().RecipeId:0));
                result.Add(new(name,Combine(merged),Mode:RetrievalMode.DirectIngredients));
            }
            catch(Exception e) when(e is IOException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
            {warnings.Add(name+": "+e.Message);}
        }
        return new(result.ToArray(),warnings.ToArray());
    }
    internal static bool AllowsQuality(bool hq,RetrievalQuality quality)=>quality==RetrievalQuality.Any||(quality==RetrievalQuality.HqOnly)==hq;
    internal static bool IsRetainerStorage(ulong container)=>container is >=10000 and <=10006 or 12001;
    internal const int MaxQuantity=1_000_000, MaxEntries=5000;
    internal static RetrievalEntry[] Combine(IEnumerable<RetrievalEntry> entries)
    {
        var result=new Dictionary<uint,RetrievalEntry>();
        foreach(var e in entries){if(e.ItemId==0||e.Quantity is <1 or >MaxQuantity)throw new ArgumentException("Invalid item or quantity.");
            if(result.TryGetValue(e.ItemId,out var old)){if(old.RecipeId!=0&&e.RecipeId!=0&&old.RecipeId!=e.RecipeId)throw new ArgumentException("Choose one recipe for "+e.Name);result[e.ItemId]=old with {Quantity=checked(old.Quantity+e.Quantity),RecipeId=old.RecipeId==0?e.RecipeId:old.RecipeId,ReadyMaterial=old.ReadyMaterial&&e.ReadyMaterial};}
            else result[e.ItemId]=e;
            if(result[e.ItemId].Quantity>MaxQuantity||result.Count>MaxEntries)throw new ArgumentException("List exceeds 5,000 items or 1,000,000 per item.");}
        return result.Values.ToArray();
    }
    internal sealed record CraftStep(uint RecipeId,uint ItemId,string Name,int Crafts,int Yield);
    internal sealed record CraftPlan(RetrievalEntry[] Materials,CraftStep[] Steps);
    private static RetrievalRecipe? Recipe(RetrievalEntry e,IReadOnlyDictionary<uint,RetrievalRecipe[]> recipes)
    {
        if(!recipes.TryGetValue(e.ItemId,out var choices)||choices.Length==0){if(e.RecipeId!=0)throw new ArgumentException("Saved recipe is unavailable for "+e.Name+". Choose a current recipe.");return null;}
        var same=choices.Select(x=>x.Yield+"|"+string.Join(";",x.Ingredients.OrderBy(i=>i.ItemId).Select(i=>i.ItemId+":"+i.Quantity))).Distinct().Count()==1;
        // Job alternatives using identical ordinary materials may differ only in crystal type.
        // Keep the chosen recipe's actual crystals; genuinely different material recipes need a choice.
        var jobAlternates=choices.All(x=>x.Ingredients.Any(i=>i.ItemId>19))&&choices.Select(x=>x.Yield+"|"+string.Join(";",x.Ingredients.Where(i=>i.ItemId>19).OrderBy(i=>i.ItemId).Select(i=>i.ItemId+":"+i.Quantity))).Distinct().Count()==1;
        var r=e.RecipeId!=0?choices.FirstOrDefault(r=>r.Id==e.RecipeId):(same||jobAlternates)?choices.OrderBy(x=>x.Id).First():null;
        return r??throw new RetrievalRecipeChoiceException(e.ItemId,e.Name);
    }
    internal static RetrievalEntry[] Requested(IEnumerable<RetrievalEntry> entries,int multiplier,bool includesSubcrafts,IReadOnlyDictionary<uint,RetrievalRecipe[]> recipes)
    {
        if(multiplier is <1 or >999)throw new ArgumentException("Multiplier must be 1–999.");
        var listed=Combine(entries.Select(e=>e with{Quantity=checked(e.Quantity*multiplier)}));
        if(!includesSubcrafts)return listed;
        // Explicit opt-in: listed intermediate outputs are consumed by the other listed recipes.
        // Preserve any surplus as a requested output; do not infer intent from a list title.
        var consumed=new Dictionary<uint,int>();
        foreach(var e in listed){var r=Recipe(e,recipes);if(r is null)continue;if(r.Yield<1)throw new ArgumentException("Invalid recipe yield.");
            var crafts=checked((e.Quantity+r.Yield-1)/r.Yield);
            foreach(var i in r.Ingredients)consumed[i.ItemId]=checked(consumed.GetValueOrDefault(i.ItemId)+i.Quantity*crafts);}
        var outputs=listed.Select(e=>e with{Quantity=Math.Max(0,e.Quantity-consumed.GetValueOrDefault(e.ItemId))}).Where(e=>e.Quantity>0).ToArray();
        if(listed.Length>0&&outputs.Length==0)throw new ArgumentException("No final outputs remain. Check the list and its subcraft option.");
        return outputs;
    }
    internal static RetrievalEntry[] WorkshopTurnIns(IEnumerable<RetrievalEntry> entries,IReadOnlyDictionary<uint,RetrievalEntry[]> projects)
    {
        var expanded=new List<RetrievalEntry>();
        foreach(var e in entries)
        {
            if(projects.TryGetValue(e.ItemId,out var supplies))
            {
                if(supplies.Length==0)throw new ArgumentException("Workshop requirements unavailable for "+e.Name);
                expanded.AddRange(supplies.Select(i=>i with{Quantity=checked(i.Quantity*e.Quantity)}));
            }
            else expanded.Add(e);
        }
        return Combine(expanded);
    }
    internal static CraftPlan BuildPlan(IEnumerable<RetrievalEntry> entries,int multiplier,bool includesSubcrafts,RetrievalMode mode,IReadOnlyDictionary<uint,RetrievalRecipe[]> recipes,IReadOnlyDictionary<uint,RetrievalEntry[]> workshop,IReadOnlyDictionary<uint,RetrievalEntry[]>? outfits=null)
    {
        var requested=Requested(entries,multiplier,includesSubcrafts,recipes);
        if(mode==RetrievalMode.CompletedItems)return new(requested,[]);
        if(outfits!=null)requested=WorkshopTurnIns(requested,outfits);
        if(mode==RetrievalMode.AllMaterials)return PlanCrafts(WorkshopTurnIns(requested,workshop),recipes);
        if(mode==RetrievalMode.FinalItems)return new(WorkshopTurnIns(requested,workshop),[]); // Legacy exact-list mode.
        var materials=new List<RetrievalEntry>();var steps=new List<CraftStep>();
        foreach(var e in requested)
        {
            if(workshop.ContainsKey(e.ItemId)){materials.AddRange(WorkshopTurnIns([e],workshop));continue;}
            var r=e.ReadyMaterial?null:Recipe(e,recipes);
            if(r is null){materials.Add(e);continue;}
            if(r.Yield<1)throw new ArgumentException("Invalid recipe yield for "+e.Name);
            var crafts=checked((e.Quantity+r.Yield-1)/r.Yield);
            materials.AddRange(r.Ingredients.Select(i=>i with{Quantity=checked(i.Quantity*crafts)}));
            steps.Add(new(r.Id,e.ItemId,e.Name,crafts,r.Yield));
        }
        return new(Combine(materials),steps.ToArray());
    }
    internal static CraftPlan PlanCrafts(IEnumerable<RetrievalEntry> entries,IReadOnlyDictionary<uint,RetrievalRecipe[]> recipes)
    {
        var leaves=new List<RetrievalEntry>();var spare=new Dictionary<uint,int>();var steps=new List<CraftStep>();
        void Visit(RetrievalEntry e,HashSet<uint> path){
            var r=Recipe(e,recipes);if(r is null){leaves.Add(e with{RecipeId=0});return;}
            if(r.Yield<1||!path.Add(e.ItemId)||path.Count>32)throw new ArgumentException("Invalid or cyclic recipe for "+e.Name);
            var needed=Math.Max(0,e.Quantity-spare.GetValueOrDefault(e.ItemId));spare[e.ItemId]=Math.Max(0,spare.GetValueOrDefault(e.ItemId)-e.Quantity);
            var crafts=checked((needed+r.Yield-1)/r.Yield);spare[e.ItemId]+=crafts*r.Yield-needed;
            if(crafts>0){foreach(var i in r.Ingredients)Visit(i with{Quantity=checked(i.Quantity*crafts)},new(path));steps.Add(new(r.Id,e.ItemId,e.Name,crafts,r.Yield));}
        }
        foreach(var e in Combine(entries))Visit(e,[]);
        return new(Combine(leaves),steps.GroupBy(s=>s.RecipeId).Select(g=>g.First() with{Crafts=checked(g.Sum(s=>s.Crafts))}).ToArray());
    }
    internal static RetrievalEntry[] Expand(IEnumerable<RetrievalEntry> entries,int multiplier,RetrievalMode mode,IReadOnlyDictionary<uint,RetrievalRecipe[]> recipes)
    {
        var requested=Requested(entries,multiplier,false,recipes);
        return mode!=RetrievalMode.AllMaterials?requested:PlanCrafts(requested,recipes).Materials;
    }
    internal static string ExportArtisan(string name,CraftStep[] steps)
    {
        if(steps.Length==0)throw new ArgumentException("There are no craftable items in this selection.");
        // Artisan's documented UI import reads plain NewCraftingList JSON and assigns its own ID.
        return JsonSerializer.Serialize(new{Name=name,ID=0,Recipes=steps.Select(s=>new{ID=s.RecipeId,Quantity=s.Crafts,ListItemOptions=new{Skipping=false}}).ToArray(),SkipIfEnough=false,IsPremade=false},new JsonSerializerOptions{WriteIndented=true});
    }
    internal static string Export(RetrievalPreset list)=>JsonSerializer.Serialize(new {Format="EquinoxRetrieval",Version=1,List=list},new JsonSerializerOptions{WriteIndented=true});
    internal static RetrievalPreset Import(string text,Func<string,RetrievalEntry?> resolve, Func<uint[]>? protectedItems = null)
    {
        if(text.Length>2_000_000)throw new ArgumentException("Import is limited to 2,000,000 characters.");
        if(text.TrimStart().StartsWith('{')){using var doc=JsonDocument.Parse(text);var root=doc.RootElement;if(root.TryGetProperty("EntrustItems",out _))return EntrustSelection.Import(root,resolve,protectedItems);if(root.GetProperty("Format").GetString()!="EquinoxRetrieval"||root.GetProperty("Version").GetInt32()!=1)throw new ArgumentException("Unsupported preset format.");var p=root.GetProperty("List").Deserialize<RetrievalPreset>()??throw new ArgumentException("Missing list.");if(string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>100)throw new ArgumentException("List name must be 1–100 characters.");if(p.Multiplier is <1 or >999)throw new ArgumentException("Invalid preset multiplier.");return p with{Items=Combine(p.Items.Select(e=>{var found=resolve(e.ItemId.ToString())??throw new ArgumentException("Unknown item ID: "+e.ItemId);return e with{Name=found.Name};}))};}
        var parsed=MarketShopping.Parse(text);if(parsed.Errors.Length>0)throw new ArgumentException(string.Join("\n",parsed.Errors));
        return new("Imported list",Combine(parsed.Items.Select(e=>(resolve(e.Name)??throw new ArgumentException("Unknown or ambiguous item: "+e.Name)) with{Quantity=e.Quantity})));
    }
}
internal sealed record RetrievalSlot(uint ItemId,int Quantity,int Container,int Slot,bool Hq=false);
internal interface IRetrievalPort
{
    bool ContextMatches {get;}
    void Begin(IEnumerable<uint> items) {}
    bool Prepare();
    bool Select(ulong retainer);
    bool OpenInventory(ulong retainer);
    RetrievalSlot[]? Read(ulong retainer);
    int Held(uint item);
    bool Withdraw(ulong retainer,RetrievalSlot slot,int quantity);
    bool Close(ulong retainer);
    void Confirmed(ulong retainer,RetrievalSlot slot,int quantity) {}
    void Release();
}
internal sealed class RetrievalRun(IRetrievalPort port)
{
    internal enum Phase{Idle,Prepare,Select,Open,Read,Withdraw,Confirm,Close}
    internal Phase State{get;private set;}
    internal bool Running=>State!=Phase.Idle;
    internal string Status{get;private set;}="Ready";
    private HashSet<uint> optionalCategoryItems=[];private bool allAvailable;
    private ulong[] retainers=[];private int index,before,quantity,knownHolders;private RetrievalSlot? slot;private Dictionary<uint,int> targets=[];private DateTimeOffset deadline;
    internal void Start(ulong[] ids,RetrievalEntry[] plan,bool subtractHeld,DateTimeOffset now,int preferredCount=-1,IEnumerable<uint>? categoryItems=null,bool allAvailable=false){if(Running)throw new InvalidOperationException("Retrieval already running.");if(ids.Length==0||plan.Length==0)throw new ArgumentException("Select items and open the retainer list first.");port.Begin(plan.Select(e=>e.ItemId));this.allAvailable=allAvailable;optionalCategoryItems=(categoryItems??[]).ToHashSet();retainers=ids;knownHolders=preferredCount<0?ids.Length:Math.Clamp(preferredCount,0,ids.Length);index=0;targets=plan.ToDictionary(e=>e.ItemId,e=>allAvailable?int.MaxValue:checked((subtractHeld?0:port.Held(e.ItemId))+e.Quantity));Transition(Phase.Prepare,now);}
    internal void Stop(string reason){State=Phase.Idle;Status=reason;port.Release();}
    private void Transition(Phase phase,DateTimeOffset now){State=phase;deadline=now.AddSeconds(20);Status=index<knownHolders?phase+" · known holder "+(index+1)+"/"+knownHolders:phase+" · checking missing stock "+(index-knownHolders+1)+"/"+(retainers.Length-knownHolders);}
    internal void Tick(DateTimeOffset now){if(!Running)return;try{
        if(!port.ContextMatches){Stop("Stopped: character, world or bell changed.");return;}if(now>deadline){Stop("Stopped: "+State+" was not confirmed. Check the game window.");return;}
        var id=retainers[index];switch(State){
        case Phase.Prepare:if(targets.All(x=>port.Held(x.Key)>=x.Value)){Stop("Complete · requested quantities are already available.");return;}if(port.Prepare())Transition(Phase.Select,now);break;
        case Phase.Select:if(port.Select(id))Transition(Phase.Open,now);break;
        case Phase.Open:if(port.OpenInventory(id))Transition(Phase.Read,now);break;
        case Phase.Read:
            var stock=port.Read(id);if(stock is null)return;slot=stock.FirstOrDefault(s=>targets.TryGetValue(s.ItemId,out var target)&&port.Held(s.ItemId)<target);
            if(slot is null){Transition(Phase.Close,now);break;}before=port.Held(slot.ItemId);quantity=Math.Min(slot.Quantity,targets[slot.ItemId]-before);Transition(Phase.Withdraw,now);break;
        case Phase.Withdraw:if(port.Withdraw(id,slot!,quantity))Transition(Phase.Confirm,now);break;
        case Phase.Confirm:if(port.Held(slot!.ItemId)>=before+quantity){Transition(Phase.Read,now);port.Confirmed(id,slot,quantity);}break;
        case Phase.Close:if(port.Close(id)){if(targets.All(x=>port.Held(x.Key)>=x.Value)){Stop("Complete · requested quantities received in bags / Armoury Chest.");return;}if(++index>=retainers.Length){Stop(allAvailable?"Complete · all available matching items withdrawn (subject to inventory capacity).":targets.Where(x=>!optionalCategoryItems.Contains(x.Key)).All(x=>port.Held(x.Key)>=x.Value)?"Complete · available category items withdrawn.":"Finished checking retainers · some requested items are still missing.");return;}Transition(Phase.Select,now);}break;
        }
    }catch(Exception e){Stop("Stopped: "+e.Message);}}
}


