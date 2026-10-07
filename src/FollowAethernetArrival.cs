using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string aethernetArrivalTrip="";
    private uint aethernetArrivalId;
    private bool aethernetArrivalLoading;
    private unsafe void RecordAethernetSelection(FollowPortalSignal trip,uint callback)
    {
        if(aethernetArrivalTrip==trip.Id)return;
        aethernetArrivalTrip=trip.Id;aethernetArrivalId=0;aethernetArrivalLoading=false;
        var agent=AgentTelepotTown.Instance();
        if(agent==null||agent->Data==null||agent->Data->AetheryteCount>64||callback>=agent->Data->AetheryteCount)return;
        var entry=agent->Data->Entries[(int)callback];
        if(entry.IsLocked||entry.IsUnusable||entry.IsCurrent||entry.TerritoryTypeId!=trip.ArrivalTerritory)return;
        aethernetArrivalId=entry.AetheryteId;
        RecordFollowTravel("Aethernet arrival destination recorded",new {trip.Id,destination=trip.Destination,aetheryte=aethernetArrivalId});
    }
    private unsafe bool ConfirmAethernetArrival(FollowPortalSignal trip,bool loading)
    {
        if(aethernetArrivalTrip!=trip.Id||aethernetArrivalId==0)return false;
        aethernetArrivalLoading|=loading;
        var map=AgentMap.Instance();var self=Objects.LocalPlayer;
        if(map==null||self==null)return false;
        var mapId=map->CurrentMapId;
        return Objects.Any(x=>x.ObjectKind==ObjectKind.Aetheryte&&x.BaseId==aethernetArrivalId&&
            FollowAethernetArrival.Confirmed(trip,true,aethernetArrivalLoading,loading,Player.IsLoaded,
                Player.CurrentWorld.RowId,Client.TerritoryType,mapId,CurrentFollowInstance(),
                self.Position,x.Position));
    }
}
