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
        var key=plan.HouseId+":"+plan.Batch;
        if(remember)gardenPlanUndo[key]=plan.Definition;
        var now=DateTimeOffset.UtcNow;
        var e=new SyncEvent(Guid.NewGuid().ToString("N"),"garden.plan",now,WithWorldNames(actor),WithAddressNames(currentAddress),PlanEdit:new(plan.HouseId,plan.Batch,plan.At==DateTimeOffset.MinValue?null:plan.At,definition));
        if(!SyncValidation.CanSend(e,now)){gardenPlanEditStatus="Waiting for complete character and estate identity.";return;}
        KeepDiscovery(e,force:true);GardenActionRecorded();nextRosterRead=default;
        gardenPlanEditStatus=definition is null?"Plan reset · shared sync queued. Undo is available.":"Plan applied · shared sync queued.";
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
