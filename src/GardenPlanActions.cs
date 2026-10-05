using Dalamud.Bindings.ImGui;
using System.Numerics;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Dictionary<string,GardenPlanDefinition?> gardenPlanUndo=[];
    private string gardenPlanEditStatus="";
    private void ChangeGardenPlan(SharedGardenPlan plan,GardenPlanDefinition? definition,bool remember=true)
    {
        if(config.SharedRoster?.ProtocolVersion<12){gardenPlanEditStatus="Upload Journal V7.11.51 and refresh the website first.";return;}
        if(!Player.IsLoaded||currentAddress is null||SharedGardenLocation.Match(WithAddressNames(currentAddress),[plan])!=plan.HouseId)return;
        var actor=ReadActor();if(actor is null)return;
        if(GardenOrderPending(plan.HouseId)){gardenPlanEditStatus="Wait for the batch assignment to sync before changing its plan.";return;}
        var key=plan.HouseId+":"+plan.Batch;
        if(remember)gardenPlanUndo[key]=plan.Definition;
        var now=DateTimeOffset.UtcNow;
        var e=new SyncEvent(Guid.NewGuid().ToString("N"),"garden.plan",now,WithWorldNames(actor),WithAddressNames(currentAddress),PlanEdit:new(plan.HouseId,plan.Batch,plan.At==DateTimeOffset.MinValue?null:plan.At,definition));
        if(!SyncValidation.CanSend(e,now)){gardenPlanEditStatus="Waiting for complete character and estate identity.";return;}
        KeepDiscovery(e,force:true);GardenActionRecorded();nextRosterRead=default;
        gardenPlanEditStatus=definition is null?"Plan reset · shared sync queued. Undo is available.":"Plan applied · shared sync queued.";
    }
    private bool GardenOrderPending(string house)
    {
        var at=config.SharedRoster?.GardenBatchOrders?.FirstOrDefault(o=>o.HouseId==house)?.At;
        return config.Discoveries.Any(e=>e.Kind=="garden.batch-order"&&e.BatchOrder?.HouseId==house&&e.BatchOrder.BaseAt==at&&e.At>(at??DateTimeOffset.MinValue));
    }
    private string GardenResyncKey(string house,int patch)=>RegistrationScope+":"+house+":"+patch;
    private void DrawGardenBatchMapping(SharedGardenPlan plan)
    {
        if(!ImGui.CollapsingHeader("Batch mapping · left to right"))return;
        ImGui.TextWrapped((plan.Capacity==3?"Batch 1 = left · Batch 2 = middle · Batch 3 = right.":plan.Capacity==2?"Batch 1 = left · Batch 2 = right.":"This house has one batch.")+" Crops, timers, plans and history stay with their physical patch.");
        var visitor=GardenBedSyncSession.IsVisitor(config.SharedRoster,Player.CharacterName,WorldName(Player.HomeWorld.RowId)??"",plan.HouseId);
        if(visitor){
            if(ImGui.Button("Reset batch mapping / resync")){
                var patch=plan.PhysicalPatch>0?plan.PhysicalPatch:plan.Batch;
                config.GardenResyncBatches.Add(GardenResyncKey(plan.HouseId,patch));gardenBedSync=null;canceledBedSync=(plan.HouseId,patch);gardenSelectionFollow.Reset();Pi.SavePluginConfig(config);
                gardenPlanEditStatus="Sync beds is available again for this batch. Existing garden records were kept.";
            }
            ImGui.TextWrapped("Use Sync beds above, then follow the existing numbered steps through all eight beds.");return;
        }
        if(config.SharedRoster is not {ProtocolVersion:>=15} roster){ImGui.TextWrapped("Deploy Journal V7.11.69 and refresh it to enable shared batch reassignment.");return;}
        var pending=GardenOrderPending(plan.HouseId);
        var target=GardenGuideSelection();var selection=target is null?null:GardenTargetMap.SelectedBatch(target,GardenMappings(),GardenPlanSources());
        var matched=selection is {} pick&&pick.House==plan.HouseId;
        ImGui.TextWrapped(pending?"Assignment queued. Waiting for the shared order before another swap.":matched?$"Selected physical patch currently appears as Batch {selection!.Value.Batch}. Assign it below:":"Select a bed in the physical patch. Open its game menu once if its numbered identity is not recorded yet.");
        ImGui.BeginDisabled(!matched||pending||gardenBedSync is {Complete:false});
        for(var destination=1;destination<=plan.Capacity;destination++){
            if(destination>1)ImGui.SameLine();
            if(ImGui.Button("Assign to Batch "+destination)&&selection is {} selected&&selected.Batch!=destination&&currentAddress is {} address){
                var old=roster.GardenBatchOrders?.FirstOrDefault(o=>o.HouseId==plan.HouseId);var order=old?.Order??Enumerable.Range(1,plan.Capacity).ToArray();
                var now=DateTimeOffset.UtcNow;var actor=ReadActor();if(actor is null)continue;
                var e=new SyncEvent(Guid.NewGuid().ToString("N"),"garden.batch-order",now,WithWorldNames(actor),WithAddressNames(address),BatchOrder:new(plan.HouseId,GardenBatchOrdering.Swap(order,selected.Batch,destination),old?.At));
                if(SyncValidation.CanSend(e,now)){KeepDiscovery(e,force:true);GardenActionRecorded();gardenPlanUndo.Clear();nextRosterRead=default;plantingBatch=destination;selectedGardenBed=(plan.HouseId,destination,selected.Bed);gardenPlanEditStatus=$"Assigned to Batch {destination} · swapped labels · shared sync queued.";}
                else gardenPlanEditStatus="Assignment not saved: waiting for complete estate and character identity.";
            }
        }
        ImGui.EndDisabled();
    }
    private void DrawSavedGardenPlans(SharedGardenPlan plan)
    {
        ImGui.SetNextItemOpen(false,ImGuiCond.Once);
        if(!ImGui.CollapsingHeader("Saved plans###saved-garden-plans"))return;
        if(config.SharedRoster?.ProtocolVersion<12){ImGui.TextWrapped("Upload Journal V7.11.51 and refresh to sync favourites.");return;}
        var favourites=config.SharedRoster?.GardenFavourites??[];
        if(favourites.Length==0)ImGui.TextWrapped("Favourite crops and recipes on the website. They appear here after the Journal syncs.");
        foreach(var group in favourites.GroupBy(f=>f.PersonId))
        {
            ImGui.TextDisabled(group.First().PersonName);
            foreach(var favourite in group)
            {
                ImGui.PushID(favourite.PersonId+":"+favourite.Id);
                var width=ImGui.GetContentRegionAvail().X;
                if(ImGui.Button(FitOverviewText(favourite.Label,Math.Max(20,width-24))+"###apply",new Vector2(width,0)))ChangeGardenPlan(plan,GardenPlanEditing.Build(favourite,plan));
                if(ImGui.IsItemHovered())ImGui.SetTooltip(favourite.Label+"\nApply this layout to Batch "+plan.Batch+" immediately. Actual crops and care stay recorded.");
                ImGui.PopID();
            }
        }
    }
    private void DrawGardenPlanActions(SharedGardenPlan plan)
    {
        ImGui.Separator();
        var swap=GardenPlanEditing.Swap(plan.Definition);
        ImGui.BeginDisabled(swap is null||config.SharedRoster?.ProtocolVersion<12);
        if(ImGui.Button("Swap crops"))ChangeGardenPlan(plan,swap);
        ImGui.EndDisabled();ImGui.SameLine();
        ImGui.BeginDisabled(plan.Definition is null||config.SharedRoster?.ProtocolVersion<12);
        if(ImGui.Button("Reset plan"))ChangeGardenPlan(plan,null);
        ImGui.EndDisabled();
        var key=plan.HouseId+":"+plan.Batch;
        if(gardenPlanUndo.TryGetValue(key,out var previous))
        {
            ImGui.SameLine();if(ImGui.Button("Undo")){ChangeGardenPlan(plan,previous,false);gardenPlanUndo.Remove(key);}
        }
        if(gardenPlanEditStatus.Length>0)ImGui.TextWrapped(gardenPlanEditStatus);
    }
}
