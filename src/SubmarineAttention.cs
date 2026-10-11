namespace EquinoxCompanion;
public sealed record FleetAttention(int Level,string Text,int OpenSlots=0);
public static class SubmarineAttention
{
    // 3 maintenance, 2 dispatch/registration, 1 sailing/complete, 0 unknown.
    public static FleetAttention Evaluate(long[] returns,bool?[] repairs,SubmarineSupplyView? supplies,int? slots,long now,int fuel=50,int kits=10,int space=20)
    {
        var reasons=new List<string>();
        // A supply reading or FC membership does not establish a working fleet.
        // A recorded vessel is workshop evidence, including for non-owner members.
        if(returns.Length>0){
            if(repairs.Take(returns.Length).Any(x=>x==true))reasons.Add("Repair needed");
            if(supplies?.Ceruleum is {} c&&c<Math.Max(1,fuel))reasons.Add("Low ceruleum");
            if(supplies?.RepairKits is {} k&&k<Math.Max(1,kits))reasons.Add("Low repair kits");
            if(supplies?.InventorySpace is {} i&&i<Math.Max(1,space))reasons.Add("Inventory space needed");
        }
        var available=slots is >=1 and <=4?Math.Max(0,slots.Value-returns.Length):0;
        if(reasons.Count>0)return new(3,string.Join(" · ",reasons)+(available>0?$" · {available} new submarine slot(s)":""),available);
        if(returns.Any(t=>t<=0))return new(2,"Needs dispatch"+(available>0?$" · {available} new submarine slot(s)":""),available);
        if(available>0)return new(2,$"{available} new submarine slot(s) available",available);
        if(returns.Any(t=>t>0&&t<=now))return new(1,"Voyage Complete");
        if(returns.Length>0)return new(1,"All recorded submarines voyaging");
        return new(0,"No recorded submarines · no maintenance reminders");
    }
    public static int Combine(IEnumerable<int> levels)=>levels.DefaultIfEmpty(0).Max();
}
