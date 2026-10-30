using Dalamud.Game.ClientState.Objects.Enums;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset nextGardenObjectDiscovery;
    private unsafe void DiscoverGardenBedMappings(SharedGardenPlan plan,DateTimeOffset now)
    {
        if(now<nextGardenObjectDiscovery||!config.GardenAdventureInspect||!atOutdoorEstate||currentAddress is not {} address||
            SharedGardenLocation.Match(address,[plan])!=plan.HouseId)return;
        nextGardenObjectDiscovery=now.AddSeconds(3);
        var targets=new List<GardenTargetDetails>();
        foreach(var o in Objects){
            if(o.BaseId!=2003757||o.ObjectKind!=ObjectKind.EventObj||o.Address==0)continue;
            var arg=((FFXIVClientStructs.FFXIV.Client.Game.Object.EventObject*)o.Address)->Arg;
            if(GardenObjectDiscovery.Decode(arg) is not {} identity||identity.Plot!=address.Plot)continue;
            targets.Add(new(arg,o.Position.X,o.Position.Y,o.Position.Z));
        }
        var named=WithAddressNames(address);var actor=WithWorldNames(ReadActor());
        foreach(var mapped in GardenObjectDiscovery.Missing(named,targets,GardenMappings(),now))
            KeepDiscovery(new(Guid.NewGuid().ToString("N"),"garden.mapped",now,actor,named,mapped.Patch,mapped.Bed,
                GardenTarget:new(mapped.Argument,mapped.X,mapped.Y,mapped.Z)));
    }
}
