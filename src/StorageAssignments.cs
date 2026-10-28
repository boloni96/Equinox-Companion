namespace EquinoxCompanion;
public sealed record RetainerAssignment(ulong Retainer,uint[] Categories,uint[] Items,string[]? Plans=null);
public sealed record RetainerInventoryRecord(ulong Character,ulong Retainer,DateTimeOffset At,ChestStack[] Slots);
public static class StorageAssignments
{
    public static bool HasSelections(IEnumerable<RetainerAssignment> assignments,IReadOnlyList<RetrievalPreset> plans) =>
        assignments.Any(a=>a.Items.Length>0||a.Categories.Length>0||plans.Any(p=>!string.IsNullOrEmpty(p.PlanId)&&(a.Plans??[]).Contains(p.PlanId)&&p.Items.Length>0));
    public static StorageRoute[] Apply(StorageRoute[] rules,RetainerAssignment[] assignments,IReadOnlyDictionary<ulong,ChestStack[]> inventory,Func<uint,uint> category,IReadOnlyList<RetrievalPreset>? plans=null)
    {
        // Plans are live item groups, not crafting expansion or quantity instructions.
        assignments=assignments.Select(a=>a with{Items=a.Items.Concat((plans??[]).Where(p=>p.PlanId.Length>0&&(a.Plans??[]).Contains(p.PlanId)).SelectMany(p=>p.Items).Select(i=>i.ItemId)).Distinct().ToArray()}).ToArray();
        var result=new List<StorageRoute>();
        foreach(var rule in rules){
            if(rule.To!=StorageAutoRouting.AllRetainers){result.Add(rule);continue;}
            var ids=inventory.Where(x=>rule.From==StorageRouting.AllSources||rule.From==x.Key).SelectMany(x=>x.Value).Where(s=>s.Item>0&&s.Quantity>0&&RetainerRetrieval.AllowsQuality(s.Hq,rule.Quality)).Select(s=>s.Item).Distinct();
            foreach(var id in ids){
                if(!rule.AllItems&&!rule.Duplicates&&!rule.Items.Contains(id)&&!rule.Categories.Contains(category(id)))continue;
                var explicitOwners=assignments.Where(a=>a.Items.Contains(id)).ToArray();
                var choices=(explicitOwners.Length>0?explicitOwners:assignments.Where(a=>a.Categories.Contains(category(id)))).Select(a=>a.Retainer).Where(inventory.ContainsKey).Distinct().ToArray();
                if(choices.Length>0)result.Add(rule with{Items=[id],Categories=[],AllItems=false,Destinations=choices,Flexible=true});
            }
        }
        return result.ToArray();
    }
}
