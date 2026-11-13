using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Vector2 mapTravelPress;
    private uint mapTravelPressMap;
    private bool mapTravelPressed,mapTravelDragged;
    private DateTimeOffset mapTravelNext;
    private void InitializeMapAethernetTravel()=>Addons.RegisterListener(AddonEvent.PreReceiveEvent,"AreaMap",OnMapAethernetInput);
    private unsafe void OnMapAethernetInput(AddonEvent type,AddonArgs args)
    {
        if(!config.MapAethernetTravel){mapTravelPressed=false;return;}
        if(args is not AddonReceiveEventArgs input||input.AtkEventData==nint.Zero)return;
        if(input.AtkEventType!=AddonEventType.MouseDown&&input.AtkEventType!=AddonEventType.MouseMove&&input.AtkEventType!=AddonEventType.MouseUp)return;
        var data=(AtkEventData*)input.AtkEventData;var map=AgentMap.Instance();
        if(map==null)return;
        var mouse=new Vector2(data->MouseData.PosX,data->MouseData.PosY);
        if(input.AtkEventType==AddonEventType.MouseDown){
            mapTravelPressed=data->MouseData.ButtonId==0;mapTravelDragged=false;mapTravelPress=mouse;mapTravelPressMap=map->SelectedMapId;return;
        }
        if(input.AtkEventType==AddonEventType.MouseMove){
            if(mapTravelPressed&&Vector2.DistanceSquared(mouse,mapTravelPress)>25*MathF.Pow(AtkUnitBase.GetGlobalUIScale(),2))mapTravelDragged=true;
            return;
        }
        var click=mapTravelPressed&&data->MouseData.ButtonId==0&&!mapTravelDragged&&Vector2.DistanceSquared(mouse,mapTravelPress)<=25*MathF.Pow(AtkUnitBase.GetGlobalUIScale(),2)&&mapTravelPressMap==map->SelectedMapId;
        mapTravelPressed=false;
        var io=ImGui.GetIO();
        if(!click||io.KeyCtrl||io.KeyAlt||io.KeyShift||NearbyTravelBusy()||LifestreamBusy()||DateTimeOffset.UtcNow<mapTravelNext)return;
        try{
            var addon=(AddonAreaMap*)args.Addon.Address;
            var tooltip=SellerAddon("Tooltip");
            if(addon==null||tooltip==null||!tooltip->IsVisible)return;
            var labels=TravelNodeText(&tooltip->UldManager).Select(x=>x.Trim()).Where(x=>x.Length>0).ToHashSet(StringComparer.Ordinal);
            var selected=DataManager.GetExcelSheet<Map>().GetRowOrDefault(map->SelectedMapId);
            if(selected is not {} row||row.SizeFactor==0||!DataManager.GetSubrowExcelSheet<MapMarker>().TryGetRow(row.MapMarkerRange,out var markers))return;
            var candidates=DataManager.GetExcelSheet<Aetheryte>().Where(a=>a.AethernetGroup!=0&&a.Territory.RowId==map->SelectedTerritoryId&&labels.Contains(a.AethernetName.Value.Name.ToString().Trim())).ToArray();
            var matches=candidates.Where(a=>markers.Any(m=>m.DataType==(a.IsAetheryte?3:4)&&m.DataKey.RowId==(a.IsAetheryte?a.RowId:a.AethernetName.RowId)&&MapTravelPolicy.Hit(m.X,m.Y,row.SizeFactor,addon->MouseCoords.X,addon->MouseCoords.Y))).ToArray();
            if(matches.Length!=1)return;
            var target=matches[0];
            // Ordinary long-distance aetheryte clicks retain the game's teleport prompt.
            if(target.IsAetheryte&&!Objects.Any(o=>o.ObjectKind==Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte&&o.IsTargetable&&DataManager.GetExcelSheet<Aetheryte>().GetRowOrDefault(o.BaseId)?.AethernetGroup==target.AethernetGroup))return;
            mapTravelNext=DateTimeOffset.UtcNow.AddSeconds(1);
            if(!RequestUnlockedAethernet(target.RowId,out var reason)){RecordTravelPrompt("Map destination rejected",new{id=target.RowId,reason});FollowChatNotice("TRAVEL — "+reason);return;}
            // Consume only a validated, accepted left click so the native paid-teleport prompt does not also open.
            data->MouseData.ButtonId=50;
            RecordTravelPrompt("Map destination selected",new{id=target.RowId,name=target.AethernetName.Value.Name.ToString(),map=map->SelectedMapId});
        }catch(Exception ex){Log.Debug(ex,"Map aethernet click failed");RecordTravelPrompt("Map destination error",new{error=ex.GetType().Name});}
    }
}
