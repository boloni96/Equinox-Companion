using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private bool adventureShowActual;
 private void DrawAdventureHeading(string icon,string title)
 {
  var at=ImGui.GetCursorScreenPos();var size=ImGui.GetTextLineHeight()*2;
  GardenImage("assets/category-icons/"+icon+".png",at,new Vector2(size));
  ImGui.Dummy(new Vector2(size));ImGui.SameLine();ImGui.TextWrapped(title);
 }
 private void DrawAdventureGardens(string houseId)
 {
  var sources=GardenPlanSources().Where(p=>p.HouseId==houseId).OrderBy(p=>p.Batch).ToArray();
  if(sources.Length==0){ImGui.TextWrapped("Waiting for this house’s garden records. Refresh after the Journal has synced.");return;}
  ImGui.Checkbox("Show actual garden instead of planting steps",ref adventureShowActual);
  foreach(var source in sources){
   ImGui.PushID("adventure-garden-"+source.HouseId+"-"+source.Batch);
   var plan=WithQueuedGardenBeds(EffectiveGardenPlan(source));
   var planned=plan.Beds.Where(b=>b.Crop.Length>0).ToArray();
   var complete=plan.CompletedAt is not null||planned.Length>0&&planned.All(b=>b.Status=="confirmed");
   ImGui.SetNextItemOpen(sources.Length==1,ImGuiCond.Once);
   if(ImGui.CollapsingHeader($"{(complete?"✓ ":"")}Batch {plan.Batch}")){
    var showPlan=!adventureShowActual&&planned.Length>0&&!complete;
    ImGui.TextWrapped(GardenOverview.Summary([plan],DateTimeOffset.UtcNow));
    if(showPlan&&GardenGuidance.Next(plan) is {} next)ImGui.TextWrapped($"Next: Bed {next.Bed} · {next.Crop} · {GardenPlantRequirement.Soil(next)}");
    DrawGardenBoard(plan,showPlan,complete,false);
    if(selectedGardenBed is {} selected&&selected.House==plan.HouseId&&selected.Batch==plan.Batch&&plan.Beds.FirstOrDefault(b=>b.Bed==selected.Bed) is {} bed){
     DrawGardenPlanTooltip(bed,false);DrawGardenCareTooltip(bed,showPlan,false);
    }
   }ImGui.PopID();
  }
 }
}
