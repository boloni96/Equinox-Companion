namespace EquinoxCompanion;

internal enum PreparationCoverage { Bags, Retainers, Craftable, Missing, Incomplete }
internal sealed record PreparationRow(uint ItemId,string Name,int Required,int Bags,int Retainers,int CraftQuantity,int Missing,bool Complete,string[] UsedFor,PreparationCoverage Coverage);
internal sealed record PreparationPlan(PreparationRow[] Rows,RetainerRetrieval.CraftStep[] Steps,RetrievalEntry[] Withdraw,RetrievalEntry[] Missing);
internal static class CraftingPreparation
{
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
    internal static int CompleteSets(int nq,int hq,int size)=>size>0?Math.Max(0,nq)/size+Math.Max(0,hq)/size:0;
    internal static (int Nq,int Hq) Supplements(int nq,int hq,int size,int sets)
    {
        if(size<1||sets<0)throw new ArgumentException("Invalid workshop set.");
        var missing=Math.Max(0,sets-CompleteSets(nq,hq,size));
        return missing==0?(0,0):(checked(missing*size-Math.Max(0,nq)%size),checked(missing*size-Math.Max(0,hq)%size));
    }
    internal static bool? ChooseHq(int nq,int hq,int size)=>size<1?null:nq>=size?false:hq>=size?true:null;
    internal static bool Valid(WorkshopPhase p)=>p.ItemId>0&&p.TotalPhases is >0 and <=12&&p.Phase>=0&&p.Phase<p.TotalPhases&&p.Rows.Length is >0 and <=12&&p.Rows.All(r=>r.ItemId>0&&r.SetSize is >0 and <=999&&r.SetsRequired is >0 and <=99&&r.SetsDelivered>=0&&r.SetsDelivered<=r.SetsRequired);
    internal static RetrievalEntry? Next(WorkshopQueueSave q)=>q.Items.SelectMany(e=>Enumerable.Repeat(e with{Quantity=1},e.Quantity)).Skip(q.Completed).FirstOrDefault();
}
