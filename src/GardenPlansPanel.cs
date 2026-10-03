using Dalamud.Bindings.ImGui;
using System.Numerics;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private PlantingGuideWindow plantingWindow = null!;
    private sealed class PlantingGuideWindow : Window
    {
        private readonly Plugin plugin;
        public PlantingGuideWindow(Plugin plugin) : base("Planting guide###EquinoxPlanting",ImGuiWindowFlags.NoCollapse)
        {
            this.plugin=plugin;Size=new Vector2(560,680);SizeCondition=ImGuiCond.FirstUseEver;
            SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(360,360),MaximumSize=new Vector2(float.MaxValue)};
            AllowPinning=true;RespectCloseHotkey=true;AllowBackgroundBlur=true;
        }
        public override void Draw() { if (ImGui.SmallButton("Minimize to icon")) plugin.MinimizeLauncher("Planting"); plugin.DrawGardenPlans(); }
    }
    private string? plantingHouseId;
    private int plantingBatch=1;
    private string? previousPlantingHouse;
    private void OnPlantingCommand(string command,string args)
    {
        nextRosterRead=default;
        plantingHouseId=config.SyncEnabled&&config.PairingKey.Length==64?SharedGardenLocation.Match(currentAddress,GardenPlanSources()):null;
        if(plantingHouseId is null){if(config.NotifyPlantingUnavailable)Chat.Print("[Equinox] /planting is available only at an identified paired house. Open its placard and allow sync, then try again.");return;}
        plantingWindow.IsOpen=true;
    }
    private SharedGardenPlan[] GardenPlanSources()
    {
        var saved=config.SharedRoster?.GardenPlans??[];var result=saved.ToList();
        var houses=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).SelectMany(c=>c.Houses).DistinctBy(h=>h.Id);
        foreach(var house in houses)for(var batch=1;batch<=(house.Size=="Large"?3:house.Size=="Medium"?2:1);batch++)
        {
            var care=config.SharedRoster?.GardenCare?.FirstOrDefault(c=>c.HouseId==house.Id&&c.Batch==batch);
            var existing=result.FindIndex(p=>p.HouseId==house.Id&&p.Batch==batch);
            var plan=existing>=0?result[existing]:new SharedGardenPlan(house.Id,house.Name,house.World,house.District,house.Ward,house.Plot,batch,"",DateTimeOffset.MinValue,[],house.GameHouseId,house.Size=="Large"?3:house.Size=="Medium"?2:1,PhysicalPatch:care?.PhysicalPatch??batch);
            var beds=Enumerable.Range(1,8).Select(number=>{
                var bed=plan.Beds.FirstOrDefault(b=>b.Bed==number);if(bed is not null)return bed;
                var b=care?.Beds.FirstOrDefault(b=>b.Bed==number);
                return new SharedGardenBed(number,"","","actual",b?.Crop is {Length:>0}?b.Crop:"Not synced yet",b?.Soil??"",b?.Planted,b?.Watered,0,b?.Ready??false,b?.NextTend,b?.HarvestAt,ObservedAt:b?.ObservedAt,TendedBy:b?.TendedBy??"",WiltHours:b?.WiltHours);
            }).ToArray();
            plan=plan with {Beds=beds};if(existing>=0)result[existing]=plan;else result.Add(plan);
        }
        return result.ToArray();
    }
    private void DrawGardenPlans()
    {
        DrawGardenArtworkPreview();
        var plans=GardenPlanSources();plantingHouseId=config.SyncEnabled&&config.PairingKey.Length==64?SharedGardenLocation.Match(currentAddress,plans):null;
        if(plantingHouseId is null){ImGui.TextWrapped("Visit an identified paired house to view its planting guide. No guide is shown outside or while loading.");return;}
        if(previousPlantingHouse!=plantingHouseId){plantingBatch=1;previousPlantingHouse=plantingHouseId;}
        var house=plans.Where(p=>p.HouseId==plantingHouseId).ToArray();
        var capacity=house.Max(p=>Math.Max(p.Batch,p.Capacity));
        for(var i=1;i<=capacity;i++){if(i>1)ImGui.SameLine();if(ImGui.Selectable($"Batch {i}",plantingBatch==i,ImGuiSelectableFlags.None,new Vector2(85,24)))plantingBatch=i;}
        var source=house.FirstOrDefault(p=>p.Batch==plantingBatch);if(source is null)return;
        var plan=EffectiveGardenPlan(source);var planned=plan.Beds.Where(b=>b.Crop.Length>0).ToArray();
        var complete=plan.CompletedAt is not null||planned.Length>0&&planned.All(b=>b.Status=="confirmed");var showPlan=planned.Length>0&&!complete;
        ImGui.TextWrapped($"{plan.HouseName} · {plan.World} · {plan.District} W{plan.Ward} P{plan.Plot}");
        if(ImGui.SmallButton("Refresh / retry garden sync")){nextRosterRead=default;if(syncFailures==0)nextSync=default;}
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped("Live plan check every 2s while open · Tend Suggested Every 12h");
        ImGui.PopStyleColor();
        ImGui.TextWrapped(rosterStatus+" "+syncStatus);
        if(gardenMarkerStatus.Length>0){if(gardenMarkerError)ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(1,.3f,.3f,1));ImGui.TextWrapped(gardenMarkerStatus);if(gardenMarkerError)ImGui.PopStyleColor();}
        var next=planned.Where(b=>b.Status!="confirmed").OrderBy(b=>b.Status=="replant"?b.ReplantOrder:b.Status=="starter"?int.MaxValue:b.Order).FirstOrDefault();
        if(showPlan&&next is not null)ImGui.TextWrapped(next.Status=="replant"?$"Next: Step {next.ReplantOrder} · Bed {next.Bed}. Remove only the temporary starter; replant {next.Crop} with {next.Soil}.":next.Status=="starter"?"Starter planted. Finish the other required beds before replanting it.":$"Next: Step {next.Order} · Bed {next.Bed}: {next.Crop} · {(next.ReplantOrder>0?next.StarterSoil:next.Soil)}");
        else ImGui.TextWrapped(complete?"Planting complete · showing the actual synced garden":"Choose Start garden on the website to save a planting plan.");
        var totalSteps=planned.Sum(b=>b.ReplantOrder>0?2:1);var doneSteps=planned.Sum(b=>b.Status=="confirmed"?(b.ReplantOrder>0?2:1):b.Status is "starter" or "replant"?1:0);
        if(planned.Length>0)ImGui.TextWrapped($"{(complete?totalSteps:doneSteps)}/{totalSteps} planting steps complete · Goal: {plan.Target}");
        var board=Math.Min(432,ImGui.GetContentRegionAvail().X);var origin=ImGui.GetCursorScreenPos();var tile=board*128/432;var margin=board*16/432;var stride=board*136/432;
        GardenImage("assets/backgrounds/batch-base.png",origin,new(board));
        int[] layout=[1,2,3,8,0,4,7,6,5];
        for(var i=0;i<9;i++)
        {
            var at=origin+new Vector2(margin+i%3*stride,margin+i/3*stride);var n=layout[i];
            if(n==0)
            {
                GardenImage("assets/centers/stone-emblem.png",at,new(tile));
                var centerLabel=complete?"Complete":showPlan?"Plan":"Garden";
                var labelSize=ImGui.CalcTextSize(centerLabel);
                var labelAt=at+new Vector2((tile-labelSize.X)/2,tile-labelSize.Y-8);
                var centerDraw=ImGui.GetWindowDrawList();
                centerDraw.PushClipRect(at,at+new Vector2(tile),true);
                centerDraw.AddRectFilled(labelAt-new Vector2(4,2),labelAt+labelSize+new Vector2(4,2),0xdd201710,3);
                centerDraw.AddText(labelAt,0xffffffff,centerLabel);
                centerDraw.PopClipRect();
                ImGui.SetCursorScreenPos(at);ImGui.InvisibleButton("garden-center",new Vector2(tile));
                if(ImGui.IsItemHovered())ImGui.SetTooltip($"Batch {plan.Batch} · {(complete?"Planting complete":showPlan?"Automatic planting guide":"Actual garden")}");
                continue;
            }
            var b=plan.Beds.First(x=>x.Bed==n);DrawGardenTile(b,showPlan,at,tile);
            DrawGardenIdentity(b,showPlan,at,tile);
            var draw=ImGui.GetWindowDrawList();
            if(showPlan&&next?.Bed==n)GardenImage("assets/borders/next.png",at,new Vector2(tile));
            var label=showPlan&&b.Crop.Length>0?b.Crop:b.ActualCrop;
            while(label.Length>1&&ImGui.CalcTextSize(label).X>tile-10)label=label[..^2]+"…";
            var bedLabelSize=ImGui.CalcTextSize(label);var bedLabelAt=at+new Vector2(Math.Max(4,(tile-bedLabelSize.X)/2),tile-bedLabelSize.Y-4);
            draw.PushClipRect(at,at+new Vector2(tile),true);
            draw.AddRectFilled(new Vector2(at.X+3,bedLabelAt.Y-1),at+new Vector2(tile-3,tile-2),0xdd201710);
            draw.AddText(bedLabelAt,0xffffffff,label);draw.PopClipRect();
            // Bed details are attached only to the small plan icon, never the whole bed.
            var infoSize=tile/4;
            var infoAt=at+new Vector2(91,7)*tile/128;
            ImGui.SetCursorScreenPos(infoAt);
            ImGui.InvisibleButton("Bed information##garden-info-"+n,new Vector2(infoSize));
            var infoHover=ImGui.IsItemHovered();
            if(infoHover)draw.AddRect(infoAt,infoAt+new Vector2(infoSize),0xffe6a4c1,3);
            if(infoHover||ImGui.IsItemFocused()){ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*28);ImGui.TextWrapped($"Bed {n}");DrawGardenItem(b.ActualCrop,false,"Actual: ");DrawGardenItem(b.ActualSoil,true,"Actual soil: ");if(b.Crop.Length>0)DrawGardenInfoLabel("plan",$"Plan: {b.Crop} · {b.Soil} · {b.Status}");if(b.ReplantOrder>0)ImGui.TextWrapped($"Step {b.Order}: {b.StarterSoil}. Step {b.ReplantOrder}: remove only starter, then replant with {b.Soil}.");if(b.Planted is {} planted)ImGui.TextWrapped($"Planted: {planted.ToLocalTime():g}");if(b.Watered is {} watered)DrawGardenInfoLabel("clock",$"Latest care: {watered.ToLocalTime():g}");DrawGardenInfoLabel("actor","Tended by: "+(b.TendedBy.Length>0?b.TendedBy:"Gardener not recorded"));if(b.Watered is {} dw&&b.WiltHours is {} wh&&GardenTiming.DeathRisk(b.Ready,dw.AddHours(wh+24),b.HarvestAt,DateTimeOffset.UtcNow))ImGui.TextWrapped("Dead (estimated) — check in game before removing.");if(GardenVisualState(b,false,DateTimeOffset.UtcNow)=="wilt-estimated")ImGui.TextWrapped("Wilting (estimated) — check in game.");if(b.Ready)ImGui.TextUnformatted("Confirmed ready to harvest");else{if(b.NextTend is {} tend)ImGui.TextWrapped($"Tending suggested: {tend.ToLocalTime():g}");if(b.HarvestAt is {} harvest)ImGui.TextWrapped($"Maturity estimate: {harvest.ToLocalTime():g}");}if(!b.Ready&&b.Watered is {} lastCare&&b.WiltHours is >0)ImGui.TextWrapped($"Death estimate: {lastCare.AddHours(b.WiltHours.Value+24).ToLocalTime():g} · check in game");ImGui.TextWrapped("Care state: "+GardenVisualState(b,false,DateTimeOffset.UtcNow).Replace('-',' '));ImGui.TextWrapped("Green confirms planting, not guaranteed crossbred seeds. Unsynced confirmed actions appear locally first.");ImGui.PopTextWrapPos();ImGui.EndTooltip();}
        }
        ImGui.SetCursorScreenPos(origin+new Vector2(0,board));ImGui.Dummy(new Vector2(board,4));
        if(ImGui.CollapsingHeader("Tips, supplies and harvest potential")){
            ImGui.TextWrapped("Follow step numbers, not just bed numbers. Keep compatible mature neighbours. The first planting in an empty batch has no neighbour; the guide may return to that starter after the others are planted. Crossbred seeds are possible extras, not guaranteed harvests.");
            var y=config.SharedRoster?.GardenYields?.FirstOrDefault(y=>y.HouseId==plan.HouseId&&y.Batch==plan.Batch);if(y is not null){ImGui.TextWrapped("Recorded: "+y.Actual);ImGui.TextWrapped("Planned: "+y.Planned);ImGui.TextWrapped(y.Seeds);}
            var supplies=planned.SelectMany(b=>b.ReplantOrder>0?new[]{b.Crop+" seed",b.Crop+" seed",b.StarterSoil,b.Soil}:new[]{b.Crop+" seed",b.Soil}).GroupBy(x=>x);ImGui.TextUnformatted("Full plan supplies:");foreach(var supply in supplies)DrawGardenItem(supply.Key,true,$"{supply.Count()} × ");
        }
        if(ImGui.CollapsingHeader("Waiting for a garden or house to connect?")){ImGui.TextWrapped("Houses match by world, district, ward and plot. Numbered game batches and beds connect automatically. Open the estate placard if the house is missing. Position-only observations wait until the game exposes a matching numbered bed, then retry automatically. The selected tab never overrides a different physical batch. Keep the website open for new links and full garden reconciliation.");

        }
    }
}
