namespace EquinoxCompanion;
public enum AssignmentRecommendationStyle { FewerMoves, MoreSpace, FewerHomes }
public sealed record AssignmentSetup(string Name,RetainerAssignment[] Assignments,bool Enabled);
public sealed record AssignmentSuggestion(uint Category,ulong Retainer,int AdditionalSlots,int FreeAfter);
public sealed record AssignmentRecommendation(AssignmentSuggestion[] Suggestions,uint[] NeedsSpace,int ExistingUnplacedSlots);
public static class AssignmentRecommendations
{
    public static RetainerAssignment[] Copy(IEnumerable<RetainerAssignment> rows)=>rows.Select(a=>a with{Categories=a.Categories.ToArray(),Items=a.Items.ToArray(),Plans=a.Plans?.ToArray()}).ToArray();
    // Conservative capacity projection: reserve incoming stock without assuming outgoing
    // items have already freed their old slots. HQ and NQ cannot share stacks.
    public static AssignmentRecommendation Build(ulong[] owners,IReadOnlyDictionary<ulong,ChestStack[]> inventory,
        RetainerAssignment[] assignments,IReadOnlyList<RetrievalPreset> plans,Func<uint,uint> category,AssignmentRecommendationStyle style=AssignmentRecommendationStyle.FewerMoves)
    {
        if(owners.Any(id=>!inventory.ContainsKey(id)))throw new InvalidOperationException("Record every retainer inventory before requesting recommendations.");
        var rows=Copy(assignments).Where(a=>owners.Contains(a.Retainer)).Select(a=>a with{Items=a.Items.Concat(plans.Where(p=>p.PlanId.Length>0&&(a.Plans??[]).Contains(p.PlanId)).SelectMany(p=>p.Items).Select(i=>i.ItemId)).Distinct().ToArray()}).ToArray();
        var stock=owners.ToDictionary(id=>id,id=>inventory[id].Where(s=>s.Container is >=10000 and <=10006&&s.Item>0&&s.Quantity>0).ToList());
        int Free(List<ChestStack> slots)=>175-slots.Count;
        bool Pack(List<ChestStack> slots,ChestStack source,int reserve){
            var quantity=source.Quantity;
            for(var i=0;i<slots.Count&&quantity>0;i++)if(slots[i].Item==source.Item&&slots[i].Hq==source.Hq&&slots[i].CanDeposit){
                var add=Math.Min(quantity,Math.Max(0,slots[i].Limit-slots[i].Quantity));slots[i]=slots[i] with{Quantity=slots[i].Quantity+add};quantity-=add;
            }
            while(quantity>0&&Free(slots)>reserve){
                var add=Math.Min(quantity,Math.Max(1,source.Limit));slots.Add(source with{Quantity=add});quantity-=add;
            }
            return quantity==0;
        }
        var unplaced=0;
        foreach(var owner in owners)foreach(var item in inventory[owner].Where(s=>s.Container is >=10000 and <=10006&&s.Item>0&&s.Quantity>0)){
            var explicitHomes=rows.Where(a=>a.Items.Contains(item.Item)).ToArray();
            var homes=(explicitHomes.Length>0?explicitHomes:rows.Where(a=>a.Categories.Contains(category(item.Item)))).Select(a=>a.Retainer).Distinct().ToArray();
            if(homes.Length==0||homes.Contains(owner))continue;
            var fitted=false;
            foreach(var home in homes.OrderByDescending(h=>Free(stock[h]))){
                var candidate=stock[home].ToList();
                if(!Pack(candidate,item,0))continue;
                stock[home]=candidate;fitted=true;break;
            }
            if(!fitted)unplaced++;
        }
        var coverage=AssignmentCoverage.Check(owners,inventory,assignments,plans,category);
        var suggestions=new List<AssignmentSuggestion>();var needsSpace=new List<uint>();
        foreach(var group in coverage.Missing.GroupBy(x=>x.Category).OrderByDescending(g=>g.Sum(x=>x.Slots))){
            var ids=group.Select(x=>x.Item).ToHashSet();
            if(group.Key==0||group.Any(x=>x.Item is >=2 and <=19)){needsSpace.Add(group.Key);continue;}
            var choices=new List<(ulong Owner,List<ChestStack> Stock,int Added,int Already)>();
            foreach(var home in owners){
                var candidate=stock[home].ToList();var before=candidate.Count;var ok=true;
                foreach(var source in owners.Where(x=>x!=home))foreach(var item in inventory[source].Where(s=>s.Container is >=10000 and <=10006&&s.Quantity>0&&ids.Contains(s.Item))){
                    if(!Pack(candidate,item,5)){ok=false;break;}
                }
                if(ok)choices.Add((home,candidate,candidate.Count-before,inventory[home].Count(s=>s.Quantity>0&&ids.Contains(s.Item)&&s.Container is >=10000 and <=10006)));
            }
            var ranked=style switch{
                AssignmentRecommendationStyle.MoreSpace=>choices.OrderByDescending(x=>Free(x.Stock)).ThenBy(x=>x.Added),
                AssignmentRecommendationStyle.FewerHomes=>choices.OrderByDescending(x=>suggestions.Any(s=>s.Retainer==x.Owner)).ThenBy(x=>x.Added).ThenByDescending(x=>Free(x.Stock)),
                _=>choices.OrderBy(x=>x.Added).ThenByDescending(x=>x.Already).ThenByDescending(x=>Free(x.Stock))
            };
            var best=ranked.FirstOrDefault();
            if(best.Stock==null){needsSpace.Add(group.Key);continue;}
            stock[best.Owner]=best.Stock;suggestions.Add(new(group.Key,best.Owner,best.Added,Free(best.Stock)));
        }
        return new(suggestions.ToArray(),needsSpace.ToArray(),unplaced);
    }
}
