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
    private readonly GardenMarkerSession gardenMarkerSession = new();
    private string gardenMarkerStatus="";
    private bool gardenMarkerError;
    private (string House,int Batch,int WrongBed,int NextBed)? gardenWrongBed;
    private bool FastGardenSync => plantingWindow?.IsOpen==true || activeGardenSession is not null || (adventureWindow?.IsOpen==true && AdventureGardenNavigation.HasGarden(adventureBoards.FirstOrDefault(b=>b.Id==adventureSelected)));
    private static string GardenSessionKey(SharedGardenPlan p)=>$"{p.HouseId}:{p.Batch}:{p.At:O}";
    private void StopGardenSession() { gardenMarkerSession.Reset();activeGardenSession=null; guidanceBed=null; gardenMarkerStatus="";gardenWrongBed=null; }
    private void RememberGuidanceBed(GardenMenu menu)
    {
        // Only a numbered empty-bed menu establishes identity; selected guide tabs never do.
        guidanceBed=menu.NumberedLocation() is not null?menu:null;
    }
    private readonly record struct PlantMenuItem(uint Id,uint Icon,bool Required,string Name);
    private unsafe List<PlantMenuItem> MarkerItems(AgentHousingPlant* agent,string crop,string soil)
    {
        LoadGardenPictures();
        var items=new List<PlantMenuItem>();
        var english=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
        var localized=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        void Add(uint id)
        {
            id%=1000000;if(id==0||items.Any(x=>x.Id==id))return;
            var row=english.GetRowOrDefault(id);if(row is null)return;
            var name=row.Value.Name.ToString();
            var required=string.Equals(name,soil,StringComparison.OrdinalIgnoreCase)||string.Equals(GardenCropName(name),crop,StringComparison.OrdinalIgnoreCase);
            items.Add(new(id,row.Value.Icon,required,localized.GetRowOrDefault(id)?.Name.ToString()??name));
        }
        for(var i=0;i<Math.Min((int)agent->SelectableItemCount,140);i++)
        {var item=agent->SelectableItems[i].ItemCache;if(item!=null)Add(item->Id);}
        // Main Gardening slots remain marked when the context picker is closed.
        for(var i=0;i<2;i++)Add(agent->SelectedItems[i].ItemId);
        return items;
    }
    private string gardenMarkerStage="idle";
    private bool gardenNativeOpen;
    private uint gardenNativeAddonId;
    private unsafe void DrawPlantingMarkers()
    {
        try { DrawPlantingMarkersCore(); }
        finally { CaptureGardenMarkerState(); }
    }
    private unsafe void DrawPlantingMarkersCore()
    {
        var gardeningOpen=plantingWindow.IsOpen||minimizedLaunchers.Contains("Planting");
        var adventureOpen=adventureWindow?.IsOpen==true||minimizedLaunchers.Contains("Adventure");
        adventureGardenNavigation.Set(adventureOpen?adventureBoards.FirstOrDefault(b=>b.Id==adventureSelected):null);
        if(!config.SyncEnabled||config.PairingKey.Length!=64||!Player.IsLoaded){StopGardenSession();gardenNativeOpen=false;gardenNativeAddonId=0;gardenMarkerStage="connection";gardenMarkerStatus="Waiting for a loaded character and paired connection.";return;}
        activeGardenSession=null;gardenMarkerError=false;gardenWrongBed=null;
        gardenMarkerStage="awaiting-bed";
        gardenMarkerStatus="Automatic guide · Open a numbered bed in game. A fresh batch starts at Bed 1.";
        try
        {
            var menu=guidanceBed;var now=DateTimeOffset.UtcNow;
            if(menu is null&&!gardeningOpen&&!adventureGardenNavigation.HasGardens){gardenMarkerSession.Reset();gardenNativeOpen=false;gardenNativeAddonId=0;gardenMarkerStage="idle";gardenMarkerStatus="";return;}
            var context=Volatile.Read(ref capturedContext);
            var current=context?.CandidateAt(now);
            var agent=AgentHousingPlant.Instance();
            var gardening=(AtkUnitBase*)GardenGui.GetAddonByName("HousingGardening").Address;
            var nativeOpen=agent!=null&&agent->IsAgentActive()&&gardening!=null&&gardening->IsVisible;
            gardenNativeOpen=nativeOpen;gardenNativeAddonId=gardening==null?0u:gardening->Id;
            var retained=gardenMarkerSession.Matches(menu,current,now,(nint)gardening,gardenNativeAddonId,nativeOpen,context?.Current);
            // Both guides share the native planting session. Opening, closing or
            // minimizing a Companion window must not discard the numbered bed.
            if(!gardeningOpen&&!adventureGardenNavigation.HasGardens){
                if(!nativeOpen&&menu is not null&&now-menu.OpenedAt>TimeSpan.FromMinutes(1))guidanceBed=null;
                gardenMarkerStage="guide-hidden";gardenMarkerStatus="Open Gardening or a matching Adventure stop to show item borders.";return;
            }
            if(menu is null)return;
            // Fresh numbered menus still show existing wrong-bed/occupied-bed guidance
            // before the native planting window opens. Only its display can outlive that menu.
            if((!retained&&!menu.Matches(now,current))||menu.NumberedLocation() is not {} location){gardenMarkerStage="context";gardenMarkerStatus="Waiting for matching live bed context. Reopen the numbered bed if the planting window changed.";return;}
            var plans=GardenPlanSources();var house=SharedGardenLocation.Match(currentAddress,plans);
            var adventureAtHouse=house is not null&&adventureGardenNavigation.Contains(house);
            if(!gardeningOpen&&!adventureAtHouse){gardenMarkerStage="adventure-scope";gardenMarkerStatus="This house is not in the current adventure stop.";return;}
            var displayed=AdventureGardenNavigation.UseStandaloneBatch(gardeningOpen,adventureAtHouse)?plans.FirstOrDefault(p=>p.HouseId==house&&p.Batch==plantingBatch):null;
            if(displayed is not null&&location.Patch!=(displayed.PhysicalPatch>0?displayed.PhysicalPatch:displayed.Batch))
            { gardenMarkerStage="wrong-batch";gardenMarkerError=true;gardenMarkerStatus=$"Wrong batch: you opened Batch {location.Patch} in game, but the guide is showing Batch {displayed.Batch}. Open the matching batch in game or switch the guide tab. No items marked.";return; }
            var matches=plans.Where(p=>p.HouseId==house&&(p.PhysicalPatch>0?p.PhysicalPatch:p.Batch)==location.Patch).Take(2).ToArray();
            if(matches.Length!=1){gardenMarkerStage="plan-match";gardenMarkerStatus="Waiting for an unambiguous saved plan for this physical batch.";return;}
            var source=matches[0];
            if(!GardenGuidance.ValidOrder(source)){gardenMarkerStage="plan-order";gardenMarkerStatus="Save a fresh website plan starting at Bed 1 before using markers.";return;}
            activeGardenSession=GardenSessionKey(source);
            var plan=EffectiveGardenPlan(source);
            if(plan.CompletedAt is not null || plan.Beds.Any(b=>b.Crop.Length>0)&&plan.Beds.Where(b=>b.Crop.Length>0).All(b=>b.Status=="confirmed")){StopGardenSession();gardenMarkerStage="complete";gardenMarkerStatus="Planting complete · confirmed by game actions.";return;}
            var next=GardenGuidance.Next(plan);
            if(next is null||next.Bed!=location.Bed){gardenMarkerStage="next-bed";if(next is not null)gardenWrongBed=(source.HouseId,source.Batch,location.Bed,next.Bed);gardenMarkerError=next is not null;gardenMarkerStatus=next is null?"Waiting for the remaining planting steps.":$"Wrong bed: you opened Bed {location.Bed}; the plan expects Bed {next.Bed}. No items marked.";return;}
            if(menu.EmptyLocation() is null){gardenMarkerStage="occupied";gardenMarkerStatus=$"Bed {location.Bed} is occupied. Review its planned/actual crop before replacing anything.";return;}
            var soil=GardenPlantRequirement.Soil(next);
            gardenMarkerStatus=$"Bed {next.Bed}: {next.Crop} · {soil}";
            if(!nativeOpen){gardenMarkerStage="window-closed";return;}
            var items=MarkerItems(agent,next.Crop,soil);
            // Native addons can retain visibility flags and old bounds after their
            // contents disappear. Only currently displayed covers hide a marker.
            Span<Vector4> covers=stackalloc Vector4[3];var coverCount=0;
            if(GardenMarkerCover("SelectYesno") is {} confirmation)covers[coverCount++]=confirmation;
            if(GardenMarkerCover("ItemDetail",AtkTooltipType.Item) is {} detail)covers[coverCount++]=detail;
            if(GardenMarkerCover("Tooltip",AtkTooltipType.Text) is {} tooltip)covers[coverCount++]=tooltip;
            var picker=(AtkUnitBase*)GardenGui.GetAddonByName("ContextIconMenu").Address;
            var pickerOpen=picker!=null&&picker->IsVisible&&agent->ContextAddonId==picker->Id;
            // ImGui overlays sit above native windows. Never outline the covered
            // Gardening slots through the foreground soil/seed picker.
            gardenMarkerStage=pickerOpen?"picker":"slots";
            var pickerCheck="";
            var marked=pickerOpen?MarkGardenPicker((AddonContextIconMenu*)picker,agent,items,covers[..coverCount],out pickerCheck):MarkGardenItems(gardening,items,covers[..coverCount]);
            if(pickerOpen)CaptureGardenPicker((AddonContextIconMenu*)picker,agent,items,marked,pickerCheck,now);
            gardenMarkerStatus+=marked>0?" · Green: required. Red: different item.":" · Choose soil and seed; follow the required names above.";
            if(pickerOpen&&!items.Any(x=>x.Required))gardenMarkerStatus+=" Required item is not offered; check inventory.";

        }
        catch(Exception e){gardenMarkerStage="error";gardenMarkerStatus="Item marker unavailable; follow the bed guide.";errorJournal.Record("garden-marker","Could not read planting item menu",exceptionType:e.GetType().Name);}
    }
    private static unsafe bool MarkerVisible(AtkResNode* node)
    {
        if(node==null)return false;
        for(var n=0;node!=null&&n<40;n++,node=node->ParentNode)if(!node->IsVisible())return false;
        return true;
    }
    private static unsafe Vector4? GardenMarkerCover(string name,AtkTooltipType tooltipType=AtkTooltipType.None)
    {
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
        if(addon==null||!GardenPlantRequirement.VisibleCover(addon->IsVisible,addon->Alpha,MarkerVisible(addon->RootNode)))return null;
        if(tooltipType!=AtkTooltipType.None){
            var stage=AtkStage.Instance();
            if(stage==null||!MarkerVisible(stage->TooltipManager.TargetNode)||((AtkTooltipType)stage->TooltipManager.TooltipType&tooltipType)==0)return null;
        }
        Bounds bounds;addon->GetWindowBounds(&bounds);
        return bounds.Width>0&&bounds.Height>0?new Vector4(bounds.Pos1.X,bounds.Pos1.Y,bounds.Pos2.X,bounds.Pos2.Y):null;
    }
    private static unsafe bool GardenItemBounds(AtkResNode* node,out Vector4 rectangle)
    {
        rectangle=default;if(!MarkerVisible(node))return false;
        Bounds bounds;node->GetBounds(&bounds);
        rectangle=new(bounds.Pos1.X,bounds.Pos1.Y,bounds.Pos2.X,bounds.Pos2.Y);
        return GardenPlantRequirement.ValidMarkerBounds(rectangle);
    }
    private static unsafe bool OutlineGardenItem(AtkResNode* node,bool correct,HashSet<nint> drawn,ReadOnlySpan<Vector4> covers)
    {
        if(!GardenItemBounds(node,out var bounds))return false;
        return OutlineGardenBounds(node,bounds,correct,drawn,covers);
    }
    private static unsafe bool OutlineGardenBounds(AtkResNode* node,Vector4 bounds,bool correct,HashSet<nint> drawn,ReadOnlySpan<Vector4> covers)
    {
        Span<Vector4> regions=stackalloc Vector4[GardenMarkerClip.MaxRegions];
        var count=GardenMarkerClip.VisibleRegions(new(bounds.X-5,bounds.Y-5,bounds.Z+5,bounds.W+5),covers,regions);
        if(count==0||!drawn.Add((nint)node))return false;
        var offset=ImGui.GetMainViewport().Pos;
        var thickness=Math.Clamp((bounds.Z-bounds.X)/18f,2,4);
        var draw=ImGui.GetForegroundDrawList();
        for(var i=0;i<count;i++){
            var clip=regions[i];
            draw.PushClipRect(offset+new Vector2(clip.X,clip.Y),offset+new Vector2(clip.Z,clip.W),true);
            try{draw.AddRect(offset+new Vector2(bounds.X-2,bounds.Y-2),offset+new Vector2(bounds.Z+2,bounds.W+2),correct?0xff65ee83:0xff5757f2,3,ImDrawFlags.None,thickness);}
            finally{draw.PopClipRect();}
        }
        return true;
    }
    private static unsafe uint GardenTooltipItem(AtkTooltipManager.AtkTooltipInfo* info,List<PlantMenuItem> offered)
    {
        if(info==null)return 0;
        if(info->Type==AtkTooltipType.Text)
            return GardenPlantRequirement.TooltipItem(GardenMenuText.Read(info->AtkTooltipArgs.TextArgs.Text),offered.Select(x=>(x.Id,x.Name)));
        if((info->Type&AtkTooltipType.Item)==0)return 0;
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
    private static unsafe int MarkGardenItems(AtkUnitBase* addon,List<PlantMenuItem> items,ReadOnlySpan<Vector4> covers)
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
                var id=GardenTooltipItem(info,items);
                if(GardenPlantRequirement.Required(id,allowed) is {} correct)exact[(nint)node]=correct;
            }
        }
        // Prefer the visible item image. An invalid icon container must not consume
        // the exact tooltip binding: its validated bounds remain a fallback.
        var used=new HashSet<nint>();var nodes=600;
        var marked=MarkGardenIcons(&addon->UldManager,items,exact,drawn,used,covers,ref nodes,0);
        foreach(var match in exact)if(!used.Contains(match.Key)&&OutlineGardenItem((AtkResNode*)match.Key,match.Value,drawn,covers))marked++;
        return marked;
    }
    private static unsafe uint[] GardenPickerRowItems(AddonContextIconMenu* menu,List<PlantMenuItem> items)
    {
        var values=menu->AtkValuesSpan;
        if(values.Length<=4||values[4].Type!=AtkValueType.UInt||values[4].UInt is 0 or >140)return [];
        var count=(int)values[4].UInt;
        uint[]? resolved=null;
        // ContextIconMenu has shipped both six- and eight-value entry records.
        // Accept a layout only when EVERY displayed localized name resolves to
        // an offered item; contradictory layouts never select a colour.
        foreach(var stride in new[]{6,8}){
            if(values.Length<=13+(count-1)*stride)continue;
            var candidate=new uint[count];var valid=true;
            for(var i=0;i<count;i++){
                var value=values[13+i*stride];
                if(value.Type is not (AtkValueType.String or AtkValueType.ManagedString)){valid=false;break;}
                candidate[i]=GardenPlantRequirement.TooltipItem(GardenMenuText.Read(value.String),items.Select(x=>(x.Id,x.Name)));
                if(candidate[i]==0){valid=false;break;}
            }
            if(!valid)continue;
            if(resolved is not null&&!resolved.SequenceEqual(candidate))return [];
            resolved=candidate;
        }
        return resolved??[];
    }
    private static unsafe AtkComponentIcon* GardenRendererIcon(AtkUldManager* uld,uint expected,ref int budget,int depth=0)
    {
        if(uld==null||uld->NodeList==null||depth>6)return null;
        for(var i=0;i<uld->NodeListCount&&budget-->0;i++){
            var node=uld->NodeList[i];if(!MarkerVisible(node)||(int)node->Type<1000)continue;
            var component=node->GetComponent();if(component==null)continue;
            var type=component->GetComponentType();
            var icon=type==ComponentType.Icon?(AtkComponentIcon*)component:type==ComponentType.DragDrop?((AtkComponentDragDrop*)component)->AtkComponentIcon:null;
            if(icon!=null){if(icon->IconId==expected&&MarkerVisible((AtkResNode*)icon->IconImage))return icon;continue;}
            var child=GardenRendererIcon(&component->UldManager,expected,ref budget,depth+1);if(child!=null)return child;
        }
        return null;
    }
    private static unsafe int MarkGardenPicker(AddonContextIconMenu* menu,AgentHousingPlant* agent,List<PlantMenuItem> items,ReadOnlySpan<Vector4> covers,out string check)
    {
        var list=menu->AtkComponentList240;var inventory=InventoryManager.Instance();
        var rowItems=GardenPickerRowItems(menu,items);var count=rowItems.Length;
        check=list==null?"Picker list unavailable.":inventory==null?"Inventory unavailable.":count==0?"Picker names/count did not resolve to offered items.":list->ListLength!=count?$"Picker rows disagree: {count} named items, {list->ListLength} list rows.":list->IsUpdatePending||list->IsScrollRefreshPending?"Picker layout is updating.":"";
        if(check.Length>0)return 0;
        var marked=0;var drawn=new HashSet<nint>();
        var verifiedRows=0;var liveRows=0;var loadedIcons=0;var validBounds=0;
        // Draw only live logical rows. Walking the entire addon also visits its
        // reusable renderer templates, which retain old icons over empty space.
        for(var i=0;i<count;i++)
        {
            var item=items.FirstOrDefault(x=>x.Id==rowItems[i]);if(item.Id==0)continue;
            var verified=false;
            for(var j=0;j<Math.Min((int)agent->SelectableItemCount,140);j++){
                var offered=agent->SelectableItems[j];var cache=offered.ItemCache;
                if(cache==null||cache->Id%1000000!=item.Id||offered.InventoryType is not (InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4)||offered.InventorySlot>=35)continue;
                var slot=inventory->GetInventorySlot(offered.InventoryType,offered.InventorySlot);
                if(slot!=null&&slot->ItemId%1000000==item.Id){verified=true;break;}
            }
            if(!verified)continue;
            verifiedRows++;
            var renderer=list->GetItemRenderer(i);if(renderer==null||renderer->ListItemIndex!=i||!MarkerVisible((AtkResNode*)renderer->OwnerNode))continue;
            liveRows++;
            var icon=renderer->DragDropComponent!=null?renderer->DragDropComponent->AtkComponentIcon:null;
            if(icon==null){var budget=80;icon=GardenRendererIcon(&renderer->UldManager,item.Icon,ref budget);}
            if(icon==null||icon->IconId!=item.Icon||!icon->IsIconLoaded())continue;
            loadedIcons++;
            var node=(AtkResNode*)icon->IconImage;
            if(!GardenItemBounds(node,out var bounds)||!GardenItemBounds((AtkResNode*)renderer->OwnerNode,out var rowBounds)||!GardenPlantRequirement.WithinRow(bounds,rowBounds))continue;
            validBounds++;
            if(OutlineGardenBounds(node,bounds,item.Required,drawn,covers))marked++;
        }
        check=$"Picker: {count} items · {verifiedRows} inventory matches · {liveRows} live rows · {loadedIcons} loaded icons · {validBounds} attached bounds · {marked} borders.";
        return marked;
    }
    private static unsafe int MarkGardenIcons(AtkUldManager* uld,List<PlantMenuItem> items,Dictionary<nint,bool> exact,HashSet<nint> drawn,HashSet<nint> used,ReadOnlySpan<Vector4> covers,ref int budget,int depth)
    {
        if(uld==null||uld->NodeList==null||depth>8)return 0;
        var marked=0;
        Span<nint> bindings=stackalloc nint[5];Span<nint> candidates=stackalloc nint[4];
        for(var i=0;i<uld->NodeListCount&&budget-->0;i++)
        {
            var node=uld->NodeList[i];if(!MarkerVisible(node)||(int)node->Type<1000)continue;
            var component=node->GetComponent();if(component==null)continue;
            var type=component->GetComponentType();
            var icon=type==ComponentType.Icon?(AtkComponentIcon*)component:type==ComponentType.DragDrop?((AtkComponentDragDrop*)component)->AtkComponentIcon:null;
            if(icon!=null)
            {
                var bindingCount=0;bool? correct=null;var conflict=false;
                candidates[0]=(nint)icon->OuterResNode;candidates[1]=(nint)node;candidates[2]=(nint)icon->IconImage;candidates[3]=(nint)icon->OwnerNode;
                foreach(var candidate in candidates)
                    if(exact.TryGetValue(candidate,out var value)){if(correct.HasValue&&correct.Value!=value)conflict=true;correct=value;bindings[bindingCount++]=candidate;}
                var parent=node->ParentNode;
                for(var n=0;bindingCount==0&&parent!=null&&n<40;n++,parent=parent->ParentNode)
                    if(exact.TryGetValue((nint)parent,out var value)){correct=value;bindings[bindingCount++]=(nint)parent;}
                if(conflict){foreach(var binding in bindings[..bindingCount])used.Add(binding);continue;}
                if(bindingCount==0)correct=GardenPlantRequirement.IconMatch(icon->IconId,items.Select(x=>(x.Icon,x.Required)));
                if(correct is {} match){
                    // OuterResNode is an interaction target, not necessarily the
                    // visible square. Select valid geometry before retiring bindings.
                    candidates[0]=(nint)icon->IconImage;candidates[1]=(nint)icon->OuterResNode;candidates[2]=(nint)node;candidates[3]=(nint)icon->OwnerNode;
                    foreach(var candidate in candidates){
                        var target=(AtkResNode*)candidate;
                        if(!GardenItemBounds(target,out var bounds))continue;
                        foreach(var binding in bindings[..bindingCount])used.Add(binding);
                        if(OutlineGardenBounds(target,bounds,match,drawn,covers))marked++;
                        break;
                    }
                }
                continue;
            }
            marked+=MarkGardenIcons(&component->UldManager,items,exact,drawn,used,covers,ref budget,depth+1);
        }
        return marked;
    }
}

