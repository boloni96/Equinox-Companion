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
        if(aethernetArrivalTrip==trip.Id&&aethernetArrivalId!=0)return;
        if(aethernetArrivalTrip!=trip.Id){aethernetArrivalTrip=trip.Id;aethernetArrivalId=0;aethernetArrivalLoading=false;}
        // Resolve the exact menu label from game data, independently of transient native entry flags.
        var candidates=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>()
            .Select(x=>(Id:x.RowId,Territory:x.Territory.RowId,Name:x.AethernetName.Value.Name.ToString()));
        aethernetArrivalId=FollowAethernetArrival.Resolve(candidates,trip.ArrivalTerritory,trip.Destination);
        var method="game data";
        var agent=AgentTelepotTown.Instance();
        if(aethernetArrivalId==0&&agent!=null&&agent->Data!=null&&agent->Data->AetheryteCount<=64&&callback<agent->Data->AetheryteCount){
            var entry=agent->Data->Entries[(int)callback];
            RecordFollowTravel("Aethernet native arrival candidate",new {trip.Id,callback,entry.AetheryteId,entry.TerritoryTypeId,entry.IsLocked,entry.IsUnusable,entry.IsCurrent});
            if(!entry.IsLocked&&!entry.IsUnusable&&!entry.IsCurrent&&entry.TerritoryTypeId==trip.ArrivalTerritory)aethernetArrivalId=entry.AetheryteId;
            method="native menu";
        }
        RecordFollowTravel(aethernetArrivalId==0?"Aethernet arrival destination unresolved":"Aethernet arrival destination recorded",
            new {trip.Id,destination=trip.Destination,aetheryte=aethernetArrivalId,method});
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
