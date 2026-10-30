namespace EquinoxCompanion;
public enum AssignmentRecommendationStyle { FewerMoves, MoreSpace, FewerHomes }
public sealed record AssignmentSetup(string Name,RetainerAssignment[] Assignments,bool Enabled);
public sealed record AssignmentSuggestion(uint Category,ulong Retainer,int AdditionalSlots,int FreeAfter,int MovedStacks,bool CapacityRelief=false,uint Item=0);
public sealed record AssignmentRecommendation(AssignmentSuggestion[] Suggestions,uint[] NeedsSpace,int ExistingUnplacedSlots,AssignmentGap[] Remaining);
public static class AssignmentRecommendations
{
    public static RetainerAssignment[] Copy(IEnumerable<RetainerAssignment> rows)=>rows.Select(a=>a with{Categories=a.Categories.ToArray(),Items=a.Items.ToArray(),Plans=a.Plans?.ToArray()}).ToArray();
    public static RetainerAssignment[] Apply(IEnumerable<RetainerAssignment> assignments,IEnumerable<AssignmentSuggestion> suggestions)
    {
        var rows=Copy(assignments).ToList();
        foreach(var s in suggestions){
            var index=rows.FindIndex(a=>a.Retainer==s.Retainer);
            if(index<0){rows.Add(new(s.Retainer,[],[]));index=rows.Count-1;}
            var row=rows[index];
            rows[index]=s.Item>0?row with{Items=row.Items.Append(s.Item).Distinct().Order().ToArray()}:
                row with{Categories=row.Categories.Append(s.Category).Distinct().Order().ToArray()};
        }
        return rows.ToArray();
    }
    // Conservative capacity projection: reserve incoming stock without assuming outgoing
    // items have already freed their old slots. HQ and NQ cannot share stacks.
    public static AssignmentRecommendation Build(ulong[] owners,IReadOnlyDictionary<ulong,ChestStack[]> inventory,
        RetainerAssignment[] assignments,IReadOnlyList<RetrievalPreset> plans,Func<uint,uint> category,AssignmentRecommendationStyle style=AssignmentRecommendationStyle.FewerMoves,bool relieveFullHomes=false)
    {
        owners=owners.Distinct().Order().ToArray();
        if(owners.Any(id=>!inventory.ContainsKey(id)))throw new InvalidOperationException("Record every retainer inventory before requesting recommendations.");
        var rows=Copy(assignments).Where(a=>owners.Contains(a.Retainer)).Select(a=>a with{Items=a.Items.Concat(plans.Where(p=>p.PlanId.Length>0&&(a.Plans??[]).Contains(p.PlanId)).SelectMany(p=>p.Items).Select(i=>i.ItemId)).Distinct().ToArray()}).ToArray();
        var stock=owners.ToDictionary(id=>id,id=>inventory[id].Where(s=>(s.Container is >=10000 and <=10006 or 12001)&&s.Item>0&&s.Quantity>0).ToList());
        int Free(List<ChestStack> slots)=>175-slots.Count(s=>s.Container is >=10000 and <=10006);
        int Pack(List<ChestStack> slots,ChestStack source,int reserve){
            var quantity=source.Quantity;
            if(source.Item is >=2 and <=19){
                var index=slots.FindIndex(s=>s.Container==12001&&s.Item==source.Item);
                if(index>=0){
                    var slot=slots[index];if(!slot.CanDeposit)return quantity;
                    var add=Math.Min(quantity,Math.Max(0,Math.Min(9999,slot.Limit)-slot.Quantity));
                    slots[index]=slot with{Quantity=slot.Quantity+add};return quantity-add;
                }
                var accepted=Math.Min(quantity,9999);
                slots.Add(source with{Container=12001,Quantity=accepted,Limit=9999});return quantity-accepted;
            }
            for(var i=0;i<slots.Count&&quantity>0;i++)if(slots[i].Item==source.Item&&slots[i].Hq==source.Hq&&slots[i].CanDeposit){
                var add=Math.Min(quantity,Math.Max(0,slots[i].Limit-slots[i].Quantity));slots[i]=slots[i] with{Quantity=slots[i].Quantity+add};quantity-=add;
            }
            while(quantity>0&&Free(slots)>reserve){
                var add=Math.Min(quantity,Math.Max(1,source.Limit));slots.Add(source with{Quantity=add});quantity-=add;
            }
            return quantity;
        }
        var unplaced=0;var overflow=new List<(ulong Owner,ChestStack Stack)>();
        foreach(var owner in owners)foreach(var item in inventory[owner].Where(s=>(s.Container is >=10000 and <=10006 or 12001)&&s.Item>0&&s.Quantity>0)){
            var explicitHomes=rows.Where(a=>a.Items.Contains(item.Item)).ToArray();
            var homes=(explicitHomes.Length>0?explicitHomes:rows.Where(a=>a.Categories.Contains(category(item.Item)))).Select(a=>a.Retainer).Distinct().ToArray();
            if(homes.Length==0||homes.Contains(owner))continue;
            var remaining=item.Quantity;
            foreach(var home in homes.OrderByDescending(h=>Free(stock[h]))){
                var candidate=stock[home].ToList();
                var left=Pack(candidate,item with{Quantity=remaining},0);
                if(left==remaining)continue;
                stock[home]=candidate;remaining=left;if(remaining==0)break;
            }
            if(remaining>0){unplaced++;if(explicitHomes.Length==0)overflow.Add((owner,item with{Quantity=remaining}));}
        }
        var coverage=AssignmentCoverage.Check(owners,inventory,assignments,plans,category);
        var suggestions=new List<AssignmentSuggestion>();
        var missingIds=coverage.Missing.Select(x=>x.Item).ToHashSet();
        var demand=owners.SelectMany(owner=>inventory[owner].Where(s=>s.Item>0&&s.Quantity>0&&missingIds.Contains(s.Item)&&(s.Container is >=10000 and <=10006 or 12001)).Select(s=>(Owner:owner,Stack:s,Relief:false))).ToList();
        if(relieveFullHomes)demand.AddRange(overflow.Select(x=>(x.Owner,x.Stack,Relief:true)));
        foreach(var group in demand.GroupBy(x=>category(x.Stack.Item)).OrderByDescending(g=>g.Count())){
            var relief=group.Any(x=>x.Relief);
            if(group.Key==0){
                // Unknown categories must not create a broad category-zero rule.
                foreach(var item in group.GroupBy(x=>x.Stack.Item))foreach(var owner in item.Select(x=>x.Owner).Distinct())
                    suggestions.Add(new(0,owner,0,Free(stock[owner]),0,relief,item.Key));
                continue;
            }
            var choices=new List<(ulong Owner,List<ChestStack> Stock,int Added,int Already,int Moved)>();
            foreach(var reserve in new[]{5,0}){
            foreach(var home in owners){
                if(relief&&rows.Any(a=>a.Retainer==home&&a.Categories.Contains(group.Key)))continue;
                var candidate=stock[home].ToList();var before=Free(candidate);var ok=true;var moved=0;
                foreach(var item in group.Where(x=>x.Owner!=home)){
                    moved++;if(Pack(candidate,item.Stack,reserve)>0){ok=false;break;}
                }
                if(ok)choices.Add((home,candidate,before-Free(candidate),group.Count(x=>x.Owner==home),moved));
            }
            if(choices.Count>0)break;
            }
            var ranked=style switch{
                AssignmentRecommendationStyle.MoreSpace=>choices.OrderByDescending(x=>Free(x.Stock)).ThenBy(x=>x.Added),
                AssignmentRecommendationStyle.FewerHomes=>choices.OrderByDescending(x=>suggestions.Any(s=>s.Retainer==x.Owner)).ThenBy(x=>x.Added).ThenByDescending(x=>Free(x.Stock)),
                _=>choices.OrderBy(x=>x.Moved).ThenBy(x=>x.Added).ThenByDescending(x=>x.Already).ThenByDescending(x=>Free(x.Stock))
            };
            var best=ranked.FirstOrDefault();
            if(best.Stock==null||style==AssignmentRecommendationStyle.FewerMoves&&best.Moved>0){
                // FewerMoves compares the zero-move shared-home option as well.
                // A shared category home can retain stock already there with no move
                // or extra slot. This also handles crystal quantities >9999 across
                // several retainers and full normal inventories.
                foreach(var owner in group.Select(x=>x.Owner).Distinct().Order()){
                    if(rows.Any(a=>a.Retainer==owner&&a.Categories.Contains(group.Key)))continue;
                    suggestions.Add(new(group.Key,owner,0,Free(stock[owner]),0,relief));
                }
                continue;
            }
            stock[best.Owner]=best.Stock;suggestions.Add(new(group.Key,best.Owner,best.Added,Free(best.Stock),best.Moved,relief));
        }
        var applied=Apply(assignments,suggestions);
        var after=AssignmentCoverage.Check(owners,inventory,applied,plans,category);
        return new(suggestions.ToArray(),after.Missing.Select(x=>x.Category).Distinct().ToArray(),unplaced,after.Missing);
    }
}
