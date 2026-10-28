namespace EquinoxCompanion;
public static class StorageAutoRouting
{
    public const ulong AllRetainers=ulong.MaxValue-1;
    public static StorageRoutingPlan Resolve(StorageRoutingPlan plan,IReadOnlyDictionary<ulong,ChestStack[]> inventory,Func<uint,uint> category)
    {
        // Dictionary order is the game's retainer order supplied by the caller.
        var allOwners=inventory.Keys.Where(x=>x!=0).ToArray();
        if(allOwners.Length==0)throw new ArgumentException("No retainer inventories available.");
        var resolved=new List<StorageRoute>();
        foreach(var rule in plan.Rules){
            if(rule.To!=AllRetainers){resolved.Add(rule);continue;}
            var owners=rule.Destinations?.Where(allOwners.Contains).Distinct().ToArray()??allOwners;
            if(owners.Length==0)continue;
            var groups=inventory.Where(x=>x.Key!=0&&(rule.From==StorageRouting.AllSources||rule.From==x.Key)).SelectMany(x=>x.Value.Select(s=>(Owner:x.Key,Stack:s)))
                .Where(x=>x.Stack.Item>0&&x.Stack.Quantity>0&&RetainerRetrieval.AllowsQuality(x.Stack.Hq,rule.Quality)&&(rule.AllItems||rule.Items.Contains(x.Stack.Item)||rule.Categories.Contains(category(x.Stack.Item))))
                .GroupBy(x=>x.Stack.Item);
            foreach(var group in groups){
                var existing=owners.Where(id=>inventory[id].Any(s=>s.Item==group.Key&&s.Quantity>0)).ToArray();
                // Regular organisation consolidates duplicates; it does not assign
                // a new category home or relocate unique stacks.
                if(rule.Destinations==null&&inventory.Where(x=>x.Key!=0).SelectMany(x=>x.Value).Count(s=>s.Item==group.Key&&s.Quantity>0&&RetainerRetrieval.AllowsQuality(s.Hq,rule.Quality))<2)continue;
                var to=existing.FirstOrDefault();
                if(to==0)to=owners.FirstOrDefault(id=>inventory[id].Any(s=>s.Container is >=10000 and <=10006&&s.CanDeposit&&s.Item==0));
                if(to==0)to=owners[0];
                // Existing partial stacks win, then assignment priority. The
                // transfer planner reports capacity that cannot be reached.
                resolved.Add(rule with{To=to,Categories=[],Items=[group.Key],AllItems=false,Destinations=rule.Destinations??existing});
            }
        }
        return plan with{Rules=resolved.ToArray()};
    }
    public static RoutingResult Entrust(StorageRoutingPlan plan,IReadOnlyDictionary<ulong,ChestStack[]> inventories,Func<uint,uint> category)
    {
        var state=inventories.ToDictionary(x=>x.Key,x=>x.Value.ToList());
        if(!state.ContainsKey(0))throw new ArgumentException("Player inventory is required.");
        var owners=state.Keys.Where(x=>x!=0).ToArray();var moves=new List<RoutedMove>();var remaining=0;
        var spent=new Dictionary<(int Rule,uint Item),int>();var kept=new Dictionary<(uint Item,bool Hq),int>();
        foreach(var original in inventories[0].Where(s=>s.Item>0&&s.Quantity>0)){
            var ruleIndex=Array.FindIndex(plan.Rules,r=>(r.From==0||r.From==StorageRouting.AllSources)&&RetainerRetrieval.AllowsQuality(original.Hq,r.Quality)&&
                (r.Duplicates?(r.Items.Length==0||r.Items.Contains(original.Item))&&owners.Any(id=>(r.To==AllRetainers||r.To==id)&&(r.Destinations==null||r.Destinations.Contains(id))&&inventories[id].Any(s=>s.Item==original.Item&&s.Hq==original.Hq&&s.Quantity>0)):(r.AllItems||r.Items.Contains(original.Item)||r.Categories.Contains(category(original.Item)))));
            if(ruleIndex<0)continue;var rule=plan.Rules[ruleIndex];
            var keepKey=(original.Item,original.Hq);var keep=Math.Min(original.Quantity,Math.Max(0,plan.KeepInBags-kept.GetValueOrDefault(keepKey)));kept[keepKey]=kept.GetValueOrDefault(keepKey)+keep;
            var amount=original.Quantity-keep;var amountKey=(ruleIndex,original.Item);
            if(rule.Amounts!=null)amount=Math.Min(amount,Math.Max(0,rule.Amounts.GetValueOrDefault(original.Item)-spent.GetValueOrDefault(amountKey)));
            // Reserve this allowance even if blocked, so later quality/stacks cannot exceed the requested total.
            spent[amountKey]=spent.GetValueOrDefault(amountKey)+amount;
            while(amount>0){
                var source=state[0].First(s=>s.Container==original.Container&&s.Slot==original.Slot);
                bool Same(ChestStack d)=>d.Item==source.Item&&d.Hq==source.Hq;
                var targets=owners.Where(id=>(rule.To==AllRetainers||rule.To==id)&&(rule.Destinations==null||rule.Destinations.Contains(id)))
                    .Where(id=>!rule.Duplicates||inventories[id].Any(s=>Same(s)&&s.Quantity>0))
                    .SelectMany(id=>state[id].Where(d=>source.Item is >=2 and <=19?d.Container==12001:d.Container is >=10000 and <=10006)
                        .Where(d=>d.CanDeposit&&(Same(d)&&d.Quantity<d.Limit||d.Item==0))
                        .Select(d=>(Owner:id,Slot:d,Existing:inventories[id].Any(s=>Same(s)&&s.Quantity>0))))
                    .OrderByDescending(x=>Same(x.Slot)).ThenByDescending(x=>x.Existing).ThenBy(x=>rule.Destinations==null?0:Array.IndexOf(rule.Destinations,x.Owner)).ThenBy(x=>Array.IndexOf(owners,x.Owner)).ThenBy(x=>x.Slot.Container).ThenBy(x=>x.Slot.Slot).ToArray();
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
