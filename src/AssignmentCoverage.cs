namespace EquinoxCompanion;
public sealed record AssignmentGap(uint Item,uint Category,int Slots,long Quantity,ulong[] Retainers);
public sealed record AssignmentCoverageResult(AssignmentGap[] Missing,int MisplacedSlots,int CheckedRetainers,int TotalRetainers);
public static class AssignmentCoverage
{
    public static AssignmentCoverageResult Check(ulong[] owners,IReadOnlyDictionary<ulong,ChestStack[]> inventory,
        IEnumerable<RetainerAssignment> assignments,IReadOnlyList<RetrievalPreset> plans,Func<uint,uint> category)
    {
        var owned=owners.ToHashSet();
        var rows=assignments.Where(a=>owned.Contains(a.Retainer)).Select(a=>a with{
            Items=a.Items.Concat(plans.Where(p=>p.PlanId.Length>0&&(a.Plans??[]).Contains(p.PlanId)).SelectMany(p=>p.Items).Select(i=>i.ItemId)).Distinct().ToArray()
        }).ToArray();
        var missing=new List<(ulong Owner,ChestStack Stack,uint Category)>();var misplaced=0;
        foreach(var owner in owners.Distinct()){
            if(!inventory.TryGetValue(owner,out var slots))continue;
            foreach(var slot in slots.Where(s=>s.Item>0&&s.Quantity>0&&(s.Container is >=10000 and <=10006 or 12001))){
                var cat=category(slot.Item);var explicitHomes=rows.Where(a=>a.Items.Contains(slot.Item)).ToArray();
                var homes=(explicitHomes.Length>0?explicitHomes:rows.Where(a=>a.Categories.Contains(cat))).Select(a=>a.Retainer).ToArray();
                if(homes.Length==0)missing.Add((owner,slot,cat));
                else if(!homes.Contains(owner))misplaced++;
            }
        }
        return new(missing.GroupBy(x=>x.Stack.Item).Select(g=>new AssignmentGap(g.Key,g.First().Category,g.Count(),g.Sum(x=>(long)x.Stack.Quantity),g.Select(x=>x.Owner).Distinct().ToArray())).OrderBy(x=>x.Category).ThenBy(x=>x.Item).ToArray(),
            misplaced,owners.Distinct().Count(inventory.ContainsKey),owned.Count);
    }
}
