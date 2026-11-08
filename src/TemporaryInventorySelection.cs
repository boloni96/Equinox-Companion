namespace EquinoxCompanion;
public static class TemporaryInventorySelection
{
    public static bool Pick(IDictionary<uint,int> selected,uint item,int action,IEnumerable<ChestStack> stock)
    {
        if(item==0||action==0)return false;
        if(action<0)return selected.Remove(item);
        if(selected.ContainsKey(item))return false;
        var quantity=stock.Where(s=>(s.Container<=3||s.Container==2001)&&s.Item==item&&s.Quantity>0).Sum(s=>s.Quantity);
        if(quantity<=0)return false;
        selected[item]=quantity;return true;
    }
    public static Dictionary<uint,int> Amounts(IEnumerable<ChestStack> stock,IReadOnlyDictionary<uint,int> selected,RetrievalQuality quality,Func<uint,bool> matches)=>
        stock.Where(s=>(s.Container<=3||s.Container==2001)&&s.Item>0&&s.Quantity>0&&selected.ContainsKey(s.Item)&&matches(s.Item)&&RetainerRetrieval.AllowsQuality(s.Hq,quality))
            .GroupBy(s=>s.Item).Select(g=>(Id:g.Key,Quantity:Math.Min(g.Sum(s=>s.Quantity),Math.Max(0,selected[g.Key])))).Where(x=>x.Quantity>0).ToDictionary(x=>x.Id,x=>x.Quantity);
}
