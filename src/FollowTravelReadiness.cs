using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly FollowStationaryGate travelStepStationary=new();
    private bool travelStepReady;
    private unsafe void ObserveTravelStationary(DateTimeOffset now)
    {
        var moving=FollowMovementKeysHeld();
        var self=Objects.LocalPlayer;
        travelStepReady=travelStepStationary.Observe(now,self?.Position??default,self!=null&&Player.IsLoaded&&!moving&&!followStopUnconfirmed&&!Conditions[ConditionFlag.InCombat]&&!Conditions[ConditionFlag.Unconscious]&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51]);
    }
    private unsafe bool BoundaryWardOpen(FollowPortalSignal signal)
    {
        if(signal.TravelKind!="ward"||signal.SourceKind!="boundary")return false;
        var block=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectBlock").Address;
        return block!=null&&block->IsVisible;
    }
}
