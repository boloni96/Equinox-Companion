using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private bool adventureShowActual;
 private (string House,int Batch,int Bed)? adventureOpenBed;
 private readonly AdventureGardenNavigation adventureGardenNavigation=new();
 private void FollowAdventureGarden(AdventureBoard board)
 {
  adventureGardenNavigation.Set(board);
  if(!adventureGardenNavigation.HasGardens)return;
  var selected=adventureGardenNavigation.Follow(GardenGuideSelection(),currentAddress,atOutdoorEstate,
   gardenBedSync is {Complete:false},GardenMappings(),GardenPlanSources(),DateTimeOffset.UtcNow);
  if(selected is {} target)adventureOpenBed=target;
 }
 private void DrawAdventureHeading(string icon,string title)
 {
  var at=ImGui.GetCursorScreenPos();var size=ImGui.GetTextLineHeight()*2;
  GardenImage("assets/category-icons/"+icon+".png",at,new Vector2(size));
  ImGui.Dummy(new Vector2(size));ImGui.SameLine();ImGui.TextWrapped(title);
 }
 private string adventureFocusHouse="";
 private void DrawAdventureMultiGardens(AdventureTask[] tasks)
 {
  foreach(var id in tasks.Where(t=>AdventurePolicy.IsGardenTask(t)).Select(t=>t.Rule!.HouseId).Where(id=>id.Length>0).Distinct()){
   if(!adventureLayoutHouses.TryGetValue(id,out var house))continue;
   ImGui.PushID("multi-garden-"+id);
   var requested=adventureFocusHouse==id||adventureOpenBed is {} target&&target.House==id;
   if(requested)ImGui.SetNextItemOpen(true,ImGuiCond.Always);else ImGui.SetNextItemOpen(false,ImGuiCond.Once);
   if(ImGui.CollapsingHeader($"Gardening · {house.Name} · {house.District} W{house.Ward} P{house.Plot}")){
    if(adventureFocusHouse==id){adventureFocusHouse="";ImGui.SetScrollHereY(.1f);}
    if(ImGui.SmallButton("Go"))VisitSharedHouse(house);
    if(ImGui.IsItemHovered())ImGui.SetTooltip("Travel to this house using the available estate, friend or ward route.");
    DrawAdventureGardens(id);
   }
   ImGui.PopID();
  }
 }
 private void DrawAdventureGardens(string houseId)
 {
  var sources=GardenPlanSources().Where(p=>p.HouseId==houseId).OrderBy(p=>p.Batch).ToArray();
  if(sources.Length==0){ImGui.TextWrapped("Waiting for this house’s garden records. Confirmed garden observations will appear after Companion syncs.");return;}
  foreach(var source in sources){
   ImGui.PushID("adventure-garden-"+source.HouseId+"-"+source.Batch);
   var plan=EffectiveGardenPlan(source);
   var planned=plan.Beds.Where(b=>b.Crop.Length>0).ToArray();
   var complete=plan.CompletedAt is not null||planned.Length>0&&planned.All(b=>b.Status=="confirmed");
   var requested=adventureOpenBed is {} target&&target.House==plan.HouseId&&target.Batch==plan.Batch;
   ImGui.TextUnformatted($"{(complete?"✓ ":"")}Batch {plan.Batch}");
   ImGui.SameLine();DrawGardenGuideHelp();
   {
    if(requested&&adventureOpenBed is {} focus){if(focus.Bed>0)selectedGardenBed=focus;ImGui.SetScrollHereY(.15f);adventureOpenBed=null;}
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
   }ImGui.PopID();
  }
  ImGui.Checkbox("Show observed garden",ref adventureShowActual);
  if(ImGui.IsItemHovered())ImGui.SetTooltip("On: show crops observed in game. Off: show planting guidance when an unfinished plan is available.");
 }
}

