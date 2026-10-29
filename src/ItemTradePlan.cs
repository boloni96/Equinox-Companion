namespace EquinoxCompanion;
internal readonly record struct TradeItemKey(uint Item,bool Hq);
internal sealed record TradeItemAmount(uint Item,bool Hq,int Quantity)
{
    internal TradeItemKey Key=>new(Item,Hq);
}
internal sealed record TradeBagSlot(uint Container,int Slot,uint Item,bool Hq,int Quantity,int Limit,bool Tradable)
{
    internal TradeItemKey Key=>new(Item,Hq);
}
internal sealed record TradeBatchSlot(TradeBagSlot Source,int Quantity);
internal static class ItemTradePlan
{
    internal static Dictionary<TradeItemKey,int> Counts(IEnumerable<TradeItemAmount> items)=>items
        .GroupBy(x=>x.Key).ToDictionary(g=>g.Key,g=>checked(g.Sum(x=>x.Quantity)));
    internal static Dictionary<TradeItemKey,int> BagCounts(IEnumerable<TradeBagSlot> bags)=>Counts(bags.Where(x=>x.Item>0).Select(x=>new TradeItemAmount(x.Item,x.Hq,x.Quantity)));
    internal static bool Same(IEnumerable<TradeItemAmount> a,IEnumerable<TradeItemAmount> b){var x=Counts(a);var y=Counts(b);return x.Count==y.Count&&x.All(p=>y.GetValueOrDefault(p.Key)==p.Value);}
    internal static TradeItemAmount[] Amounts(IEnumerable<TradeBatchSlot> batch)=>batch.Select(x=>new TradeItemAmount(x.Source.Item,x.Source.Hq,x.Quantity)).ToArray();
    internal static TradeBatchSlot[] Next(TradeItemAmount[] remaining,TradeBagSlot[] bags)
    {
        var result=new List<TradeBatchSlot>();
        foreach(var want in remaining){
            if(want.Item<=1||want.Quantity<=0)throw new InvalidOperationException("Invalid item or quantity.");
            var sources=bags.Where(x=>x.Tradable&&x.Key==want.Key&&x.Quantity>0).ToArray();
            if(sources.Sum(x=>x.Quantity)<want.Quantity)throw new InvalidOperationException("Selected items changed or are no longer tradable. Refresh your bags.");
            var left=want.Quantity;
            foreach(var source in sources){var qty=Math.Min(left,source.Quantity);if(qty>0&&result.Count<5)result.Add(new(source,qty));left-=qty;if(left==0)break;}
        }
        return result.ToArray();
    }
    internal static bool CanReceive(TradeItemAmount[] incoming,TradeBagSlot[] bags,Func<uint,int> limit)
    {
        if(incoming.Length is <1 or >5||incoming.Any(x=>x.Item<=1||x.Quantity<=0||limit(x.Item)<=0||x.Quantity>limit(x.Item)))return false;
        var empty=bags.Count(x=>x.Container<=3&&x.Item==0);
        foreach(var (key,qty) in Counts(incoming)){
            var size=limit(key.Item);var crystal=CrystalStorage.IsItem(key.Item);
            var room=bags.Where(x=>x.Key==key&&(crystal?x.Container==2001:x.Container<=3)).Sum(x=>Math.Max(0,x.Limit-x.Quantity));
            var left=Math.Max(0,qty-room);if(crystal){if(left>0)return false;}else empty-=(left+size-1)/size;
        }
        return empty>=0;
    }
}
