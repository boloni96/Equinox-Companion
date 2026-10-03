namespace EquinoxCompanion;

public sealed record SharedGardenTarget(string GameHouseId,string World,string District,int Ward,int Plot,int Patch,int Bed,uint Argument,float X,float Y,float Z,DateTimeOffset At);

public static class GardenTargetMap
{
    public static bool IsEmptyChat(CropChat chat) => chat.Text.Trim()=="There is nothing in this bed." &&
        string.IsNullOrWhiteSpace(chat.Sender) && chat.Target.TargetId is not null &&
        chat.Target.TargetDetails is {DataId:2003757,EventArgument:not null} &&
        chat.Target.Address is {Apartment:false,Workshop:false,Room:0,Plot:>0} &&
        chat.At>=chat.Target.ObservedAt && chat.At-chat.Target.ObservedAt<=TimeSpan.FromSeconds(2);

    public static SharedGardenTarget? FromNumbered(SyncEvent e) =>
        e.Kind is "garden.mapped" or "garden.empty" or "garden.dead" or "garden.ready" or "garden.observed" or "garden.planted" or "garden.tended" &&
        e.Patch is >=1 and <=3 && e.Bed is >=1 and <=8 && e.GardenTarget is {} t && e.Address is {} a
        ?new(a.HouseId,a.WorldName??"",a.DistrictName??"",a.Ward,a.Plot,e.Patch.Value,e.Bed.Value,t.Argument,t.X,t.Y,t.Z,e.At):null;

    public static SyncEvent Resolve(SyncEvent e,IEnumerable<SharedGardenTarget> mappings)
    {
        if(e.Kind is not ("garden.empty.unmapped" or "garden.unmapped") || e.GardenTarget is not {} t || e.Address is not {} a)return e;
        static string Norm(string? s)=>(s??"").Trim().Replace("The ","",StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        // Use the newest calibration for this object. Moving a patch invalidates its old coordinates.
        var latest=mappings.Where(m=>string.Equals(m.GameHouseId,a.HouseId,StringComparison.OrdinalIgnoreCase)&&m.Argument==t.Argument&&
            Norm(m.World)==Norm(a.WorldName)&&Norm(m.District)==Norm(a.DistrictName)&&m.Ward==a.Ward&&m.Plot==a.Plot)
            .OrderByDescending(m=>m.At).FirstOrDefault();
        if(latest is null || latest.Patch is <1 or >3 || latest.Bed is <1 or >8 || Math.Abs((e.At-latest.At).TotalDays)>60 ||
            !float.IsFinite(t.X)||!float.IsFinite(t.Y)||!float.IsFinite(t.Z)||
            !float.IsFinite(latest.X)||!float.IsFinite(latest.Y)||!float.IsFinite(latest.Z)||
            Math.Pow(t.X-latest.X,2)+Math.Pow(t.Y-latest.Y,2)+Math.Pow(t.Z-latest.Z,2)>.25*.25)return e;
        return e with {Kind=e.Kind=="garden.empty.unmapped"?"garden.empty":"garden.observed",Patch=latest.Patch,Bed=latest.Bed,
            Crop=e.Crop is null?null:e.Crop with {Evidence="garden-menu-and-system-message"}};
    }
}
