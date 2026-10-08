using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private HelperMeetingDestination[] CurrentMapDestinations(uint territory,uint mapId)
    {
        var map=DataManager.GetExcelSheet<Map>().GetRowOrDefault(mapId);
        if(map is not {} row||row.TerritoryType.RowId!=territory||row.SizeFactor==0)return [];
        var ids=DataManager.GetExcelSheet<Aetheryte>().Where(a=>a.IsAetheryte&&a.Territory.RowId==territory&&a.Map.RowId==mapId).Select(a=>a.RowId).ToHashSet();
        if(!DataManager.GetSubrowExcelSheet<MapMarker>().TryGetRow(row.MapMarkerRange,out var markers))return [];
        return markers.Where(m=>m.DataType==3&&ids.Contains(m.DataKey.RowId)).Select(m=>new HelperMeetingDestination(m.DataKey.RowId,territory,mapId,(m.X-1024f)/(row.SizeFactor/100f)-row.OffsetX,(m.Y-1024f)/(row.SizeFactor/100f)-row.OffsetY)).DistinctBy(m=>m.Id).ToArray();
    }
    private unsafe FollowPortalSignal? CreateCurrentMapMeeting()
    {
        var map=AgentMap.Instance();
        if(!Player.IsLoaded||map==null||Objects.LocalPlayer is not {} self)return null;
        var best=HelperMeetingPolicy.Select(CurrentMapDestinations(Client.TerritoryType,map->CurrentMapId),Client.TerritoryType,map->CurrentMapId,self.Position);
        if(best==null)return null;
        var name=DataManager.GetExcelSheet<Aetheryte>().GetRowOrDefault(best.Id)?.PlaceName.Value.Name.ToString()??"Current map";
        var signal=TravelSignal("teleport",best.Id,name,0,self.Position);
        return signal==null?null:CaptureTravelArrival(signal with {SourceKind="CurrentMapMeeting",DestinationTerritory=Client.TerritoryType});
    }
}
