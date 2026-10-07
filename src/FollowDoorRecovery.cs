using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string recoveryDoorId="";
    private bool recoveryDoorAbsent;
    private unsafe bool RecoverReturnedDoorLeader(FollowPortalSignal trip,DateTimeOffset now,bool loading)
    {
        if(recoveryDoorId!=trip.Id){recoveryDoorId=trip.Id;recoveryDoorAbsent=false;}
        if(trip.TravelKind!="door"||loading||!Player.IsLoaded||AgentMap.Instance()==null)return false;
        var leader=Objects.OfType<IPlayerCharacter>().FirstOrDefault(x=>x.IsTargetable&&FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,x.Name.TextValue,x.HomeWorld.RowId));
        if(leader==null){recoveryDoorAbsent=true;return false;}
        if(!config.FollowThem.ResumeNearby||!FollowTravelRecovery.ReturnedDoor(trip,recoveryDoorAbsent,true,loading,routeSawLoading,Player.CurrentWorld.RowId,Client.TerritoryType,AgentMap.Instance()->CurrentMapId))return false;
        // The leader came back to the source; do not keep trying a denied/stale door.
        FailFollowTrip("Your selected character returned outside; cancelled the unfinished door trip.");
        RecordFollowTravel("Door return resumed follow",new {trip.Id});return true;
    }
}
