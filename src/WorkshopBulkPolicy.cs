namespace EquinoxCompanion;
internal sealed record WorkshopBulkStock(uint Item,int Quantity,bool Hq,bool Chest);
internal sealed record WorkshopBulkBatch(bool Chest,RetrievalQuality Quality,RetrievalEntry[] Items)
{
    public WorkshopBulkBatch[] RetainerParts {get;init;}=[];
}
internal static class WorkshopBulkPolicy
{
    internal static WorkshopBulkBatch[] Allocate(RetrievalEntry[] nq,RetrievalEntry[] hq,IEnumerable<WorkshopBulkStock> stock,bool chestNearer)
    {
        var available=stock.GroupBy(s=>(s.Item,s.Hq,s.Chest)).ToDictionary(g=>g.Key,g=>g.Sum(s=>Math.Max(0,s.Quantity)));
        var groups=new Dictionary<(bool Chest,bool Hq),List<RetrievalEntry>>();
        foreach(var high in new[]{false,true})foreach(var item in RetainerRetrieval.Combine(high?hq:nq)){
            var ret=available.GetValueOrDefault((item.ItemId,high,false));var chest=available.GetValueOrDefault((item.ItemId,high,true));
            if(ret+chest<item.Quantity)throw new InvalidOperationException("Recorded stock changed for "+item.Name+". Refresh materials before resuming.");
            var chestFirst=WorkshopPolicy.PreferRecordedChest(item.Quantity,ret,chest,chestNearer?0:1,chestNearer?1:0);
            var left=item.Quantity;
            foreach(var source in new[]{chestFirst,!chestFirst}){
                var take=Math.Min(left,source?chest:ret);if(take==0)continue;left-=take;
                var key=(source,high);if(!groups.TryGetValue(key,out var list))groups[key]=list=[];list.Add(item with{Quantity=take});
            }
        }
        return groups.OrderBy(g=>g.Key.Chest==chestNearer?0:1).ThenBy(g=>g.Key.Hq)
            .Select(g=>new WorkshopBulkBatch(g.Key.Chest,g.Key.Hq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly,g.Value.ToArray())).ToArray();
    }
    internal static WorkshopBulkBatch[] Preparation(RetrievalEntry[] reservedNq,RetrievalEntry[] reservedHq,bool turnIns,RetrievalEntry[] ingredients,WorkshopBulkStock[] stock,bool chestNearer,Func<uint,int> held,out long deferred)
    {
        var nq=new List<RetrievalEntry>();var hq=new List<RetrievalEntry>();
        if(turnIns){nq.AddRange(reservedNq);hq.AddRange(reservedHq);}
        var spareNq=stock.Where(s=>!s.Hq).GroupBy(s=>s.Item).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Quantity));
        foreach(var e in reservedNq)spareNq[e.ItemId]=spareNq.GetValueOrDefault(e.ItemId)-e.Quantity;
        foreach(var e in RetainerRetrieval.Combine(ingredients)){
            var normal=Math.Min(e.Quantity,Math.Max(0,spareNq.GetValueOrDefault(e.ItemId)));
            if(normal>0)nq.Add(e with{Quantity=normal});
            if(normal<e.Quantity){
                var high=e.Quantity-normal;
                var free=stock.Where(s=>s.Hq&&s.Item==e.ItemId).Sum(s=>s.Quantity)-reservedHq.Where(x=>x.ItemId==e.ItemId).Sum(x=>x.Quantity);
                if(free<high)throw new InvalidOperationException("Refresh changed stock for "+e.Name+".");
                hq.Add(e with{Quantity=high});
            }
        }
        var capped=new List<RetrievalEntry>();deferred=0;
        foreach(var e in RetainerRetrieval.Combine(nq)){
            var take=CrystalStorage.IsItem(e.ItemId)?CrystalStorage.Withdrawable(e.ItemId,held(e.ItemId),e.Quantity):e.Quantity;
            deferred+=e.Quantity-take;
            if(take>0)capped.Add(e with{Quantity=take});
        }
        var batches=Allocate(capped.ToArray(),hq.ToArray(),stock,chestNearer);
        var ret=batches.Where(b=>!b.Chest).ToArray();
        if(ret.Length==0)return batches;
        var combined=new WorkshopBulkBatch(false,RetrievalQuality.Any,RetainerRetrieval.Combine(ret.SelectMany(b=>b.Items))){RetainerParts=ret};
        var chest=batches.Where(b=>b.Chest);
        return (chestNearer?chest.Append(combined):chest.Prepend(combined)).ToArray();
    }
    internal static int NewSlots(int quantity,int room,int stack)=>quantity<=room?0:checked((quantity-room+Math.Max(1,stack)-1)/Math.Max(1,stack));
}
internal sealed record WorkshopSetChoice(int SetSize,bool Hq);
internal sealed record WorkshopSetAllocation(int Required,int Bags,int WithdrawNq,int WithdrawHq,int Missing,WorkshopSetChoice[] Choices);
internal static class WorkshopSetAllocator
{
    internal static WorkshopSetAllocation Allocate(IEnumerable<WorkshopSupply> rows,int nq,int hq,int rn,int rh,int quality)
    {
        if(quality is <0 or >2)throw new ArgumentException("Unknown quality filter.");
        if(quality==1){hq=rh=0;}else if(quality==2){nq=rn=0;}
        var choices=new List<WorkshopSetChoice>();
        var initial=nq+hq;var usedN=0;var usedH=0;var missing=0;var required=0;
        foreach(var row in rows.Where(r=>r.RemainingSets>0))for(var set=0;set<row.RemainingSets;set++){
            required=checked(required+row.SetSize);
            var high=WorkshopPolicy.ChooseHq(nq,hq,row.SetSize)??WorkshopPolicy.ChooseHq(nq+rn,hq+rh,row.SetSize);
            var useHq=high==true||high==null&&quality==2;choices.Add(new(row.SetSize,useHq));
            if(useHq){
                var bag=Math.Min(hq,row.SetSize);hq-=bag;var stored=Math.Min(rh,row.SetSize-bag);rh-=stored;usedH+=stored;missing+=row.SetSize-bag-stored;
            }else{
                var bag=Math.Min(nq,row.SetSize);nq-=bag;var stored=Math.Min(rn,row.SetSize-bag);rn-=stored;usedN+=stored;missing+=row.SetSize-bag-stored;
            }
        }
        return new(required,initial-nq-hq,usedN,usedH,missing,choices.ToArray());
    }
}
