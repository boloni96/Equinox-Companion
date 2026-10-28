namespace EquinoxCompanion;
public static class StorageAutoRouting
{
    public const ulong AllRetainers=ulong.MaxValue-1;
    public static StorageRoutingPlan Resolve(StorageRoutingPlan plan,IReadOnlyDictionary<ulong,ChestStack[]> inventory,Func<uint,uint> category)
    {
        var owners=inventory.Keys.Where(x=>x!=0).Order().ToArray();
        if(owners.Length==0)throw new ArgumentException("No retainer inventories available.");
        var resolved=new List<StorageRoute>();
        foreach(var rule in plan.Rules){
            if(rule.To!=AllRetainers){resolved.Add(rule);continue;}
            var candidates=inventory.Where(x=>x.Key!=0&&(rule.From==StorageRouting.AllSources||rule.From==x.Key)).SelectMany(x=>x.Value)
                .Where(s=>s.Item>0&&s.Quantity>0&&RetainerRetrieval.AllowsQuality(s.Hq,rule.Quality)&&(rule.AllItems||rule.Items.Contains(s.Item)||rule.Categories.Contains(category(s.Item))));
            var capacity=owners.ToDictionary(id=>id,id=>inventory[id].Count(s=>s.Container is >=10000 and <=10006));
            var selectedIds=candidates.Select(s=>s.Item).ToHashSet();
            var assigned=owners.ToDictionary(id=>id,id=>inventory[id].Count(s=>s.Item>19&&!selectedIds.Contains(s.Item)));
            int Slots(IEnumerable<ChestStack> rows)=>rows.Where(s=>s.Item>19).GroupBy(s=>(s.Item,s.Hq)).Sum(g=>(int)Math.Ceiling(g.Sum(s=>(decimal)s.Quantity)/Math.Max(1,g.First().Limit)));
            ulong Pick(uint cat,uint[] ids,int slots)=>owners.OrderByDescending(id=>capacity[id]-assigned[id]>=slots)
                .ThenByDescending(id=>inventory[id].Where(s=>ids.Contains(s.Item)).Sum(s=>(long)Math.Max(1,(s.Quantity+Math.Max(1,s.Limit)-1)/Math.Max(1,s.Limit))))
                .ThenByDescending(id=>capacity[id]-assigned[id]).ThenBy(id=>id).First();
            foreach(var group in candidates.GroupBy(s=>category(s.Item)).OrderByDescending(x=>Slots(x)).ThenBy(x=>x.Key)){
                var ids=group.Select(s=>s.Item).Distinct().Order().ToArray();var slots=Slots(group);var to=Pick(group.Key,ids,slots);
                if(capacity[to]-assigned[to]>=slots){resolved.Add(rule with{To=to,Categories=[],Items=ids,AllItems=false});assigned[to]+=slots;}
                else foreach(var item in group.GroupBy(s=>s.Item).OrderByDescending(x=>Slots(x)).ThenBy(x=>x.Key)){
                    // A category larger than one retainer is split by item, keeping NQ/HQ together.
                    var needed=Slots(item);var dest=Pick(group.Key,[item.Key],needed);assigned[dest]+=needed;
                    resolved.Add(rule with{To=dest,Categories=[],Items=[item.Key],AllItems=false});
                }
            }
        }
        return plan with{Rules=resolved.ToArray()};
    }
    public static RoutingResult Entrust(StorageRoutingPlan plan,IReadOnlyDictionary<ulong,ChestStack[]> inventories,Func<uint,uint> category)
    {
        var state=inventories.ToDictionary(x=>x.Key,x=>x.Value.ToList());
        if(!state.ContainsKey(0))throw new ArgumentException("Player inventory is required.");
        var owners=state.Keys.Where(x=>x!=0).Order().ToArray();var moves=new List<RoutedMove>();var remaining=0;
        var spent=new Dictionary<(int Rule,uint Item),int>();var kept=new Dictionary<(uint Item,bool Hq),int>();
        foreach(var original in inventories[0].Where(s=>s.Item>0&&s.Quantity>0)){
            var ruleIndex=Array.FindIndex(plan.Rules,r=>(r.From==0||r.From==StorageRouting.AllSources)&&RetainerRetrieval.AllowsQuality(original.Hq,r.Quality)&&
                (r.AllItems||r.Items.Contains(original.Item)||r.Categories.Contains(category(original.Item))||r.Duplicates&&owners.Any(id=>(r.To==AllRetainers||r.To==id)&&inventories[id].Any(s=>s.Item==original.Item&&s.Hq==original.Hq&&s.Quantity>0))));
            if(ruleIndex<0)continue;var rule=plan.Rules[ruleIndex];
            var keepKey=(original.Item,original.Hq);var keep=Math.Min(original.Quantity,Math.Max(0,plan.KeepInBags-kept.GetValueOrDefault(keepKey)));kept[keepKey]=kept.GetValueOrDefault(keepKey)+keep;
            var amount=original.Quantity-keep;var amountKey=(ruleIndex,original.Item);
            if(rule.Amounts!=null)amount=Math.Min(amount,Math.Max(0,rule.Amounts.GetValueOrDefault(original.Item)-spent.GetValueOrDefault(amountKey)));
            // Reserve this allowance even if blocked, so later quality/stacks cannot exceed the requested total.
            spent[amountKey]=spent.GetValueOrDefault(amountKey)+amount;
            while(amount>0){
                var source=state[0].First(s=>s.Container==original.Container&&s.Slot==original.Slot);
                bool Same(ChestStack d)=>d.Item==source.Item&&d.Hq==source.Hq;
                var targets=owners.Where(id=>rule.To==AllRetainers||rule.To==id)
                    .Where(id=>!rule.Duplicates||inventories[id].Any(s=>Same(s)&&s.Quantity>0))
                    .SelectMany(id=>state[id].Where(d=>source.Item is >=2 and <=19?d.Container==12001:d.Container is >=10000 and <=10006)
                        .Where(d=>Same(d)&&d.Quantity<d.Limit||d.Item==0)
                        .Select(d=>(Owner:id,Slot:d,Existing:inventories[id].Any(s=>Same(s)&&s.Quantity>0))))
                    .OrderByDescending(x=>Same(x.Slot)).ThenByDescending(x=>x.Existing).ThenBy(x=>x.Owner).ThenBy(x=>x.Slot.Container).ThenBy(x=>x.Slot.Slot).ToArray();
                if(targets.Length==0){remaining+=amount;break;}
                var target=targets[0];var qty=Math.Min(amount,(target.Slot.Item==0?source.Limit:target.Slot.Limit)-target.Slot.Quantity);
                if(qty<=0){remaining+=amount;break;}
                moves.Add(new(target.Owner,new(source,target.Slot,qty),false));
                var si=state[0].FindIndex(s=>s.Container==source.Container&&s.Slot==source.Slot);var di=state[target.Owner].FindIndex(s=>s.Container==target.Slot.Container&&s.Slot==target.Slot.Slot);
                var left=source.Quantity-qty;state[0][si]=left==0&&source.Item>19?source with{Item=0,Quantity=0,Hq=false,Limit=0}:source with{Quantity=left};
                state[target.Owner][di]=target.Slot with{Item=source.Item,Hq=source.Hq,Quantity=target.Slot.Quantity+qty,Limit=source.Limit};amount-=qty;
            }
        }
        return new(moves.ToArray(),remaining);
    }
}
