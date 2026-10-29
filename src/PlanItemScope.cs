namespace EquinoxCompanion;

// Categories narrow an explicit plan. Routing assignments never grant item eligibility.
public static class PlanItemScope
{
    public static RetrievalEntry[] Filter(IEnumerable<RetrievalEntry> items,bool enabled,IEnumerable<uint> categories,Func<uint,uint> category)
    {
        var allowed=categories.ToHashSet();
        return items.Where(e=>!enabled||allowed.Contains(category(e.ItemId))).ToArray();
    }
    public static StorageRoute[] Restrict(IEnumerable<StorageRoute> rules,IEnumerable<uint> items,Func<uint,uint> category)
    {
        var allowed=items.ToHashSet();
        return rules.Select(r=>r with{
            Items=allowed.Where(id=>r.AllItems||r.Items.Contains(id)||r.Categories.Contains(category(id))).ToArray(),
            Categories=[],AllItems=false,Duplicates=false,
            Amounts=r.Amounts==null?null:new(r.Amounts),Destinations=r.Destinations?.ToArray()
        }).Where(r=>r.Items.Length>0).ToArray();
    }
    public static StorageRoute[] FromRetainers(IEnumerable<StorageRoute> rules,IReadOnlyDictionary<ulong,ChestStack[]> inventories,Func<uint,uint> category)=>
        Restrict(rules,inventories.Where(x=>x.Key!=StorageRouting.Bags).SelectMany(x=>x.Value).Where(s=>s.Item>0&&s.Quantity>0).Select(s=>s.Item),category);
    public static bool Allows(RoutedMove action,IEnumerable<StorageRoute> rules,bool entrust,IReadOnlyDictionary<uint,int> spent)
    {
        var move=action.Move;var source=move.Source;
        if(move.Quantity<=0||move.Quantity>source.Quantity)return false;
        if(entrust&&(action.Withdraw||action.Internal||!(source.Container<=3||source.Container==2001)))return false;
        return rules.Any(r=>!r.AllItems&&r.Categories.Length==0&&r.Items.Contains(source.Item)&&RetainerRetrieval.AllowsQuality(source.Hq,r.Quality)&&
            (!entrust||r.Amounts!=null&&(long)spent.GetValueOrDefault(source.Item)+move.Quantity<=r.Amounts.GetValueOrDefault(source.Item)));
    }
}
