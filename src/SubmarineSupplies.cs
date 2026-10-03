namespace EquinoxCompanion;

public sealed record SubmarineSupplies(string FcId,int Ceruleum,int RepairKits,int InventorySpace,int InventoryCapacity);
public sealed record SharedSubmarineSupplies(string CharacterId,DateTimeOffset ObservedAt,SubmarineSupplies Data);
public sealed record SubmarineSupplyView(int? Ceruleum,int? RepairKits,int? InventorySpace,int? InventoryCapacity,DateTimeOffset At,bool Observed);

public static class SubmarineSupplyStatus
{
    public static bool Valid(SubmarineSupplies? s) => s is not null && System.Text.RegularExpressions.Regex.IsMatch(s.FcId??"",@"^[1-9]\d{0,19}$") &&
        s.Ceruleum is >=0 and <=10000000 && s.RepairKits is >=0 and <=10000000 && s.InventoryCapacity is >0 and <=140 && s.InventorySpace>=0 && s.InventorySpace<=s.InventoryCapacity;
    public static SubmarineSupplyView? Select(string fcId,SharedSubmarineSupplies? direct,SharedCachedVoyage? cached)
    {
        if(direct is not null&&direct.Data.FcId==fcId&&Valid(direct.Data))return new(direct.Data.Ceruleum,direct.Data.RepairKits,direct.Data.InventorySpace,direct.Data.InventoryCapacity,direct.ObservedAt,true);
        return cached?.Data.FcId==fcId?new(cached.Data.Ceruleum,cached.Data.RepairKits,cached.Data.InventorySpace,null,cached.ImportedAt,false):null;
    }
    public static string Summary(SubmarineSupplyView? s) => s is null?"Supplies not recorded":$"Tanks: {s.Ceruleum?.ToString()??"?"} · Repairs: {s.RepairKits?.ToString()??"?"} · Bag space: {s.InventorySpace?.ToString()??"?"}"+(s.InventoryCapacity is {} cap?$"/{cap}":"");
    public static string Freshness(SubmarineSupplyView s,DateTimeOffset now) => s.Observed?
        $"Supplies observed {s.At.ToLocalTime():g}"+(now-s.At>=TimeSpan.FromHours(24)?" · older reading; log in to refresh":" · last recorded reading"):
        $"Cached supplies imported {s.At.ToLocalTime():g} · game observation time unknown";
}
