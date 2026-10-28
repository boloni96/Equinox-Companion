namespace EquinoxCompanion;
public sealed record ChestStack(uint Container,int Slot,uint Item,int Quantity,bool Hq,int Limit,bool CanDeposit=true);
public sealed record ChestMove(ChestStack Source,ChestStack Destination,int Quantity);
public static class ChestTransferPlanner
{
    private static bool Same(ChestStack a,ChestStack b)=>a.Item==b.Item&&a.Hq==b.Hq;
    private static ChestStack? Destination(ChestStack source,IReadOnlyList<ChestStack> targets)=>targets.FirstOrDefault(x=>Same(source,x)&&x.Quantity<x.Limit&&(x.Container!=source.Container||x.Slot!=source.Slot))??targets.FirstOrDefault(x=>x.Item==0);
    private static ChestMove? Move(ChestStack source,ChestStack? target,int wanted)
    {
        if(target==null)return null;
        var room=(target.Item==0?source.Limit:target.Limit)-target.Quantity;
        var quantity=Math.Min(source.Quantity,Math.Min(wanted,room));
        return quantity>0?new(source,target,quantity):null;
    }
    public static ChestMove? Withdraw(IReadOnlyList<ChestStack> chest,IReadOnlyList<ChestStack> bags,IReadOnlyDictionary<uint,int> goals,RetrievalQuality quality)
    {
        foreach(var s in chest.Where(x=>x.Item>0&&RetainerRetrieval.AllowsQuality(x.Hq,quality))){
            var missing=goals.GetValueOrDefault(s.Item)-bags.Where(x=>x.Item==s.Item&&RetainerRetrieval.AllowsQuality(x.Hq,quality)).Sum(x=>x.Quantity);
            if(missing>0&&Move(s,Destination(s,bags),missing) is {} move)return move;
        }
        return null;
    }
    public static ChestMove? Duplicates(IReadOnlyList<ChestStack> chest,IReadOnlyList<ChestStack> bags,int keep)
    {
        foreach(var s in bags.Where(x=>x.Item>0&&x.CanDeposit&&chest.Any(c=>Same(c,x)&&c.Quantity>0))){
            var spare=bags.Where(x=>Same(x,s)).Sum(x=>x.Quantity)-Math.Max(0,keep);
            if(spare>0&&Move(s,Destination(s,chest),spare) is {} move)return move;
        }
        return null;
    }
    public static ChestMove? Transfer(IReadOnlyList<ChestStack> source,IReadOnlyList<ChestStack> destination,IReadOnlySet<uint> items,RetrievalQuality quality)
    {
        foreach(var s in source.Where(x=>x.Item>0&&items.Contains(x.Item)&&RetainerRetrieval.AllowsQuality(x.Hq,quality)))
            if(Move(s,Destination(s,destination),s.Quantity) is {} move)return move;
        return null;
    }
    public static ChestMove? Merge(IReadOnlyList<ChestStack> chest)
    {
        // Only move later stacks into earlier stacks: every move reduces fragmentation.
        for(var i=0;i<chest.Count;i++){
            var target=chest[i];if(target.Item==0||target.Quantity>=target.Limit)continue;
            for(var j=i+1;j<chest.Count;j++)if(Same(target,chest[j])&&Move(chest[j],target,chest[j].Quantity) is {} move)return move;
        }
        return null;
    }
    public static bool Confirmed(ChestMove move,ChestStack source,ChestStack destination)=>
        (source.Item==move.Source.Item&&source.Hq==move.Source.Hq&&source.Quantity==move.Source.Quantity-move.Quantity||source.Item==0&&move.Source.Quantity==move.Quantity)&&
        destination.Item==move.Source.Item&&destination.Hq==move.Source.Hq&&destination.Quantity==move.Destination.Quantity+move.Quantity;
}
