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
        public PlantingGuideWindow(Plugin plugin) : base("Planting guide###EquinoxPlanting",ImGuiWindowFlags.None)
        {
            this.plugin=plugin;Size=new Vector2(560,680);SizeCondition=ImGuiCond.FirstUseEver;
            SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(360,360),MaximumSize=new Vector2(float.MaxValue)};
            AllowPinning=true;RespectCloseHotkey=true;AllowBackgroundBlur=true;
        }
        public override void PostDraw() => HandleNativeCollapse(this, () => plugin.MinimizeLauncher("Planting"));
        public override void Draw() { plugin.DrawGardenPlans(); }
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
        DrawGardenInfoLabel("paired",rosterStatus+" "+syncStatus);
        if(gardenMarkerStatus.Length>0){if(gardenMarkerError)ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(1,.3f,.3f,1));ImGui.TextWrapped(gardenMarkerStatus);if(gardenMarkerError)ImGui.PopStyleColor();}
        var next=GardenGuidance.Next(plan);
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
            var wrong= gardenWrongBed is {} mismatch&&mismatch.House==plan.HouseId&&mismatch.Batch==plan.Batch&&mismatch.WrongBed==n;
            var b=plan.Beds.First(x=>x.Bed==n);DrawGardenTile(b,showPlan,at,tile,wrong);
            DrawGardenIdentity(b,showPlan,at,tile);
            var draw=ImGui.GetWindowDrawList();
            if(showPlan&&next?.Bed==n)GardenImage("assets/borders/next.png",at,new Vector2(tile));
            if(wrong)GardenImage("assets/borders/different.png",at,new Vector2(tile));
            var label=showPlan&&b.Crop.Length>0?b.Crop:b.ActualCrop;
            var captionInset=Math.Max(7,tile*8/128);
            while(label.Length>1&&ImGui.CalcTextSize(label).X>tile-2*captionInset)label=label[..^2]+"…";
            var bedLabelSize=ImGui.CalcTextSize(label);var bedLabelAt=at+new Vector2(Math.Max(4,(tile-bedLabelSize.X)/2),tile-bedLabelSize.Y-captionInset);
            draw.PushClipRect(at,at+new Vector2(tile),true);
            draw.AddRectFilled(new Vector2(at.X+captionInset,bedLabelAt.Y-1),at+new Vector2(tile-captionInset,tile-captionInset+1),0xdd201710);
            draw.AddText(bedLabelAt,0xffffffff,label);draw.PopClipRect();
            DrawGardenHoverTarget($"Planting plan##garden-plan-{n}", at+new Vector2(91,7)*tile/128, tile/4,
                () => DrawGardenPlanTooltip(b, wrong));
            DrawGardenHoverTarget($"Crop and care##garden-care-{n}", at+new Vector2(88,52)*tile/128, tile/4,
                () => DrawGardenCareTooltip(b,showPlan,wrong));
        }
        ImGui.SetCursorScreenPos(origin+new Vector2(0,board));ImGui.Dummy(new Vector2(board,4));
        if(ImGui.CollapsingHeader("Tips, supplies and harvest potential")){
            DrawGardenInfoLabel("tips","Follow step numbers, not just bed numbers. Keep compatible mature neighbours. The first planting in an empty batch has no neighbour; the guide may return to that starter after the others are planted. Crossbred seeds are possible extras, not guaranteed harvests.");
            var y=config.SharedRoster?.GardenYields?.FirstOrDefault(y=>y.HouseId==plan.HouseId&&y.Batch==plan.Batch);if(y is not null){ImGui.TextWrapped("Recorded: "+y.Actual);ImGui.TextWrapped("Planned: "+y.Planned);ImGui.TextWrapped(y.Seeds);}
            var supplies=planned.SelectMany(b=>b.ReplantOrder>0?new[]{b.Crop+" seed",b.Crop+" seed",b.StarterSoil,b.Soil}:new[]{b.Crop+" seed",b.Soil}).GroupBy(x=>x);ImGui.TextUnformatted("Full plan supplies:");foreach(var supply in supplies)DrawGardenItem(supply.Key,true,$"{supply.Count()} × ");
        }
        if(ImGui.CollapsingHeader("Waiting for a garden or house to connect?")){ImGui.TextWrapped("Houses match by world, district, ward and plot. Numbered game batches and beds connect automatically. Open the estate placard if the house is missing. Position-only observations wait until the game exposes a matching numbered bed, then retry automatically. The selected tab never overrides a different physical batch. Keep the website open for new links and full garden reconciliation.");

        }
    }
    private static void DrawGardenHoverTarget(string id, Vector2 at, float size, Action contents)
    {
        ImGui.SetCursorScreenPos(at);
        ImGui.InvisibleButton(id, new Vector2(size));
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.GetWindowDrawList().AddRect(at, at+new Vector2(size), 0xffe6a4c1, 3);
        if (!hovered && !ImGui.IsItemFocused()) return;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize()*28);
        contents();
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
    private void DrawGardenPlanTooltip(SharedGardenBed b, bool wrong)
    {
        ImGui.TextUnformatted($"Bed {b.Bed} · Planting plan");
        if (wrong && gardenWrongBed is {} correction)
            DrawGardenInfoLabel("warning", $"Wrong bed opened. Close this menu and open Bed {correction.NextBed}, outlined as the next planting bed. No planting action was recorded.");
        if (b.Crop.Length == 0)
        {
            ImGui.TextWrapped("No planting plan for this bed. Choose Start garden on the website to create one.");
            return;
        }
        DrawGardenItem(b.Crop, true, "Planned crop: ");
        DrawGardenItem(b.Soil, true, "Final planned soil: ");
        DrawGardenInfoLabel("plan", "Plan status: " + b.Status.Replace('-', ' '));
        if (b.ReplantOrder > 0)
            ImGui.TextWrapped($"Step {b.Order}: plant the temporary starter with {b.StarterSoil}. Step {b.ReplantOrder}: after the other required beds, remove only that starter and replant with {b.Soil}.");
        else if (b.Order > 0) ImGui.TextWrapped($"Planting step: {b.Order}");
        if (b.Status == "different")
            DrawGardenInfoLabel("warning", $"Different from plan: {b.ActualCrop} · {(b.ActualSoil.Length > 0 ? b.ActualSoil : "soil not recorded")}");
        if (b.CheckExisting) ImGui.TextWrapped("Check the existing crop in game before replacing it.");
    }
    private void DrawGardenCareTooltip(SharedGardenBed b, bool showPlan, bool wrong)
    {
        ImGui.TextUnformatted($"Bed {b.Bed} · Crop and care");
        var icon = GardenCareIcon(b,showPlan,wrong);
        DrawGardenInfoLabel(icon,GardenCareHint(icon));
        DrawGardenItem(b.ActualCrop, false, "Actual crop: ");
        if (b.ActualCrop == "Empty" && !b.Ready)
        {
            if (b.LastClearedAt is {} cleared) DrawGardenInfoLabel("clock", $"Cleared: {cleared.ToLocalTime():g}");
            if (b.ObservedAt is {} emptySeen) DrawGardenInfoLabel("sync", $"Last observed: {emptySeen.ToLocalTime():g}");
            return;
        }
        if (b.ActualCrop is "" or "Not synced yet" or "Crop not identified")
            DrawGardenInfoLabel("unknown", "Crop unidentified. Open this numbered bed in game to inspect it. If its name remains unknown, confirm it manually on the website; tending alone may not identify it.");
        DrawGardenItem(b.ActualSoil.Length > 0 ? b.ActualSoil : "Not recorded", true, "Actual soil: ");
        var state = GardenVisualState(b, false, DateTimeOffset.UtcNow);
        if (icon == "warning") DrawGardenInfoLabel("check-status", "Care state: " + state.Replace('-', ' '));
        if (b.Planted is {} planted) DrawGardenInfoLabel("seed", $"Planted: {planted.ToLocalTime():g}");
        if (b.Watered is {} watered) DrawGardenInfoLabel("clock", $"Latest care: {watered.ToLocalTime():g}");
        DrawGardenInfoLabel("actor", "Tended by: " + (b.TendedBy.Length > 0 ? b.TendedBy : "Gardener not recorded"));
        if (b.LastFertilized is {} fed) DrawGardenInfoLabel("fertilized", $"Last fertilized: {fed.ToLocalTime():g}");
        if (!b.Ready)
        {
            if (b.NextTend is {} tend) DrawGardenInfoLabel("tend", $"Tending suggested: {tend.ToLocalTime():g}");
            if (b.HarvestAt is {} harvest) DrawGardenInfoLabel("check-maturity", $"Maturity estimate: {harvest.ToLocalTime():g}");
            if (b.Watered is {} lastCare && b.WiltHours is >0) ImGui.TextWrapped($"Death estimate: {lastCare.AddHours(b.WiltHours.Value+24).ToLocalTime():g} · check in game");
        }
        if (b.ObservedAt is {} seen) DrawGardenInfoLabel("sync", $"Last observed: {seen.ToLocalTime():g}");
    }

}
