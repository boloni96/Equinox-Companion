using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private bool adventureShowActual;
 private (string House,int Batch,int Bed)? adventureOpenBed;
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
    if(NeedsBedSync(plan)){
     DrawBedSyncButton(plan,Math.Max(36,ImGui.GetFrameHeight()));ImGui.SameLine();ImGui.TextUnformatted("Sync beds");
    }
    if(BedSyncMatches(plan)&&gardenBedSync is {Complete:false} sync){
     ImGui.TextWrapped(sync.Status);
     if(ImGui.SmallButton("Cancel bed sync")){gardenBedSync=null;canceledBedSync=(plan.HouseId,plan.PhysicalPatch>0?plan.PhysicalPatch:plan.Batch);}
    }
    DrawGardenBoard(plan,showPlan,complete,BedSyncMatches(plan));
    if(selectedGardenBed is {} selected&&selected.House==plan.HouseId&&selected.Batch==plan.Batch&&plan.Beds.FirstOrDefault(b=>b.Bed==selected.Bed) is {} bed){
     DrawGardenPlanTooltip(bed,false);DrawGardenCareTooltip(bed,showPlan,false);
    }
   }ImGui.PopID();
  }
  ImGui.Checkbox("Show observed garden",ref adventureShowActual);
  if(ImGui.IsItemHovered())ImGui.SetTooltip("On: show crops observed in game. Off: show planting guidance when an unfinished plan is available.");
 }
}
