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
    private (string House,int Batch,int Bed)? selectedGardenBed;
    private GardenBedSyncSession? gardenBedSync;
    private bool atOutdoorEstate;
    private bool suppressSyncRelease;
    private (string House,int Patch)? canceledBedSync;
    private void ObserveBedSync(GardenSnapshot target, DateTimeOffset at, (int Patch,int Bed)? numbered = null)
    {
        if (!plantingWindow.IsOpen || !atOutdoorEstate || gardenBedSync?.Observe(target,at,numbered)!=true || !gardenBedSync.Complete) return;
        foreach (var e in gardenBedSync.Mappings)
            KeepDiscovery(e with {Actor=WithWorldNames(e.Actor),Address=WithAddressNames(e.Address!)},force:true);
        nextRosterRead=default;if(syncFailures==0)nextSync=default;
    }
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
                return new SharedGardenBed(number,"","","actual",b?.Empty==true?"Empty":b?.Crop is {Length:>0}?b.Crop:"Not synced yet",b?.Soil??"",b?.Planted,b?.Watered,0,b?.Ready??false,b?.NextTend,b?.HarvestAt,ObservedAt:b?.ObservedAt,TendedBy:b?.TendedBy??"",WiltHours:b?.WiltHours,DeadConfirmedAt:b?.DeadConfirmedAt,LastClearedAt:b?.LastClearedAt,LastFertilized:b?.LastFertilized,KeepMature:b?.KeepMature??false,WiltedAt:b?.WiltedAt,GrowingObservedAt:b?.GrowingObservedAt);
            }).ToArray();
            plan=plan with {Beds=beds};if(existing>=0)result[existing]=plan;else result.Add(plan);
        }
        return result.ToArray();
    }
    private void DrawGardenPlans()
    {
        var plans=GardenPlanSources();plantingHouseId=config.SyncEnabled&&config.PairingKey.Length==64?SharedGardenLocation.Match(currentAddress,plans):null;
        if(plantingHouseId is null){ImGui.TextWrapped("Visit an identified paired house to view its planting guide. No guide is shown outside or while loading.");return;}
        if(previousPlantingHouse!=plantingHouseId){plantingBatch=1;previousPlantingHouse=plantingHouseId;}
        var house=plans.Where(p=>p.HouseId==plantingHouseId).ToArray();
        var capacity=house.Max(p=>Math.Max(p.Batch,p.Capacity));
        var tabsAt=ImGui.GetCursorScreenPos();var tabsWidth=ImGui.GetContentRegionAvail().X;
        var syncSize=Math.Max(36,ImGui.GetFrameHeight());var houseSize=syncSize*1.25f;var rowHeight=houseSize;
        var tabWidth=Math.Min(85,Math.Max(40,(tabsWidth-houseSize-syncSize-5*ImGui.GetStyle().ItemSpacing.X)/capacity));
        for(var i=1;i<=capacity;i++){if(i>1)ImGui.SameLine();if(ImGui.Selectable($"Batch {i}",plantingBatch==i,ImGuiSelectableFlags.None,new Vector2(tabWidth,ImGui.GetFrameHeight())))plantingBatch=i;}
        var source=house.FirstOrDefault(p=>p.Batch==plantingBatch);if(source is null)return;
        var plan=EffectiveGardenPlan(source);
        var sent=config.SentEvents.ToHashSet();
        var queued=LocalGardenActions().Where(e=>!sent.Contains(e.Id)&&e.Address is not null&&SharedGardenLocation.Match(e.Address,[plan])==plan.HouseId&&e.Patch==(plan.PhysicalPatch>0?plan.PhysicalPatch:plan.Batch)).Select(e=>e.Bed).ToHashSet();
        plan=plan with {Beds=plan.Beds.Select(b=>b with {Queued=queued.Contains(b.Bed)}).ToArray()};
        var planned=plan.Beds.Where(b=>b.Crop.Length>0).ToArray();
        var physicalPatch=plan.PhysicalPatch>0?plan.PhysicalPatch:plan.Batch;
        var visitorSetup=atOutdoorEstate&&GardenBedSyncSession.IsVisitor(config.SharedRoster,Player.CharacterName,WorldName(Player.HomeWorld.RowId)??"",plan.HouseId);
        if(!visitorSetup||gardenBedSync is not null&&!gardenBedSync.Matches(Player.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture),currentAddress,physicalPatch))gardenBedSync=null;
        if(!visitorSetup||canceledBedSync is {} canceled&&(canceled.House!=plan.HouseId||canceled.Patch!=physicalPatch))canceledBedSync=null;
        var showSync=visitorSetup&&(gardenBedSync is {Complete:false}||canceledBedSync is not null||GardenBedSyncSession.NeedsSync(plan.Beds));
        var houseAt=new Vector2(tabsAt.X+Math.Max(0,tabsWidth-houseSize-(showSync?syncSize+ImGui.GetStyle().ItemSpacing.X:0)),tabsAt.Y);
        ImGui.SetCursorScreenPos(houseAt);
        var openHouse=ImGui.Button("##Open house gardening",new Vector2(houseSize));
        GardenImage("assets/icons/house-moogle.png",houseAt+new Vector2(2),new Vector2(houseSize-4));
        if(openHouse)Dalamud.Utility.Util.OpenLink(GardenCareStatus.WebsiteUrl(plan.HouseId,plan.Batch));
        if(ImGui.IsItemHovered()||ImGui.IsItemFocused()&&ImGui.GetIO().NavVisible)ImGui.SetTooltip($"Open this house’s gardening plans on the website\n{plan.HouseName} · {plan.World} · {plan.District} W{plan.Ward} P{plan.Plot} · Batch {plan.Batch}");
        if(showSync)
        {
        var syncAt=new Vector2(tabsAt.X+Math.Max(0,tabsWidth-syncSize),tabsAt.Y);
        ImGui.SetCursorScreenPos(syncAt);
        var syncPressed=ImGui.Button("##Sync garden beds",new Vector2(syncSize));
        var syncAction=GardenBedSyncSession.ButtonAction(syncPressed,ImGui.IsItemHovered()&&ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left),ImGui.IsMouseDown(ImGuiMouseButton.Left),ref suppressSyncRelease);
        if(syncAction=="cancel"){gardenBedSync=null;canceledBedSync=(plan.HouseId,physicalPatch);}
        else if(syncAction=="start"&&snapshot is {} start&&currentAddress is {} syncAddress){gardenBedSync=new(start.Actor,syncAddress,physicalPatch,DateTimeOffset.UtcNow);canceledBedSync=null;}
        if(gardenBedSync is {Complete:false} syncing)
        {
            var number=syncing.NextBed.ToString();var numberSize=ImGui.CalcTextSize(number);
            ImGui.GetWindowDrawList().AddText(syncAt+new Vector2((syncSize-numberSize.X)/2,2),0xffffffff,number);
            var iconSize=Math.Max(16,syncSize-numberSize.Y-5);
            GardenImage("assets/icons/sync.png",syncAt+new Vector2((syncSize-iconSize)/2,numberSize.Y+3),new Vector2(iconSize));
        }
        else GardenImage("assets/icons/sync.png",syncAt+new Vector2(4),new Vector2(syncSize-8));
        if(ImGui.IsItemHovered()||ImGui.IsItemFocused()&&ImGui.GetIO().NavVisible)
        {
            ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*25);
            ImGui.TextUnformatted(gardenBedSync is {Complete:false}?"Restart bed sync":"Sync beds");
            ImGui.TextWrapped($"Batch {plan.Batch}: close the current bed menu, click once, then inspect Beds 1–8 clockwise. Follow the number. Click again to restart; double-click to cancel (no number).");
            ImGui.TextWrapped("Keep the website open to save new links. More details below under ‘Waiting for a garden or house to connect?’.");
            ImGui.PopTextWrapPos();ImGui.EndTooltip();
        }
        }
        ImGui.SetCursorScreenPos(tabsAt+new Vector2(0,rowHeight+ImGui.GetStyle().ItemSpacing.Y));
        if(gardenBedSync is {Complete:false})
        {
            ImGui.TextWrapped(gardenBedSync.Status);
            if(ImGui.SmallButton("Cancel bed sync")){gardenBedSync=null;canceledBedSync=(plan.HouseId,physicalPatch);}
        }
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
        if(config.SharedRoster?.GardenCornerTrim==true)GardenImage("assets/backgrounds/batch-corner-trim.png",origin,new(board));
        int[] layout=[1,2,3,8,0,4,7,6,5];
        for(var i=0;i<9;i++)
        {
            var at=origin+new Vector2(margin+i%3*stride,margin+i/3*stride);var n=layout[i];
            if(n==0)
            {
                GardenImage("assets/centers/stone-emblem.png",at,new(tile));
                if(gardenBedSync is {Complete:false} centerSync)
                {
                    var number=centerSync.NextBed.ToString();var numberSize=ImGui.CalcTextSize(number);var iconSize=tile*.28f;
                    var iconAt=at+new Vector2((tile-iconSize)/2,tile*.42f);
                    var numberAt=at+new Vector2((tile-numberSize.X)/2,tile*.42f-numberSize.Y-3);
                    ImGui.GetWindowDrawList().AddRectFilled(numberAt-new Vector2(4,2),numberAt+numberSize+new Vector2(4,2),0xdd201710,3);
                    ImGui.GetWindowDrawList().AddText(numberAt,0xffffffff,number);
                    GardenImage("assets/icons/sync.png",iconAt,new Vector2(iconSize));
                }
                var centerLabel=complete?"Complete":showPlan?"Plan":"Garden";
                var labelSize=ImGui.CalcTextSize(centerLabel);
                var labelAt=at+new Vector2((tile-labelSize.X)/2,tile-labelSize.Y-8);
                var centerDraw=ImGui.GetWindowDrawList();
                centerDraw.PushClipRect(at,at+new Vector2(tile),true);
                centerDraw.AddRectFilled(labelAt-new Vector2(4,2),labelAt+labelSize+new Vector2(4,2),0xdd201710,3);
                centerDraw.AddText(labelAt,0xffffffff,centerLabel);
                centerDraw.PopClipRect();
                ImGui.SetCursorScreenPos(at);ImGui.InvisibleButton("garden-center",new Vector2(tile));
                if(ImGui.IsItemHovered())ImGui.SetTooltip(gardenBedSync is {Complete:false} centerHelp?$"Bed sync · Touch Bed {centerHelp.NextBed} in game. Use the top-right sync button to restart.":$"Batch {plan.Batch} · {(complete?"Planting complete":showPlan?"Automatic planting guide":"Actual garden")}");
                continue;
            }
            var wrong= gardenWrongBed is {} mismatch&&mismatch.House==plan.HouseId&&mismatch.Batch==plan.Batch&&mismatch.WrongBed==n;
            ImGui.SetCursorScreenPos(at);
            if(ImGui.InvisibleButton("Select garden bed##"+n,new Vector2(tile)))selectedGardenBed=(plan.HouseId,plan.Batch,n);
            ImGui.SetItemAllowOverlap();
            var selected=selectedGardenBed is {} pick&&pick.House==plan.HouseId&&pick.Batch==plan.Batch&&pick.Bed==n;
            var b=plan.Beds.First(x=>x.Bed==n);DrawGardenTile(b,showPlan,at,tile,wrong,selected);
            DrawGardenIdentity(b,showPlan,at,tile);
            var draw=ImGui.GetWindowDrawList();
            if(showPlan&&next?.Bed==n)GardenPlanOutline("next",at,tile);
            if(gardenBedSync is {Complete:false} setup&&setup.NextBed==n)GardenImage("assets/borders/selected.png",at,new Vector2(tile));
            if(wrong)GardenPlanOutline("different",at,tile);
            var label=EquinoxCompanion.GardenVisualState.DisplayCrop(b,showPlan);
            var captionInset=Math.Max(7,tile*8/128);
            while(label.Length>1&&ImGui.CalcTextSize(label).X>tile-2*captionInset)label=label[..^2]+"…";
            var bedLabelSize=ImGui.CalcTextSize(label);var bedLabelAt=at+new Vector2(Math.Max(4,(tile-bedLabelSize.X)/2),tile-bedLabelSize.Y-captionInset);
            draw.PushClipRect(at,at+new Vector2(tile),true);
            draw.AddRectFilled(new Vector2(at.X+captionInset,bedLabelAt.Y-1),at+new Vector2(tile-captionInset,tile-captionInset+1),0xdd201710);
            draw.AddText(bedLabelAt,0xffffffff,label);draw.PopClipRect();
            DrawGardenHoverTarget($"Planting plan##garden-plan-{n}", at+new Vector2(91,7)*tile/128, tile/4,
                () => DrawGardenPlanTooltip(b, wrong));
            DrawGardenHoverTarget($"Crop and care##garden-care-{n}", GardenCarePosition(at,tile), tile/4,
                () => DrawGardenCareTooltip(b,showPlan,wrong));
        }
        ImGui.SetCursorScreenPos(origin+new Vector2(0,board));ImGui.Dummy(new Vector2(board,4));
        if(ImGui.CollapsingHeader("Tips, supplies and harvest potential")){
            DrawGardenInfoLabel("tips","Follow step numbers, not just bed numbers. Keep compatible mature neighbours. The first planting in an empty batch has no neighbour; the guide may return to that starter after the others are planted. Crossbred seeds are possible extras, not guaranteed harvests.");
            var y=config.SharedRoster?.GardenYields?.FirstOrDefault(y=>y.HouseId==plan.HouseId&&y.Batch==plan.Batch);if(y is not null){ImGui.TextWrapped("Recorded: "+y.Actual);ImGui.TextWrapped("Planned: "+y.Planned);ImGui.TextWrapped(y.Seeds);}
            var supplies=planned.SelectMany(b=>b.ReplantOrder>0?new[]{b.Crop+" seed",b.Crop+" seed",b.StarterSoil,b.Soil}:new[]{b.Crop+" seed",b.Soil}).GroupBy(x=>x);ImGui.TextUnformatted("Full plan supplies:");foreach(var supply in supplies)DrawGardenItem(supply.Key,true,$"{supply.Count()} × ");
        }
        if(ImGui.CollapsingHeader("Waiting for a garden or house to connect?")){ImGui.TextWrapped($"During setup, each observed bed previews immediately. Repeated clicks cannot skip a step. A target more than {GardenBedSyncSession.MaxDistance} game units from the first bed resets to Bed 1; nearby wrong clicks may need a restart. All eight identities are saved together. Double-click Sync or use Cancel to discard unfinished setup and clear its number; single-click starts again at Bed 1. The house/moogle button opens this exact house and batch on the website.");ImGui.TextWrapped("Houses match by world, district, ward and plot. Numbered game batches and beds connect automatically. For a visitor without numbered menus, use Sync beds and inspect Beds 1–8 in the displayed order. Restart if you clicked the wrong bed; a repeated target cannot advance setup. Only observed empty/crop information is synced, and unidentified crops stay unknown. Open the estate placard if the house is missing. Keep the website open for new links and full garden reconciliation.");
            if(visitorSetup&&ImGui.SmallButton("Recalibrate beds 1–8")&&snapshot is {} restart&&currentAddress is {} restartAddress)
                gardenBedSync=new(restart.Actor,restartAddress,physicalPatch,DateTimeOffset.UtcNow);
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
        ImGui.TextUnformatted($"Bed {b.Bed} · {(b.Crop.Length==0?"Bed overview":b.Status=="confirmed"?"Planting record":"Planting plan")}");
        if(b.Queued)DrawGardenInfoLabel("queued","Recorded locally · waiting for server acknowledgement. Acknowledgement does not confirm that another client has refreshed.");
        if (wrong && gardenWrongBed is {} correction)
            DrawGardenInfoLabel("warning", $"Wrong bed opened. Close this menu and open Bed {correction.NextBed}, outlined as the next planting bed. No planting action was recorded.");
        if (b.Crop.Length == 0)
        {
            DrawGardenItem(b.ActualCrop,false,"Crop: ");
            DrawGardenItem(b.ActualSoil.Length>0?b.ActualSoil:"Not recorded",true,"Soil: ");
            ImGui.TextWrapped("No planting plan for this bed. Use the lower icon for care details.");
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
        if(b.Queued)DrawGardenInfoLabel("queued","Recorded locally · waiting for server acknowledgement. Another client may still need to refresh afterwards.");
        if(EquinoxCompanion.GardenVisualState.SeedlingEstimate(b,DateTimeOffset.UtcNow))DrawGardenInfoLabel("seedling","Seedling illustration · first 33% of the recorded growth estimate; the game stage has not been observed.");
        var icon = GardenCareIcon(b,showPlan,wrong);
        DrawGardenInfoLabel(icon,EquinoxCompanion.GardenVisualState.For(b,false,DateTimeOffset.UtcNow)=="wilted"?"Wilting · reported by the game. Tend urgently if alive.":GardenCareHint(icon));
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
        if(!b.Ready&&b.DeadConfirmedAt is null&&EquinoxCompanion.GardenVisualState.GrowthPercent(b,DateTimeOffset.UtcNow) is {} percent)DrawGardenInfoLabel("clock",$"Estimated growth: {percent:0}% · {(percent>=100?"estimated ready · check in game":percent>=66?"late growth":percent>=33?"middle growth":"early growth")}. This is timing, not a confirmed game stage.");
        if (b.Planted is {} planted) DrawGardenInfoLabel("seed", $"Planted: {planted.ToLocalTime():g}");
        if (b.Watered is {} watered) DrawGardenInfoLabel("clock", $"Latest care: {watered.ToLocalTime():g}");
        DrawGardenInfoLabel("actor", "Tended by: " + (b.TendedBy.Length > 0 ? b.TendedBy : "Gardener not recorded"));
        if (b.LastFertilized is {} fed) {DrawGardenItem("Fishmeal",true,"Fertilizer: ");DrawGardenInfoLabel("fertilized", $"Last fertilized: {fed.ToLocalTime():g} · does not reset tending.");}
        if (!b.Ready && b.DeadConfirmedAt is null)
        {
            if (b.NextTend is {} tend) DrawGardenInfoLabel("tend", $"Tending suggested: {tend.ToLocalTime():g}");
            if (b.HarvestAt is {} harvest) DrawGardenInfoLabel("check-maturity", $"Maturity estimate: {harvest.ToLocalTime():g}");
            if (b.Watered is {} lastCare && b.WiltHours is >0) ImGui.TextWrapped($"Death estimate: {lastCare.AddHours(b.WiltHours.Value+24).ToLocalTime():g} · check in game");
        }
        if (b.ObservedAt is {} seen) DrawGardenInfoLabel("sync", $"Last observed: {seen.ToLocalTime():g}");
    }

}
