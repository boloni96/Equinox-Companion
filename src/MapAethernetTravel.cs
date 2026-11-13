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
    private string mapTravelPressWindow="";
    private HashSet<string> mapTravelPressLabels=new(StringComparer.Ordinal);
    private long mapTravelInputCount;
    private string mapTravelLastResult="No map click received";
    private bool mapTravelPressed,mapTravelDragged;
    private DateTimeOffset mapTravelNext;
    private void InitializeMapAethernetTravel(){
        Addons.RegisterListener(AddonEvent.PreReceiveEvent,"AreaMap",OnMapAethernetInput);
        Addons.RegisterListener(AddonEvent.PreReceiveEvent,"TelepotTown",OnMapAethernetInput);
    }
    private void TraceMapClick(string reason,object? detail=null){mapTravelLastResult=reason;RecordTravelPrompt("Map click",new{reason,detail});}
    private unsafe HashSet<string> MapTravelLabels(){
        var tooltip=SellerAddon("Tooltip");
        return tooltip==null?new(StringComparer.Ordinal):TravelNodeText(&tooltip->UldManager).Select(x=>x.Trim()).Where(x=>x.Length>0).ToHashSet(StringComparer.Ordinal);
    }
    private unsafe void OnMapAethernetInput(AddonEvent type,AddonArgs args)
    {

        if(args is not AddonReceiveEventArgs input||input.AtkEventData==nint.Zero)return;
        if(input.AtkEventType!=AddonEventType.MouseDown&&input.AtkEventType!=AddonEventType.MouseMove&&input.AtkEventType!=AddonEventType.MouseUp)return;
        var data=(AtkEventData*)input.AtkEventData;var map=AgentMap.Instance();
        if(map==null)return;
        var mouse=new Vector2(data->MouseData.PosX,data->MouseData.PosY);
        if(input.AtkEventType==AddonEventType.MouseDown){
            mapTravelPressed=data->MouseData.ButtonId==0;mapTravelDragged=false;mapTravelPress=mouse;mapTravelPressMap=map->SelectedMapId;mapTravelPressWindow=args.AddonName;mapTravelPressLabels=config.MapAethernetTravel?MapTravelLabels():new(StringComparer.Ordinal);return;
        }
        if(input.AtkEventType==AddonEventType.MouseMove){
            if(mapTravelPressed&&Vector2.DistanceSquared(mouse,mapTravelPress)>25*MathF.Pow(AtkUnitBase.GetGlobalUIScale(),2))mapTravelDragged=true;
            return;
        }
        var click=mapTravelPressWindow==args.AddonName&&mapTravelPressed&&data->MouseData.ButtonId==0&&!mapTravelDragged&&Vector2.DistanceSquared(mouse,mapTravelPress)<=25*MathF.Pow(AtkUnitBase.GetGlobalUIScale(),2)&&mapTravelPressMap==map->SelectedMapId;
        mapTravelPressed=false;
        var io=ImGui.GetIO();
        if(!click)return;
        mapTravelInputCount++;
        if(!config.MapAethernetTravel){TraceMapClick("Disabled in Travel settings");return;}
        if(io.KeyCtrl||io.KeyAlt||io.KeyShift){TraceMapClick("Modifier held");return;}
        if(NearbyTravelBusy()||LifestreamBusy()){TraceMapClick("Another activity blocks travel");return;}
        if(DateTimeOffset.UtcNow<mapTravelNext)return;
        try{
            var labels=MapTravelLabels();
            if(labels.Count==0)labels=mapTravelPressLabels;
            if(args.AddonName=="TelepotTown"){TryMapTownClick((AtkUnitBase*)args.Addon.Address,data,labels);return;}
            var addon=(AddonAreaMap*)args.Addon.Address;
            if(addon==null)return;
            var selected=DataManager.GetExcelSheet<Map>().GetRowOrDefault(map->SelectedMapId);
            if(selected is not {} row||row.SizeFactor==0||!DataManager.GetSubrowExcelSheet<MapMarker>().TryGetRow(row.MapMarkerRange,out var markers)){TraceMapClick("Map marker data unavailable",new{map=map->SelectedMapId});return;}
            var candidates=DataManager.GetExcelSheet<Aetheryte>().Where(a=>a.AethernetGroup!=0&&a.Territory.RowId==map->SelectedTerritoryId&&labels.Contains(a.AethernetName.Value.Name.ToString().Trim())).ToArray();
            var matches=candidates.Where(a=>markers.Any(m=>m.DataType==(a.IsAetheryte?3:4)&&m.DataKey.RowId==(a.IsAetheryte?a.RowId:a.AethernetName.RowId)&&MapTravelPolicy.Hit(m.X,m.Y,row.SizeFactor,addon->MouseCoords.X,addon->MouseCoords.Y))).ToArray();
            if(matches.Length>1)matches=matches.Where(a=>labels.Contains(a.AethernetName.Value.Name.ToString().Trim())).ToArray();
            if(matches.Length!=1){TraceMapClick("No unique destination marker",new{map=map->SelectedMapId,x=addon->MouseCoords.X,y=addon->MouseCoords.Y,labels=labels.ToArray(),matches=matches.Length});return;}
            var target=matches[0];
            // Ordinary long-distance aetheryte clicks retain the game's teleport prompt.
            if(target.IsAetheryte&&!Objects.Any(o=>o.ObjectKind==Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte&&o.IsTargetable&&DataManager.GetExcelSheet<Aetheryte>().GetRowOrDefault(o.BaseId)?.AethernetGroup==target.AethernetGroup))return;
            mapTravelNext=DateTimeOffset.UtcNow.AddSeconds(1);
            if(!RequestUnlockedAethernet(target.RowId,out var reason)){RecordTravelPrompt("Map destination rejected",new{id=target.RowId,reason});FollowChatNotice("TRAVEL — "+reason);return;}
            // Consume only a validated, accepted left click so the native paid-teleport prompt does not also open.
            data->MouseData.ButtonId=50;
            mapTravelLastResult="Destination selected";RecordTravelPrompt("Map destination selected",new{id=target.RowId,name=target.AethernetName.Value.Name.ToString(),map=map->SelectedMapId});
        }catch(Exception ex){Log.Debug(ex,"Map aethernet click failed");RecordTravelPrompt("Map destination error",new{error=ex.GetType().Name});}
    }
    private unsafe void TryMapTownClick(AtkUnitBase* town,AtkEventData* input,HashSet<string> labels)
    {
        var agent=AgentTelepotTown.Instance();
        if(town==null||agent==null||agent->Data==null||agent->Data->AetheryteCount>64){TraceMapClick("Aethernet list unavailable");return;}
        var choices=AethernetChoices(town).Where(x=>labels.Contains(x.Name.Trim())).ToArray();
        if(choices.Length!=1){TraceMapClick("No unique aethernet tooltip",new{labels=labels.ToArray(),matches=choices.Length});return;}
        var index=choices[0].Callback;
        if(index>=agent->Data->AetheryteCount){TraceMapClick("Aethernet index outside current list");return;}
        var entry=agent->Data->Entries[(int)index];
        var name=DataManager.GetExcelSheet<PlaceName>().GetRowOrDefault(entry.PlaceNameId)?.Name.ToString().Trim();
        if(!MapTravelPolicy.CanSelectTown(choices[0].Name.Trim(),name,entry.IsLocked,entry.IsUnusable,entry.IsCurrent)){
            TraceMapClick("Aethernet destination unavailable or already current",new{index,entry.AetheryteId,entry.IsLocked,entry.IsUnusable,entry.IsCurrent});return;
        }
        mapTravelNext=DateTimeOffset.UtcNow.AddSeconds(1);
        input->MouseData.ButtonId=50;
        TraceMapClick("Selected from open aethernet map",new{index,id=entry.AetheryteId,name});
        NativeTravelCallback(town,11,(int)index);
    }

}
