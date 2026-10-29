namespace EquinoxCompanion;
internal static class PlanCategoryRules
{
    internal static RetrievalEntry[] Withdraw(IEnumerable<RetrievalEntry> listed,IEnumerable<RetrievalEntry> matching,int each)
    {
        if(each<1||each>RetainerRetrieval.MaxQuantity)throw new ArgumentException("Invalid category withdrawal amount.");
        var explicitItems=RetainerRetrieval.Combine(listed);var ids=explicitItems.Select(e=>e.ItemId).ToHashSet();
        return RetainerRetrieval.Combine(explicitItems.Concat(matching.Where(e=>!ids.Contains(e.ItemId)).DistinctBy(e=>e.ItemId).Select(e=>e with{Quantity=CrystalStorage.IsItem(e.ItemId)?Math.Min(9999,each):each,ReadyMaterial=true})));
    }
    internal static Dictionary<uint,int> Entrust(IEnumerable<ChestStack> bags,IEnumerable<uint> listed,IReadOnlySet<uint> categories,Func<uint,uint> category,RetrievalQuality quality,int keep)
    {
        var ids=listed.ToHashSet();return bags.Where(s=>s.Item>0&&!ids.Contains(s.Item)&&categories.Contains(category(s.Item))&&RetainerRetrieval.AllowsQuality(s.Hq,quality)).GroupBy(s=>s.Item).Select(g=>(Id:g.Key,Amount:Math.Max(0,g.Sum(s=>s.Quantity)-Math.Max(0,keep)))).Where(x=>x.Amount>0).ToDictionary(x=>x.Id,x=>x.Amount);
    }
}
