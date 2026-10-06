namespace EquinoxCompanion;
public sealed record FleetAttention(int Level,string Text,int OpenSlots=0);
public static class SubmarineAttention
{
    // 3 orange, 2 yellow, 1 green, 0 unknown. Reserves are reminders, not route costs.
    public static FleetAttention Evaluate(long[] returns,bool?[] repairs,SubmarineSupplyView? supplies,int? slots,long now,int fuel=50,int kits=10,int space=20)
    {
        var reasons=new List<string>();
        if(repairs.Any(x=>x==true))reasons.Add("Repair needed");
        if(supplies?.Ceruleum is {} c&&c<Math.Max(1,fuel))reasons.Add("Low ceruleum");
        if(supplies?.RepairKits is {} k&&k<Math.Max(1,kits))reasons.Add("Low repair kits");
        if(supplies?.InventorySpace is {} i&&i<Math.Max(1,space))reasons.Add("Inventory space needed");
        var available=Math.Max(0,(slots??0)-returns.Length);
        if(reasons.Count>0)return new(3,string.Join(" · ",reasons)+(available>0?$" · {available} new submarine slot(s)":""),available);
        if(returns.Any(t=>t<=now))return new(2,"Needs dispatch / return collection"+(available>0?$" · {available} new submarine slot(s)":""),available);
        if(available>0)return new(2,$"{available} new submarine slot(s) available",available);
        if(returns.Length>0)return new(1,"All recorded submarines voyaging");
        return new(0,"Fleet not recorded");
    }
    public static int Combine(IEnumerable<int> levels)=>levels.DefaultIfEmpty(0).Max();
}
