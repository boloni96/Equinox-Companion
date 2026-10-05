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
        e.Kind is "garden.mapped" or "garden.empty" or "garden.dead" or "garden.ready" or "garden.observed" or "garden.status" or "garden.planted" or "garden.tended" &&
        e.Patch is >=1 and <=3 && e.Bed is >=1 and <=8 && e.GardenTarget is {} t && e.Address is {} a
        ?new(a.HouseId,a.WorldName??"",a.DistrictName??"",a.Ward,a.Plot,e.Patch.Value,e.Bed.Value,t.Argument,t.X,t.Y,t.Z,e.At):null;

    private static string Norm(string? s)=>(s??"").Trim().Replace("The ","",StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
    private static (string,string,string,int,int,uint) Key(string house,string? world,string? district,int ward,int plot,uint argument) =>
        (house.ToUpperInvariant(),Norm(world),Norm(district),ward,plot,argument);

    // One linear pass per history, rather than scanning/sorting all mappings for each event.
    // Equal timestamps retain the first mapping, matching the previous stable ordering.
    public static Func<SyncEvent,SyncEvent> CreateResolver(IEnumerable<SharedGardenTarget> mappings)
    {
        var latest=new Dictionary<(string,string,string,int,int,uint),SharedGardenTarget>();
        foreach(var m in mappings)
        {
            var key=Key(m.GameHouseId,m.World,m.District,m.Ward,m.Plot,m.Argument);
            if(!latest.TryGetValue(key,out var previous)||m.At>previous.At)latest[key]=m;
        }
        return e=>
        {
            if(e.Kind is not ("garden.empty.unmapped" or "garden.unmapped" or "garden.status.unmapped") || e.GardenTarget is not {} t || e.Address is not {} a)return e;
            latest.TryGetValue(Key(a.HouseId,a.WorldName,a.DistrictName,a.Ward,a.Plot,t.Argument),out var m);
            if(m is null || m.Patch is <1 or >3 || m.Bed is <1 or >8 || Math.Abs((e.At-m.At).TotalDays)>60 ||
                !float.IsFinite(t.X)||!float.IsFinite(t.Y)||!float.IsFinite(t.Z)||
                !float.IsFinite(m.X)||!float.IsFinite(m.Y)||!float.IsFinite(m.Z)||
                Math.Pow(t.X-m.X,2)+Math.Pow(t.Y-m.Y,2)+Math.Pow(t.Z-m.Z,2)>.25*.25)return e;
            return e with {Kind=e.Kind=="garden.empty.unmapped"?"garden.empty":e.Kind=="garden.status.unmapped"?"garden.status":"garden.observed",Patch=m.Patch,Bed=m.Bed,
                Crop=e.Crop is null?null:e.Crop with {Evidence="garden-menu-and-system-message"}};
        };
    }
    public static SyncEvent Resolve(SyncEvent e,IEnumerable<SharedGardenTarget> mappings) => CreateResolver(mappings)(e);

    public static (string House,int Batch,int Bed)? SelectedBatch(GardenSnapshot target,IEnumerable<SharedGardenTarget> mappings,IEnumerable<SharedGardenPlan> plans)
    {
        if(target.Address is not {Apartment:false,Workshop:false,Room:0,Plot:>0} address || target.TargetId is null ||
            target.TargetDetails is not {DataId:2003757,EventArgument:{} argument} t)return null;
        var resolved=Resolve(new("selection","garden.unmapped",target.ObservedAt,target.Actor,address,
            GardenTarget:new(argument,t.X,t.Y,t.Z)),mappings);
        if(resolved.Patch is null || resolved.Bed is null)return null;
        var matches=plans.Where(p=>(p.PhysicalPatch>0?p.PhysicalPatch:p.Batch)==resolved.Patch &&
            SharedGardenLocation.Match(address,[p])==p.HouseId).Take(2).ToArray();
        return matches.Length==1?(matches[0].HouseId,matches[0].Batch,resolved.Bed.Value):null;
    }
}
