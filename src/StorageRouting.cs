namespace EquinoxCompanion;
public sealed record StorageRoute(ulong From,ulong To,uint[] Categories,uint[] Items,bool AllItems=false,bool Duplicates=false,RetrievalQuality Quality=RetrievalQuality.Any,Dictionary<uint,int>? Amounts=null);
public sealed record StorageRoutingPlan(string Name,ulong Character,StorageRoute[] Rules,int ReserveSlots=1,int KeepInBags=0);
public sealed record RoutedMove(ulong Retainer,ChestMove Move,bool Withdraw,bool Internal=false);
public sealed record RoutingResult(RoutedMove[] Moves,int Remaining);
public static class StorageRouting
{
    public const ulong Bags=0,AllSources=ulong.MaxValue;
    private static bool Same(ChestStack a,ChestStack b)=>a.Item==b.Item&&a.Hq==b.Hq;
    private static bool Crystal(uint item)=>item is >=2 and <=19;
    public static RoutingResult Build(StorageRoutingPlan plan,IReadOnlyDictionary<ulong,ChestStack[]> inventories,Func<uint,uint> category,bool bagsOnly=false,bool includeExistingBags=true)
    {
        var state=inventories.ToDictionary(x=>x.Key,x=>x.Value.ToList());
        if(!state.ContainsKey(Bags))throw new ArgumentException("Player inventory is required.");
        if(plan.Rules.Any(r=>r.To==0||r.To==AllSources||!state.ContainsKey(r.To)))throw new ArgumentException("A destination retainer is unavailable.");
        var goals=new Dictionary<(ulong,uint,bool),ulong>();
        foreach(var (owner,rows) in inventories)foreach(var s in rows.Where(s=>s.Item>0)){
            if(bagsOnly&&owner!=Bags)continue;
            var rule=plan.Rules.FirstOrDefault(r=>(r.From==AllSources||r.From==owner)&&RetainerRetrieval.AllowsQuality(s.Hq,r.Quality)&&
                (r.AllItems||r.Items.Contains(s.Item)||r.Categories.Contains(category(s.Item))||r.Duplicates&&inventories[r.To].Any(d=>Same(d,s)&&d.Quantity>0)));
            if(rule!=null)goals[(owner,s.Item,s.Hq)]=rule.To;
        }
        var outgoing=new Dictionary<(ulong,uint,bool),int>();
        if(!bagsOnly)foreach(var (owner,rows) in inventories.Where(x=>x.Key!=Bags))foreach(var g in rows.Where(s=>s.Item>0).GroupBy(s=>(s.Item,s.Hq)))
            if(goals.TryGetValue((owner,g.Key.Item,g.Key.Hq),out var dest)&&dest!=owner)outgoing[(owner,g.Key.Item,g.Key.Hq)]=g.Sum(s=>s.Quantity);
        var protectedBagSlots=!includeExistingBags?inventories[Bags].Where(s=>s.Item>0&&s.Limit==1).Select(s=>(s.Container,s.Slot)).ToHashSet():[];
        // Cargo retains its source rule even after it reaches bags. Existing bag reserves are never cargo.
        var cargo=new Dictionary<(uint,bool,ulong),int>();
        if(includeExistingBags)foreach(var group in inventories[Bags].Where(s=>s.Item>0).GroupBy(s=>(s.Item,s.Hq))){
            if(goals.TryGetValue((Bags,group.Key.Item,group.Key.Hq),out var dest)){
                var available=Math.Max(0,group.Sum(s=>s.Quantity)-Math.Max(0,plan.KeepInBags));
                var rule=plan.Rules.First(r=>r.To==dest&&(r.From==Bags||r.From==AllSources)&&RetainerRetrieval.AllowsQuality(group.Key.Hq,r.Quality)&&(r.AllItems||r.Duplicates||r.Items.Contains(group.Key.Item)||r.Categories.Contains(category(group.Key.Item))));
                if(rule.Amounts!=null){var already=cargo.Where(c=>c.Key.Item1==group.Key.Item&&c.Key.Item3==dest).Sum(c=>c.Value);available=Math.Min(available,Math.Max(0,rule.Amounts.GetValueOrDefault(group.Key.Item)-already));}
                cargo[(group.Key.Item,group.Key.Hq,dest)]=available;
            }
        }
        var moves=new List<RoutedMove>();ulong current=0;
        int FreeBags()=>state[Bags].Count(s=>s.Container<=3&&s.Item==0);
        ChestStack? Target(ChestStack source,ulong owner,bool allowReserve){
            var rows=state[owner].Where(d=>Crystal(source.Item)?d.Container is 2001 or 12001:owner==Bags?d.Container<=3:d.Container is >=10000 and <=10006).ToArray();
            return rows.FirstOrDefault(d=>Same(d,source)&&d.Quantity<d.Limit)??
                (owner==Bags&&!allowReserve&&FreeBags()<=Math.Max(0,plan.ReserveSlots)?null:rows.FirstOrDefault(d=>d.Item==0));
        }
        ChestMove? Make(ChestStack source,ulong to,int maximum,bool allowReserve){var dst=Target(source,to,allowReserve);if(dst==null)return null;var qty=Math.Min(source.Quantity,Math.Min(maximum,(dst.Item==0?source.Limit:dst.Limit)-dst.Quantity));return qty>0?new(source,dst,qty):null;}
        bool HasOutgoing(ulong owner)=>outgoing.Any(x=>x.Key.Item1==owner&&x.Value>0);
        (ChestMove? move,bool withdraw,ulong destination) Next(ulong owner,bool reserve){
            foreach(var c in cargo.Where(x=>x.Key.Item3==owner&&x.Value>0))foreach(var b in state[Bags].Where(s=>s.Item==c.Key.Item1&&s.Hq==c.Key.Item2&&s.Quantity>0&&!protectedBagSlots.Contains((s.Container,s.Slot))))if(Make(b,owner,c.Value,true) is {} deposit)return(deposit,false,owner);
            if(bagsOnly)return(null,false,0);
            foreach(var s in state[owner].Where(s=>s.Item>0&&s.Quantity>0)){
                if(!goals.TryGetValue((owner,s.Item,s.Hq),out var dest)||dest==owner||outgoing.GetValueOrDefault((owner,s.Item,s.Hq))<=0)continue;
                // Do not strand items for a full destination that has nothing to move elsewhere.
                if(Target(s,dest,true)==null&&!HasOutgoing(dest))continue;
                if(Make(s,Bags,outgoing[(owner,s.Item,s.Hq)],reserve) is {} pickup)return(pickup,true,dest);
            }
            return(null,false,0);
        }
        void Apply(ulong owner,ChestMove move,bool withdraw,ulong destination){
            var from=withdraw?owner:Bags;var to=withdraw?Bags:owner;
            var si=state[from].FindIndex(s=>s.Container==move.Source.Container&&s.Slot==move.Source.Slot);
            var di=state[to].FindIndex(s=>s.Container==move.Destination.Container&&s.Slot==move.Destination.Slot);
            var left=move.Source.Quantity-move.Quantity;
            // Crystal inventory slots retain their item identity at zero.
            state[from][si]=left==0&&!Crystal(move.Source.Item)?move.Source with{Item=0,Quantity=0,Hq=false,Limit=0}:move.Source with{Quantity=left};
            state[to][di]=move.Destination with{Item=move.Source.Item,Hq=move.Source.Hq,Quantity=move.Destination.Quantity+move.Quantity,Limit=move.Source.Limit};
            var key=(move.Source.Item,move.Source.Hq,destination);
            cargo[key]=cargo.GetValueOrDefault(key)+(withdraw?move.Quantity:-move.Quantity);
            if(withdraw)outgoing[(owner,move.Source.Item,move.Source.Hq)]-=move.Quantity;
            moves.Add(new(owner,move,withdraw));
        }
        void MergeAssigned()
        {
        if(!bagsOnly)foreach(var owner in state.Keys.Where(k=>k!=Bags)){
            for(var pass=0;pass<2000;pass++){
                var eligible=state[owner].Where(s=>s.Item>0&&plan.Rules.Any(r=>r.To==owner&&RetainerRetrieval.AllowsQuality(s.Hq,r.Quality)&&(r.AllItems||r.Duplicates||r.Items.Contains(s.Item)||r.Categories.Contains(category(s.Item))))).ToArray();
                var merge=ChestTransferPlanner.Merge(eligible);if(merge==null)break;
                var si=state[owner].FindIndex(s=>s.Container==merge.Source.Container&&s.Slot==merge.Source.Slot);var di=state[owner].FindIndex(s=>s.Container==merge.Destination.Container&&s.Slot==merge.Destination.Slot);
                var left=merge.Source.Quantity-merge.Quantity;state[owner][si]=left==0?merge.Source with{Item=0,Quantity=0,Hq=false,Limit=0}:merge.Source with{Quantity=left};state[owner][di]=merge.Destination with{Quantity=merge.Destination.Quantity+merge.Quantity};
                moves.Add(new(owner,merge,false,true));
            }
        }
        }
        MergeAssigned();
        for(var step=0;step<20000;step++){
            var action=current==0?(move:(ChestMove?)null,withdraw:false,destination:0UL):Next(current,false);
            if(action.move==null){
                current=0;
                // Unload staged cargo before collecting another batch.
                var order=cargo.Where(c=>c.Value>0).Select(c=>c.Key.Item3).Concat(state.Keys.Where(k=>k!=Bags)).Distinct();
                foreach(var owner in order){action=Next(owner,false);if(action.move!=null){current=owner;break;}}
                if(current==0){
                    // A spare slot may break a full-retainer cycle; only use it at a cargo destination.
                    foreach(var owner in cargo.Where(c=>c.Value>0).Select(c=>c.Key.Item3).Distinct()){
                        action=Next(owner,true);if(action.move!=null){current=owner;break;}}
                }
                if(current==0)break;
            }
            Apply(current,action.move!,action.withdraw,action.destination);
        }
        MergeAssigned();
        var remaining=cargo.Values.Sum();
        remaining+=outgoing.Values.Sum();
        return new(moves.ToArray(),remaining);
    }
}
