namespace EquinoxCompanion;
public sealed record StoragePreviewRow(uint Item,bool Hq,ulong From,ulong To,int Quantity,bool Merge);
public static class StoragePreview
{
    // Track the source of staged cargo rather than displaying the player's bags
    // as the origin of every delivery. No player inventory data is needed here.
    public static StoragePreviewRow[] Describe(RoutedMove[] moves)
    {
        var cargo=new Dictionary<(uint,bool),Queue<(ulong Owner,int Quantity)>>();
        var rows=new List<StoragePreviewRow>();
        foreach(var action in moves){
            var m=action.Move;var key=(m.Source.Item,m.Source.Hq);
            if(action.Internal){rows.Add(new(key.Item1,key.Item2,action.Retainer,action.Retainer,m.Quantity,true));continue;}
            if(!cargo.TryGetValue(key,out var queue))cargo[key]=queue=new();
            if(action.Withdraw){queue.Enqueue((action.Retainer,m.Quantity));continue;}
            var remaining=m.Quantity;
            while(remaining>0){
                if(queue.Count==0)throw new InvalidOperationException("Preview has an unmatched delivery; refresh the movement plan.");
                var source=queue.Dequeue();var q=Math.Min(source.Quantity,remaining);
                rows.Add(new(key.Item1,key.Item2,source.Owner,action.Retainer,q,false));remaining-=q;
                if(source.Quantity>q){var rest=queue.ToArray();queue.Clear();queue.Enqueue((source.Owner,source.Quantity-q));foreach(var entry in rest)queue.Enqueue(entry);}
            }
        }
        if(cargo.Values.Any(q=>q.Count>0))throw new InvalidOperationException("Preview contains undelivered cargo.");
        return rows.GroupBy(r=>(r.Item,r.Hq,r.From,r.To,r.Merge)).Select(g=>g.First() with{Quantity=g.Sum(r=>r.Quantity)}).ToArray();
    }
}
