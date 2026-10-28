namespace EquinoxCompanion;
// Bind a planned visit to current slots without changing its item/quality/quantity intent.
public static class StorageVisit
{
    public static bool RecordedMatches(ChestStack[] slots,int used)
    {
        var normal=slots.Where(x=>x.Container is >=10000 and <=10006).ToArray();
        return used is >=0 and <=175&&normal.Length==175&&normal.Count(x=>x.Item>0)==used&&
            normal.All(x=>x.Slot is >=0 and <25)&&slots.Select(x=>(x.Container,x.Slot)).Distinct().Count()==slots.Length&&
            slots.Count(x=>x.Container==12001)==18&&slots.All(x=>x.Quantity>=0&&(x.Item==0?x.Quantity==0:x.Quantity<=x.Limit&&x.Limit>0));
    }
    public static RoutedMove[] Bind(RoutedMove[] visit,ChestStack[] bags,ChestStack[] retainer,bool protectGear=false,ISet<(uint,int)>? protectedSlots=null)
    {
        var b=bags.ToList();var r=retainer.ToList();var result=new List<RoutedMove>();
        bool Same(ChestStack a,ChestStack x)=>a.Item==x.Item&&a.Hq==x.Hq;
        foreach(var action in visit){
            var intent=action.Move;var left=intent.Quantity;
            var source=action.Withdraw||action.Internal?r:b;var target=action.Withdraw?b:r;
            while(left>0){
                var s=source.Where(x=>Same(x,intent.Source)&&x.Quantity>0&&(!protectGear||action.Withdraw||action.Internal||!protectedSlots!.Contains((x.Container,x.Slot))))
                    .OrderByDescending(x=>x.Container==intent.Source.Container&&x.Slot==intent.Source.Slot).FirstOrDefault();
                if(s==null){if(action.Internal)break;throw new InvalidOperationException("Recorded item quantity changed; refresh this retainer before continuing.");}
                var targets=target.Where(x=>x.CanDeposit&&(s.Item is >=2 and <=19?x.Container is 2001 or 12001:x.Container<=3||x.Container is >=10000 and <=10006))
                    .Where(x=>!ReferenceEquals(source,target)||x.Container!=s.Container||x.Slot!=s.Slot);
                // Internal merges never move a stack to another empty slot.
                var d=targets.Where(x=>Same(x,s)&&x.Quantity<x.Limit).OrderByDescending(x=>x.Container==intent.Destination.Container&&x.Slot==intent.Destination.Slot).FirstOrDefault()
                    ??(action.Internal?null:targets.Where(x=>x.Item==0).OrderByDescending(x=>x.Container==intent.Destination.Container&&x.Slot==intent.Destination.Slot).FirstOrDefault());
                if(d==null){if(action.Internal)break;throw new InvalidOperationException("Retainer or bag space changed; no verified space for the planned transfer.");}
                var qty=Math.Min(left,Math.Min(s.Quantity,(d.Item==0?s.Limit:d.Limit)-d.Quantity));
                if(qty<=0)throw new InvalidOperationException("Invalid live stack capacity.");
                result.Add(action with{Move=new(s,d,qty)});
                var si=source.FindIndex(x=>x.Container==s.Container&&x.Slot==s.Slot);var di=target.FindIndex(x=>x.Container==d.Container&&x.Slot==d.Slot);
                source[si]=s.Quantity==qty&&s.Item>19?s with{Item=0,Quantity=0,Hq=false,Limit=0}:s with{Quantity=s.Quantity-qty};
                target[di]=d with{Item=s.Item,Hq=s.Hq,Quantity=d.Quantity+qty,Limit=s.Limit};left-=qty;
            }
        }
        return result.ToArray();
    }
}
