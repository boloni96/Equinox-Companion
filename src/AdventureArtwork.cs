using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private bool adventureShowActual;
 private (string House,int Batch,int Bed)? adventureOpenBed;
 private readonly GardenSelectionFollow adventureGardenSelectionFollow=new();
 private void DrawAdventureHeading(string icon,string title)
 {
  var at=ImGui.GetCursorScreenPos();var size=ImGui.GetTextLineHeight()*2;
  GardenImage("assets/category-icons/"+icon+".png",at,new Vector2(size));
  ImGui.Dummy(new Vector2(size));ImGui.SameLine();ImGui.TextWrapped(title);
 }
 private void DrawAdventureGardens(string houseId)
 {
  var sources=GardenPlanSources().Where(p=>p.HouseId==houseId).OrderBy(p=>p.Batch).ToArray();
  if(sources.Length==0){ImGui.TextWrapped("Waiting for this house’s garden records. Confirmed garden observations will appear after Companion syncs.");return;}
  if(GardenGuideSelection() is {} gameTarget&&gameTarget.Address==currentAddress&&atOutdoorEstate&&DateTimeOffset.UtcNow-gameTarget.ObservedAt<TimeSpan.FromSeconds(2)&&gardenBedSync is not {Complete:false}){
   var selection=adventureGardenSelectionFollow.Update(gameTarget,GardenMappings(),sources,DateTimeOffset.UtcNow);
   if(selection is {} selected&&selected.House==houseId)adventureOpenBed=selected;
  }else adventureGardenSelectionFollow.Reset();
  foreach(var source in sources){
   ImGui.PushID("adventure-garden-"+source.HouseId+"-"+source.Batch);
   var plan=EffectiveGardenPlan(source);
   var planned=plan.Beds.Where(b=>b.Crop.Length>0).ToArray();
   var complete=plan.CompletedAt is not null||planned.Length>0&&planned.All(b=>b.Status=="confirmed");
   var requested=adventureOpenBed is {} target&&target.House==plan.HouseId&&target.Batch==plan.Batch;
   ImGui.SetNextItemOpen(requested||sources.Length==1,requested?ImGuiCond.Always:ImGuiCond.Once);
   if(ImGui.CollapsingHeader($"{(complete?"✓ ":"")}Batch {plan.Batch}")){
    if(requested&&adventureOpenBed is {} focus){selectedGardenBed=(focus.House,focus.Batch,focus.Bed);ImGui.SetScrollHereY(.15f);adventureOpenBed=null;}
    plan=WithQueuedGardenBeds(plan);
    var showPlan=!adventureShowActual&&planned.Length>0&&!complete;
    ImGui.TextWrapped(GardenOverview.Summary([plan],DateTimeOffset.UtcNow));
    if(showPlan&&GardenGuidance.Next(plan) is {} next)ImGui.TextWrapped($"Next: Bed {next.Bed} · {next.Crop} · {GardenPlantRequirement.Soil(next)}");
    if(BedSyncMatches(plan)&&gardenBedSync is {Complete:false} sync){
     ImGui.TextWrapped(sync.Status);
     if(ImGui.SmallButton("Cancel bed sync")){gardenBedSync=null;canceledBedSync=(plan.HouseId,plan.PhysicalPatch>0?plan.PhysicalPatch:plan.Batch);}
    }
    var syncGutter=0f;
    if(NeedsBedSync(plan)){
     var origin=ImGui.GetCursorScreenPos();var size=Math.Max(36,ImGui.GetFrameHeight());syncGutter=size+ImGui.GetStyle().ItemSpacing.X;
     var boardWidth=Math.Max(1,Math.Min(432,ImGui.GetContentRegionAvail().X-syncGutter));
     ImGui.SetCursorScreenPos(origin+new Vector2(boardWidth+ImGui.GetStyle().ItemSpacing.X,0));
     DrawBedSyncButton(plan,size);ImGui.SetCursorScreenPos(origin);
    }
    DrawGardenBoard(plan,showPlan,complete,BedSyncMatches(plan),syncGutter);
    var canPlan=Player.IsLoaded&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]&&!Conditions[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]&&SharedGardenLocation.Match(currentAddress,[plan])==plan.HouseId;
    DrawSavedGardenPlans(plan,canPlan);
    ImGui.BeginDisabled(!canPlan);DrawGardenPlanActions(plan);ImGui.EndDisabled();
    if(!canPlan)ImGui.TextDisabled("Visit this house to apply or change its saved plan.");
    if(selectedGardenBed is {} selected&&selected.House==plan.HouseId&&selected.Batch==plan.Batch&&plan.Beds.FirstOrDefault(b=>b.Bed==selected.Bed) is {} bed){
     DrawGardenPlanTooltip(bed,false);DrawGardenCareTooltip(bed,showPlan,false);
    }
   }ImGui.PopID();
  }
  ImGui.Checkbox("Show observed garden",ref adventureShowActual);
  if(ImGui.IsItemHovered())ImGui.SetTooltip("On: show crops observed in game. Off: show planting guidance when an unfinished plan is available.");
 }
}
