namespace EquinoxCompanion;

internal enum PreparationCoverage { Bags, Retainers, Craftable, Missing, Incomplete }
internal sealed record PreparationRow(uint ItemId,string Name,int Required,int Bags,int Retainers,int CraftQuantity,int Missing,bool Complete,string[] UsedFor,PreparationCoverage Coverage);
internal sealed record PreparationPlan(PreparationRow[] Rows,RetainerRetrieval.CraftStep[] Steps,RetrievalEntry[] Withdraw,RetrievalEntry[] Missing);
internal static class CraftingPreparation
{
    internal static RetainerRetrieval.CraftStep[] ArtisanTargets(PreparationPlan plan)
    {
        var required=plan.Rows.ToDictionary(r=>r.ItemId,r=>r.Required);
        return plan.Steps.Select(s=>s with{Crafts=checked((required[s.ItemId]+s.Yield-1)/s.Yield)}).ToArray();
    }
    // Aggregate demand before allocating stock, so shared subcrafts and recipe yields are counted once.
    internal static PreparationPlan Build(IEnumerable<RetrievalEntry> outputs,IReadOnlyDictionary<uint,RetrievalRecipe[]> recipes,IReadOnlyDictionary<uint,int> bags,IReadOnlyDictionary<uint,int> retainers,bool complete)
    {
        var roots=RetainerRetrieval.Combine(outputs);var entries=new Dictionary<uint,RetrievalEntry>();
        var chosen=new Dictionary<uint,RetrievalRecipe?>();var order=new List<uint>();var visiting=new HashSet<uint>();
        void Discover(RetrievalEntry e,int depth)
        {
            if(depth>32||visiting.Contains(e.ItemId))throw new ArgumentException("Cyclic crafting recipe: "+e.Name);
            if(entries.TryGetValue(e.ItemId,out var old)){
                if(e.RecipeId!=0&&chosen[e.ItemId]?.Id!=e.RecipeId)throw new ArgumentException("Conflicting recipes for "+old.Name);
                return;
            }
            entries[e.ItemId]=e;visiting.Add(e.ItemId);var recipe=RetainerRetrieval.Recipe(e,recipes);chosen[e.ItemId]=recipe;
            if(recipe!=null){if(recipe.Yield<1)throw new ArgumentException("Invalid recipe yield.");foreach(var i in recipe.Ingredients)Discover(i,depth+1);}
            visiting.Remove(e.ItemId);order.Add(e.ItemId);
        }
        foreach(var root in roots)Discover(root,0);
        var demand=roots.ToDictionary(x=>x.ItemId,x=>x.Quantity);var used=new Dictionary<uint,HashSet<string>>();
        var rows=new Dictionary<uint,PreparationRow>();var crafts=new Dictionary<uint,RetainerRetrieval.CraftStep>();
        foreach(var id in order.AsEnumerable().Reverse()){
            var e=entries[id];var need=demand.GetValueOrDefault(id);if(need==0)continue;
            if(need>RetainerRetrieval.MaxQuantity)throw new ArgumentException("Crafting quantity exceeds limit.");
            var bag=Math.Min(need,Math.Max(0,bags.GetValueOrDefault(id)));var ret=Math.Min(need-bag,Math.Max(0,retainers.GetValueOrDefault(id)));
            var remaining=need-bag-ret;var recipe=chosen[id];var made=0;
            if(remaining>0&&recipe!=null){
                var count=checked((remaining+recipe.Yield-1)/recipe.Yield);made=checked(count*recipe.Yield);
                crafts[id]=new(recipe.Id,id,e.Name,count,recipe.Yield);
                foreach(var i in recipe.Ingredients){demand[i.ItemId]=checked(demand.GetValueOrDefault(i.ItemId)+count*i.Quantity);if(!used.TryGetValue(i.ItemId,out var parents))used[i.ItemId]=parents=[];parents.Add(e.Name);}
            }
            rows[id]=new(id,e.Name,need,bag,ret,made,recipe==null?remaining:0,complete,[],bag==need?PreparationCoverage.Bags:bag+ret==need?PreparationCoverage.Retainers:PreparationCoverage.Craftable);
        }
        // Children precede parents: shortages propagate through every affected recipe.
        foreach(var id in order){if(!rows.TryGetValue(id,out var row))continue;
            var blocked=row.Missing>0||(crafts.ContainsKey(id)&&chosen[id]!.Ingredients.Any(i=>rows.TryGetValue(i.ItemId,out var child)&&child.Coverage is PreparationCoverage.Missing or PreparationCoverage.Incomplete));
            rows[id]=row with{UsedFor=used.TryGetValue(id,out var parents)?parents.Order().ToArray():["Requested output"],Coverage=blocked?(complete?PreparationCoverage.Missing:PreparationCoverage.Incomplete):row.Coverage};
        }
        return new(rows.Values.ToArray(),order.Where(crafts.ContainsKey).Select(id=>crafts[id]).ToArray(),rows.Values.Where(r=>r.Retainers>0).Select(r=>new RetrievalEntry(r.ItemId,r.Name,r.Retainers)).ToArray(),rows.Values.Where(r=>r.Missing>0).Select(r=>new RetrievalEntry(r.ItemId,r.Name,r.Missing)).ToArray());
    }
}

public sealed class WorkshopQueueSave
{
    public ulong Character {get;set;}
    public uint World {get;set;}
    public string House {get;set;}="";
    public RetrievalEntry[] Items {get;set;}=[];
    public int Completed {get;set;}
    public uint ReadyToCollect {get;set;}
    public uint PendingDeposit {get;set;}
    public uint PendingCollection {get;set;}
    public int CollectionBefore {get;set;}
}
internal sealed record WorkshopSupply(uint ItemId,string Name,int SetSize,int SetsRequired,int SetsDelivered=0)
{
    internal int RemainingSets=>Math.Max(0,SetsRequired-SetsDelivered);
    internal int RemainingQuantity=>checked(RemainingSets*SetSize);
}
internal sealed record WorkshopProject(uint Sequence,uint ItemId,string Name,WorkshopSupply[][] Phases);
internal sealed record WorkshopPhase(uint ItemId,int Phase,int TotalPhases,WorkshopSupply[] Rows);
internal static class WorkshopPolicy
{
    internal static (RetrievalEntry[] Items,RetrievalQuality Quality)[] WithdrawalBatches(RetrievalEntry[] nq,RetrievalEntry[] hq,int filter)
    {
        if(filter is <0 or >2)throw new ArgumentException("Unknown quality filter.");
        var result=new List<(RetrievalEntry[],RetrievalQuality)>();
        if(filter!=2&&nq.Length>0)result.Add((nq,RetrievalQuality.NqOnly));
        if(filter!=1&&hq.Length>0)result.Add((hq,RetrievalQuality.HqOnly));
        return result.ToArray();
    }
    internal static void Clear(WorkshopQueueSave queue,uint world,string house)
    {
        queue.Items=[];queue.Completed=0;queue.PendingCollection=0;queue.ReadyToCollect=0;queue.CollectionBefore=0;queue.PendingDeposit=0;
        queue.World=world;queue.House=house;
    }
    internal static string NormalizePrompt(string text)=>string.Join(" ",text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    internal static bool ContributionPrompt(string prompt,int quantity,string name,string plural)
    {
        prompt=NormalizePrompt(prompt);name=NormalizePrompt(name);plural=NormalizePrompt(plural);
        return prompt.StartsWith("Contribute "+quantity+" ",StringComparison.Ordinal)&&prompt.EndsWith(" to the company project?",StringComparison.Ordinal)&&
            (name.Length>0&&prompt.Contains(name,StringComparison.OrdinalIgnoreCase)||plural.Length>0&&prompt.Contains(plural,StringComparison.OrdinalIgnoreCase));
    }
    internal static (bool Hq,int Quantity)? Restock(WorkshopSupply row,int nq,int hq,int rn,int rh,int filter)
    {
        if(row.SetSize<=0||row.RemainingSets<=0||filter is <0 or >2)return null;
        foreach(var high in new[]{false,true}){
            if(high&&filter==1||!high&&filter==2)continue;
            var held=high?hq:nq;var stock=high?rh:rn;
            var sets=Math.Min(row.RemainingSets,(held+stock)/row.SetSize);
            var take=sets*row.SetSize-held;
            if(sets>0&&take>0&&take<=stock)return (high,take);
        }
        return null;
    }
    internal static bool PreferRecordedChest(int needed,int retainers,int chest,float chestDistance,float bellDistance)=>
        needed>0&&chest>0&&float.IsFinite(chestDistance)&&(retainers<needed||chest>=needed&&chestDistance<=bellDistance);
    internal static bool DepositReceived(int before,int after,int remaining)=>before>0&&after==before-1&&remaining==0;
    internal static bool StationNearby(float distanceSquared)=>float.IsFinite(distanceSquared)&&distanceSquared>=0&&distanceSquared<=36;
    internal static bool FinalDeliveryMenuReceipt(WorkshopPhase before,WorkshopSupply supply,int bagBefore,int bagNow,IEnumerable<string> choices)
    {
        if(supply.RemainingSets!=1||bagNow!=bagBefore-supply.SetSize||!before.Rows.Contains(supply)||before.Rows.Any(r=>r.ItemId!=supply.ItemId&&r.RemainingSets!=0))return false;
        return choices.Any(s=>before.Phase<before.TotalPhases-1
            ?s.StartsWith("Advance to the next phase of production.",StringComparison.Ordinal)
            :s.StartsWith("Complete the construction of the ",StringComparison.Ordinal));
    }
    internal static bool MenuArrived(bool phaseReady,bool bookReady,bool menuReady,bool sameMenu)=>phaseReady||bookReady||menuReady&&!sameMenu;
    internal static bool PrioritizeActive(WorkshopQueueSave queue,uint item)
    {
        if(Next(queue)?.ItemId==item)return true;
        if(queue.PendingCollection!=0||queue.ReadyToCollect!=0)return false;
        var expanded=queue.Items.SelectMany(e=>Enumerable.Repeat(e with{Quantity=1},e.Quantity)).ToList();
        var index=expanded.FindIndex(Math.Clamp(queue.Completed,0,expanded.Count),e=>e.ItemId==item);
        if(index<0)return false;
        var active=expanded[index];expanded.RemoveAt(index);expanded.Insert(queue.Completed,active);
        var packed=new List<RetrievalEntry>();
        foreach(var e in expanded){if(packed.Count>0&&packed[^1].ItemId==e.ItemId&&packed[^1].RecipeId==e.RecipeId)packed[^1]=packed[^1] with{Quantity=packed[^1].Quantity+1};else packed.Add(e);}
        queue.Items=packed.ToArray();return true;
    }
    internal static int CompleteSets(int nq,int hq,int size)=>size>0?Math.Max(0,nq)/size+Math.Max(0,hq)/size:0;
    internal static (int Nq,int Hq) Supplements(int nq,int hq,int size,int sets)
    {
        if(size<1||sets<0)throw new ArgumentException("Invalid workshop set.");
        var missing=Math.Max(0,sets-CompleteSets(nq,hq,size));
        return missing==0?(0,0):(checked(missing*size-Math.Max(0,nq)%size),checked(missing*size-Math.Max(0,hq)%size));
    }
    internal static bool? ChooseHq(int nq,int hq,int size)=>size<1?null:nq>=size?false:hq>=size?true:null;
    internal static bool ManualRequestMatches(WorkshopPhase phase,uint project,int index,uint item,int quantity)=>
        Valid(phase)&&phase.ItemId==project&&index>=0&&index<phase.Rows.Length&&
        phase.Rows[index].RemainingSets>0&&phase.Rows[index].ItemId==item&&phase.Rows[index].SetSize==quantity;
    internal static bool Valid(WorkshopPhase p)=>p.ItemId>0&&p.TotalPhases is >0 and <=12&&p.Phase>=0&&p.Phase<p.TotalPhases&&p.Rows.Length is >0 and <=12&&p.Rows.All(r=>r.ItemId>0&&r.SetSize is >0 and <=999&&r.SetsRequired is >0 and <=99&&r.SetsDelivered>=0&&r.SetsDelivered<=r.SetsRequired);
    internal static RetrievalEntry? Next(WorkshopQueueSave q)=>q.Items.SelectMany(e=>Enumerable.Repeat(e with{Quantity=1},e.Quantity)).Skip(q.Completed).FirstOrDefault();
}
