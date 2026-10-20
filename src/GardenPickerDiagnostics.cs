using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Bounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly List<object> gardenPickerCaptures=[];
    private readonly List<string> gardenPickerCaptureKeys=[];
    private readonly Dictionary<string,int> gardenPickerCaptureCounts=new();
    private DateTimeOffset nextGardenPickerCapture;
    private string gardenPickerCheck="Open the game's soil and seed pickers to capture their displayed rows.";

    private static unsafe object? GardenNodeDetails(AtkResNode* node)
    {
        if(node==null)return null;
        Bounds bounds;node->GetBounds(&bounds);
        static float? Finite(float value)=>float.IsFinite(value)?value:null;
        return new {nodeId=node->NodeId,type=node->Type.ToString(),visible=MarkerVisible(node),x=Finite(node->X),y=Finite(node->Y),width=node->Width,height=node->Height,
            bounds=new[]{Finite(bounds.Pos1.X),Finite(bounds.Pos1.Y),Finite(bounds.Pos2.X),Finite(bounds.Pos2.Y)},parentId=node->ParentNode==null?0:node->ParentNode->NodeId};
    }
    private static unsafe void GardenPickerNodeDetails(AtkUldManager* uld,List<object> nodes,ref int budget,int depth=0)
    {
        if(uld==null||uld->NodeList==null||depth>6)return;
        for(var i=0;i<uld->NodeListCount&&budget-->0;i++){
            var node=uld->NodeList[i];if(node==null||(int)node->Type<1000)continue;
            var component=node->GetComponent();if(component==null)continue;
            var type=component->GetComponentType();
            var icon=type==ComponentType.Icon?(AtkComponentIcon*)component:type==ComponentType.DragDrop?((AtkComponentDragDrop*)component)->AtkComponentIcon:null;
            if(icon!=null){nodes.Add(new {kind=type.ToString(),iconId=icon->IconId,hasTexture=icon->Texture!=null,flags=icon->Flags.ToString(),owner=GardenNodeDetails(node),image=GardenNodeDetails((AtkResNode*)icon->IconImage),outer=GardenNodeDetails(icon->OuterResNode)});continue;}
            GardenPickerNodeDetails(&component->UldManager,nodes,ref budget,depth+1);
        }
    }
    private unsafe void CaptureGardenPicker(AddonContextIconMenu* menu,AgentHousingPlant* agent,List<PlantMenuItem> items,int marked,string check,DateTimeOffset now)
    {
        gardenPickerCheck=check;
        if(now<nextGardenPickerCapture)return;
        nextGardenPickerCapture=now.AddSeconds(2);
        var identities=new uint[Math.Min((int)agent->SelectableItemCount,140)];
        for(var i=0;i<identities.Length;i++)identities[i]=agent->SelectableItems[i].ItemCache==null?0:agent->SelectableItems[i].ItemCache->Id;
        var key=menu->AtkValuesSpan.Length+"|"+string.Join(",",identities);
        if(gardenPickerCaptureCounts.GetValueOrDefault(key)>=2)return;
        // Only bounded managed values leave the draw thread. No actor, pairing key,
        // address or native pointer is included; export writes on the existing worker.
        var values=new List<object>();var source=menu->AtkValuesSpan;
        for(var i=0;i<Math.Min(source.Length,1200);i++){
            var value=source[i];
            if(value.Type is AtkValueType.String or AtkValueType.ManagedString){var label=value.String.ToString();values.Add(new {index=i,type=value.Type.ToString(),text=label.Length>300?label[..300]:label});}
            else if(value.Type==AtkValueType.UInt)values.Add(new {index=i,type="UInt",number=(long)value.UInt});
            else if(value.Type==AtkValueType.Int)values.Add(new {index=i,type="Int",number=(long)value.Int});
            else values.Add(new {index=i,type=value.Type.ToString()});
        }
        var offered=new List<object>();var inventory=InventoryManager.Instance();
        for(var i=0;i<Math.Min((int)agent->SelectableItemCount,140);i++){
            var item=agent->SelectableItems[i];var id=item.ItemCache==null?0:item.ItemCache->Id;
            var slot=inventory!=null&&(item.InventoryType is InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4)&&item.InventorySlot<35?inventory->GetInventorySlot(item.InventoryType,item.InventorySlot):null;
            offered.Add(new {index=i,itemId=id,inventory=item.InventoryType.ToString(),slot=item.InventorySlot,liveItemId=slot==null?0:slot->ItemId});
        }
        var rows=new List<object>();var list=menu->AtkComponentList240;
        if(list!=null&&list->ListLength is >=0 and <=140){
            for(var i=0;i<list->ListLength;i++){
                var renderer=list->GetItemRenderer(i);if(renderer==null){rows.Add(new {index=i,missing=true});continue;}
                var icon=renderer->DragDropComponent==null?null:renderer->DragDropComponent->AtkComponentIcon;
                rows.Add(new {index=i,listItemIndex=renderer->ListItemIndex,owner=GardenNodeDetails((AtkResNode*)renderer->OwnerNode),dragIcon=icon==null?0:icon->IconId,image=icon==null?null:GardenNodeDetails((AtkResNode*)icon->IconImage)});
            }
        }
        var icons=new List<object>();var budget=600;GardenPickerNodeDetails(&menu->UldManager,icons,ref budget);
        var listInfo=list==null?null:new {length=list->ListLength,allocated=list->AllocatedItemRendererListLength,firstVisible=list->FirstVisibleItemIndex,visibleItems=list->NumVisibleItems,visibleRows=list->VisibleRowCount,columns=list->ColumnCount,updatePending=list->IsUpdatePending,scrollRefreshPending=list->IsScrollRefreshPending};
        var capture=new {at=now,check,marked,addonId=menu->Id,entryCount=menu->EntryCount,valueCount=source.Length,selectableCount=agent->SelectableItemCount,list=listInfo,required=items.ToArray(),values=values.ToArray(),offered=offered.ToArray(),rows=rows.ToArray(),icons=icons.ToArray()};
        if(gardenPickerCaptureCounts.Count>=8)gardenPickerCaptureCounts.Clear();
        gardenPickerCaptureCounts[key]=gardenPickerCaptureCounts.GetValueOrDefault(key)+1;
        var existing=gardenPickerCaptureKeys.IndexOf(key);
        if(existing>=0){gardenPickerCaptures[existing]=capture;return;}
        if(gardenPickerCaptures.Count==4){gardenPickerCaptures.RemoveAt(0);gardenPickerCaptureKeys.RemoveAt(0);}
        gardenPickerCaptures.Add(capture);gardenPickerCaptureKeys.Add(key);
    }
    private void ExportGardenPicker()
    {
        if(DiagnosticExportBusy||gardenPickerCaptures.Count==0)return;
        diagnosticExportError="";exportPath=null;
        var path=Path.Combine(Pi.GetPluginConfigDirectory(),"exports",$"equinox-picker-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        diagnosticExportTask=DiagnosticExportWriter.Start(path,new {schemaVersion=1,pluginVersion=typeof(Plugin).Assembly.GetName().Version?.ToString(),exportedAt=DateTimeOffset.UtcNow,pickers=gardenPickerCaptures.ToArray()},json,()=>[],()=>[]);
    }
    private void DrawGardenPickerDiagnostics()
    {
        ImGui.TextWrapped(gardenPickerCheck);
        ImGui.TextDisabled($"{gardenPickerCaptures.Count} recent picker snapshots retained this session.");
        if(ImGui.SmallButton("Capture again")){gardenPickerCaptureCounts.Clear();nextGardenPickerCapture=default;}
        ImGui.SameLine();
        ImGui.BeginDisabled(DiagnosticExportBusy||gardenPickerCaptures.Count==0);
        if(ImGui.SmallButton("Export picker details"))ExportGardenPicker();
        ImGui.EndDisabled();
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Open both the game's soil and seed pickers, then export this small report. It includes their item identities and row geometry, without your observation history or pairing key.");
        if(!string.IsNullOrEmpty(exportPath)&&Path.GetFileName(exportPath).StartsWith("equinox-picker-",StringComparison.Ordinal)){
            ImGui.SameLine();if(ImGui.SmallButton("Open export folder"))Dalamud.Utility.Util.OpenLink(Path.GetDirectoryName(exportPath)!);
            ImGui.TextWrapped(Path.GetFileName(exportPath));
        }
        if(diagnosticExportError.Length>0)ImGui.TextWrapped(diagnosticExportError);
    }
}
