using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Bounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    [PluginService] internal static IGameGui GardenGui { get; private set; } = null!;
    private string? activeGardenSession;
    private GardenMenu? guidanceBed;
    private string gardenMarkerStatus="";
    private bool gardenMarkerError;
    private (string House,int Batch,int WrongBed,int NextBed)? gardenWrongBed;
    private bool FastGardenSync => plantingWindow?.IsOpen==true || activeGardenSession is not null;
    private static string GardenSessionKey(SharedGardenPlan p)=>$"{p.HouseId}:{p.Batch}:{p.At:O}";
    private void StopGardenSession() { activeGardenSession=null; guidanceBed=null; gardenMarkerStatus="";gardenWrongBed=null; }
    private void RememberGuidanceBed(GardenMenu menu)
    {
        // Only a numbered empty-bed menu establishes identity; selected guide tabs never do.
        guidanceBed=menu.NumberedLocation() is not null?menu:null;
    }
    private readonly record struct PlantMenuItem(uint Id,uint Icon,bool Required);
    private unsafe List<PlantMenuItem> MarkerItems(AgentHousingPlant* agent,string crop,string soil)
    {
        LoadGardenPictures();
        var items=new List<PlantMenuItem>();
        var english=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
        void Add(uint id)
        {
            id%=1000000;if(id==0||items.Any(x=>x.Id==id))return;
            var row=english.GetRowOrDefault(id);if(row is null)return;
            var name=row.Value.Name.ToString();
            var required=string.Equals(name,soil,StringComparison.OrdinalIgnoreCase)||string.Equals(GardenCropName(name),crop,StringComparison.OrdinalIgnoreCase);
            items.Add(new(id,row.Value.Icon,required));
        }
        for(var i=0;i<Math.Min((int)agent->SelectableItemCount,140);i++)
        {var item=agent->SelectableItems[i].ItemCache;if(item!=null)Add(item->Id);}
        // Main Gardening slots remain marked when the context picker is closed.
        for(var i=0;i<2;i++)Add(agent->SelectedItems[i].ItemId);
        return items;
    }
    private unsafe void DrawPlantingMarkers()
    {
        if(!plantingWindow.IsOpen&&!minimizedLaunchers.Contains("Planting")){StopGardenSession();return;}
        if(!config.SyncEnabled||config.PairingKey.Length!=64||!Player.IsLoaded){StopGardenSession();return;}
        activeGardenSession=null;gardenMarkerError=false;gardenWrongBed=null;
        gardenMarkerStatus="Automatic guide · Open a numbered bed in game. A fresh batch starts at Bed 1.";
        var menu=guidanceBed;var now=DateTimeOffset.UtcNow;
        var current=Volatile.Read(ref capturedContext)?.CandidateAt(now);
        if(menu is null||!menu.Matches(now,current)||menu.NumberedLocation() is not {} location)return;
        var plans=GardenPlanSources();var house=SharedGardenLocation.Match(currentAddress,plans);
        var displayed=plans.FirstOrDefault(p=>p.HouseId==house&&p.Batch==plantingBatch);
        if(displayed is not null&&location.Patch!=(displayed.PhysicalPatch>0?displayed.PhysicalPatch:displayed.Batch))
        { gardenMarkerError=true;gardenMarkerStatus=$"Wrong batch: you opened Batch {location.Patch} in game, but the guide is showing Batch {displayed.Batch}. Open the matching batch in game or switch the guide tab. No items marked.";return; }
        var matches=plans.Where(p=>p.HouseId==house&&(p.PhysicalPatch>0?p.PhysicalPatch:p.Batch)==location.Patch).Take(2).ToArray();
        if(matches.Length!=1){gardenMarkerStatus="Waiting for an unambiguous saved plan for this physical batch.";return;}
        var source=matches[0];
        if(!GardenGuidance.ValidOrder(source)){gardenMarkerStatus="Save a fresh website plan starting at Bed 1 before using markers.";return;}
        activeGardenSession=GardenSessionKey(source);
        var plan=EffectiveGardenPlan(source);
        if(plan.CompletedAt is not null || plan.Beds.Any(b=>b.Crop.Length>0)&&plan.Beds.Where(b=>b.Crop.Length>0).All(b=>b.Status=="confirmed")){StopGardenSession();gardenMarkerStatus="Planting complete · confirmed by game actions.";return;}
        var next=GardenGuidance.Next(plan);
        if(next is null||next.Bed!=location.Bed){if(next is not null)gardenWrongBed=(source.HouseId,source.Batch,location.Bed,next.Bed);gardenMarkerError=next is not null;gardenMarkerStatus=next is null?"Waiting for the remaining planting steps.":$"Wrong bed: you opened Bed {location.Bed}; the plan expects Bed {next.Bed}. No items marked.";return;}
        if(menu.EmptyLocation() is null){gardenMarkerStatus=$"Bed {location.Bed} is occupied. Review its planned/actual crop before replacing anything.";return;}
        var soil=GardenPlantRequirement.Soil(next);
        gardenMarkerStatus=$"Bed {next.Bed}: {next.Crop} · {soil}";
        try
        {
            var agent=AgentHousingPlant.Instance();
            if(agent==null||!agent->IsAgentActive())return;
            var items=MarkerItems(agent,next.Crop,soil);
            var gardening=(AtkUnitBase*)GardenGui.GetAddonByName("HousingGardening").Address;
            if(gardening==null||!gardening->IsVisible)return;
            var picker=(AtkUnitBase*)GardenGui.GetAddonByName("ContextIconMenu").Address;
            var pickerOpen=picker!=null&&picker->IsVisible&&agent->ContextAddonId==picker->Id;
            // ImGui overlays sit above native windows. Never outline the covered
            // Gardening slots through the foreground soil/seed picker.
            var marked=pickerOpen?MarkGardenItems(picker,items,agent):MarkGardenItems(gardening,items);
            gardenMarkerStatus+=marked>0?" · Green: required. Red: different item.":" · Choose soil and seed; follow the required names above.";
            if(pickerOpen&&!items.Any(x=>x.Required))gardenMarkerStatus+=" Required item is not offered; check inventory.";

        }
        catch(Exception e){gardenMarkerStatus="Item marker unavailable; follow the bed guide.";errorJournal.Record("garden-marker","Could not read planting item menu",exceptionType:e.GetType().Name);}
    }
    private static unsafe bool MarkerVisible(AtkResNode* node)
    {
        if(node==null)return false;
        for(var n=0;node!=null&&n<40;n++,node=node->ParentNode)if(!node->IsVisible())return false;
        return true;
    }
    private static unsafe bool OutlineGardenItem(AtkResNode* node,bool correct,HashSet<nint> drawn)
    {
        if(!MarkerVisible(node)||!drawn.Add((nint)node))return false;
        Bounds bounds;node->GetBounds(&bounds);
        // Ignore tooltips bound to a whole window/text row, and invalid geometry.
        if(bounds.Width<12||bounds.Height<12||bounds.Width>256||bounds.Height>256||(float)bounds.Width/bounds.Height is <.55f or >1.8f)return false;
        var offset=ImGui.GetMainViewport().Pos;
        var thickness=Math.Clamp(bounds.Width/18f,2,4);
        ImGui.GetForegroundDrawList().AddRect(offset+new Vector2(bounds.Pos1.X-2,bounds.Pos1.Y-2),offset+new Vector2(bounds.Pos2.X+2,bounds.Pos2.Y+2),correct?0xff65ee83:0xff5757f2,3,ImDrawFlags.None,thickness);
        return true;
    }
    private static unsafe uint GardenTooltipItem(AtkTooltipManager.AtkTooltipInfo* info)
    {
        if(info==null||(info->Type&AtkTooltipType.Item)==0)return 0;
        var item=info->AtkTooltipArgs.ItemArgs;
        if(item.Kind==DetailKind.InventoryItem)
        {
            // Only player inventory slots can be offered to this outdoor planting agent.
            if(item.InventoryType is not (InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4)||item.Slot is <0 or >=35)return 0;
            var inventory=InventoryManager.Instance();if(inventory==null)return 0;
            var slot=inventory->GetInventorySlot(item.InventoryType,item.Slot);return slot==null?0:slot->ItemId%1000000;
        }
        return item.Kind==DetailKind.Item&&item.ItemId>0?(uint)item.ItemId%1000000:0;
    }
    private static unsafe int MarkGardenItems(AtkUnitBase* addon,List<PlantMenuItem> items,AgentHousingPlant* plantingAgent=null)
    {
        if(addon==null||!addon->IsVisible||items.Count==0)return 0;
        var drawn=new HashSet<nint>();var exact=new Dictionary<nint,bool>();
        var allowed=items.ToDictionary(x=>x.Id,x=>x.Required);var stage=AtkStage.Instance();
        if(stage!=null)
        {
            var budget=4096;
            foreach(var entry in stage->TooltipManager.TooltipMap)
            {
                if(--budget<0)break;
                var info=entry.Item2.Value;var node=entry.Item1.Value;
                if(info==null||info->ParentId!=addon->Id||!MarkerVisible(node))continue;
                var id=GardenTooltipItem(info);
                if(GardenPlantRequirement.Required(id,allowed) is {} correct)exact[(nint)node]=correct;
            }
        }
        if(plantingAgent!=null)MapGardenPickerEntries((AddonContextIconMenu*)addon,plantingAgent,items,exact);
        // Native item tooltips carry the exact item ID or inventory slot, even when
        // the selector renders icons without labels. No screen-coordinate guesses.
        var marked=0;
        foreach(var match in exact)if(OutlineGardenItem((AtkResNode*)match.Key,match.Value,drawn))marked++;
        var nodes=600;
        marked+=MarkGardenIcons(&addon->UldManager,items,exact,drawn,ref nodes,0);
        return marked;
    }
    private static unsafe void MapGardenPickerEntries(AddonContextIconMenu* menu,AgentHousingPlant* agent,List<PlantMenuItem> items,Dictionary<nint,bool> exact)
    {
        var list=menu->AtkComponentList240;var inventory=InventoryManager.Instance();var count=menu->EntryCount;
        if(list==null||inventory==null||count is <=0 or >140||count!=agent->SelectableItemCount||list->ListLength!=count)return;
        // Use the native list item's logical index, not screen position or a seed-bag image.
        // Validate the current inventory slot and displayed icon before drawing a border.
        for(var i=0;i<count;i++)
        {
            var offered=agent->SelectableItems[i];var cache=offered.ItemCache;if(cache==null)continue;
            if(offered.InventoryType is not (InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4)||offered.InventorySlot>=35)continue;
            var slot=inventory->GetInventorySlot(offered.InventoryType,offered.InventorySlot);if(slot==null)continue;
            var item=items.FirstOrDefault(x=>x.Id==cache->Id%1000000);if(item.Id==0)continue;
            var renderer=list->GetItemRenderer(i);if(renderer==null||!MarkerVisible((AtkResNode*)renderer->OwnerNode))continue;
            var icon=renderer->DragDropComponent!=null?renderer->DragDropComponent->AtkComponentIcon:null;
            // A scrolling list need not allocate a renderer for every inventory
            // entry. GetItemRenderer resolves the current logical item safely.
            var visibleIcon=icon!=null?icon->IconId:list->ItemRendererList!=null&&i<list->AllocatedItemRendererListLength?list->ItemRendererList[i].IconId:0;
            if(!GardenPlantRequirement.PickerEntryMatches(count,agent->SelectableItemCount,list->ListLength,i,renderer->ListItemIndex,cache->Id%1000000,slot->ItemId%1000000,item.Icon,visibleIcon))continue;
            var node=icon!=null&&icon->OuterResNode!=null?(nint)icon->OuterResNode:(nint)renderer->OwnerNode;
            // An independently registered exact tooltip wins over a list binding.
            if(!exact.ContainsKey(node))exact[node]=item.Required;
        }
    }
    private static unsafe int MarkGardenIcons(AtkUldManager* uld,List<PlantMenuItem> items,Dictionary<nint,bool> exact,HashSet<nint> drawn,ref int budget,int depth)
    {
        if(uld==null||uld->NodeList==null||depth>8)return 0;
        var marked=0;
        for(var i=0;i<uld->NodeListCount&&budget-->0;i++)
        {
            var node=uld->NodeList[i];if(!MarkerVisible(node)||(int)node->Type<1000)continue;
            var component=node->GetComponent();if(component==null)continue;
            var type=component->GetComponentType();
            var icon=type==ComponentType.Icon?(AtkComponentIcon*)component:type==ComponentType.DragDrop?((AtkComponentDragDrop*)component)->AtkComponentIcon:null;
            if(icon!=null)
            {
                var target=icon->OuterResNode!=null?icon->OuterResNode:node;
                var hasExact=false;bool? correct=null;
                foreach(var candidate in new[]{(nint)target,(nint)node,(nint)icon->IconImage,(nint)icon->OwnerNode})
                    if(exact.TryGetValue(candidate,out var value)){hasExact=true;correct=value;break;}
                var parent=node->ParentNode;
                for(var n=0;!hasExact&&parent!=null&&n<40;n++,parent=parent->ParentNode)
                    if(exact.TryGetValue((nint)parent,out var value)){hasExact=true;correct=value;}
                // An exact node was already outlined above. Never overwrite it with icon inference.
                if(!hasExact){correct=GardenPlantRequirement.IconMatch(icon->IconId,items.Select(x=>(x.Icon,x.Required)));if(correct is {} match&&OutlineGardenItem(target,match,drawn))marked++;}
                continue;
            }
            marked+=MarkGardenIcons(&component->UldManager,items,exact,drawn,ref budget,depth+1);
        }
        return marked;
    }
}
