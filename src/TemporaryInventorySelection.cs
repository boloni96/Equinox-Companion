namespace EquinoxCompanion;
public static class TemporaryInventorySelection
{
    public static Dictionary<uint,int> Amounts(IEnumerable<ChestStack> stock,IReadOnlyDictionary<uint,int> selected,RetrievalQuality quality,Func<uint,bool> matches)=>
        stock.Where(s=>(s.Container<=3||s.Container==2001)&&s.Item>0&&s.Quantity>0&&selected.ContainsKey(s.Item)&&matches(s.Item)&&RetainerRetrieval.AllowsQuality(s.Hq,quality))
            .GroupBy(s=>s.Item).Select(g=>(Id:g.Key,Quantity:Math.Min(g.Sum(s=>s.Quantity),Math.Max(0,selected[g.Key])))).Where(x=>x.Quantity>0).ToDictionary(x=>x.Id,x=>x.Quantity);
}
