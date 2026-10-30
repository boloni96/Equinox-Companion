using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private GardenCareRun? gardenCareRun;
    private DateTimeOffset gardenCareNext;
    private SharedGardenPlan? gardenAutoPlantPlan;
    private Queue<SharedGardenPlan>? gardenHousePlant;
    private ulong gardenHousePlantOwner;
    private uint gardenHousePlantWorld;
    private string gardenHousePlantStatus="";
    private bool gardenCareWholeHouse;
    private SharedGardenPlan? gardenPartialHarvest;
    private bool gardenPartialSelected;
    private SharedGardenPlan[]? gardenPartialHouse;
    private Queue<(SharedGardenPlan Plan,GardenCareAction Action,int[]? Beds)>? gardenHouseHarvest;
    private bool gardenPartialHouseSelected;
    private readonly HashSet<(int Batch,int Bed)> gardenHarvestSelection=[];
    private string gardenHarvestScope="";
    private void UpdateGardenCare(DateTimeOffset now)
    {
        if(now<gardenCareNext)return;
        gardenCareNext=now.AddMilliseconds(200);TickTendAdventure(now);
        if(gardenCareRun?.Running==true)gardenCareRun.Tick(now);
        if(gardenHouseHarvest!=null&&gardenCareRun?.Running!=true){
            if(gardenCareRun?.Status.StartsWith("Stopped")==true)gardenHouseHarvest=null;
            else if(gardenHouseHarvest.TryDequeue(out var nextBatch))StartGardenCare(nextBatch.Plan,nextBatch.Action,allowedBeds:nextBatch.Beds);
            else gardenHouseHarvest=null;
        }
        if(gardenAutoPlantPlan is {} plan&&gardenCareRun?.Running!=true){
            if(gardenCareRun?.Status.StartsWith("Stopped")==true){gardenAutoPlantPlan=null;gardenHousePlant=null;return;}
            ContinueAutoPlant(plan);
        }
        if(gardenHousePlant!=null&&gardenAutoPlantPlan==null&&gardenCareRun?.Running!=true){
            if(gardenCareRun?.Status.StartsWith("Stopped")==true){gardenHousePlant=null;return;}
            if(!Player.IsLoaded||Player.ContentId!=gardenHousePlantOwner||Player.CurrentWorld.RowId!=gardenHousePlantWorld){StopHousePlant("Stopped: character or world changed.");return;}
            if(gardenHousePlant.TryDequeue(out var batch)){
                gardenHousePlantStatus=$"Auto plant · Batch {batch.Batch} · {gardenHousePlant.Count} batches remaining";
                gardenAutoPlantPlan=batch;ContinueAutoPlant(batch);
            }else {gardenHousePlant=null;gardenHousePlantStatus="Complete · house saved plans finished.";}
        }
    }
    private void StopHousePlant(string reason){gardenHousePlant=null;gardenAutoPlantPlan=null;gardenHousePlantStatus=reason;gardenCareRun?.Stop(reason);}
    private void StartHousePlant(SharedGardenPlan plan)
    {
        var plans=GardenAutoPlant.HousePlans(GardenPlanSources().Where(p=>p.HouseId==plan.HouseId).Select(EffectiveGardenPlan),plan.HouseId);
        gardenHouseHarvest=null;gardenCareRun=null;gardenAutoPlantPlan=null;
        gardenHousePlantOwner=Player.ContentId;gardenHousePlantWorld=Player.CurrentWorld.RowId;
        gardenHousePlant=plans.Length>0?new(plans):null;
        gardenHousePlantStatus=plans.Length>0?$"Auto plant · {plans.Length} saved batches queued":"No unfinished saved plans at this house.";
    }
    private void ContinueAutoPlant(SharedGardenPlan source)
    {
        try{
            var live=GardenPlanSources().FirstOrDefault(x=>x.HouseId==source.HouseId&&x.Batch==source.Batch)??throw new InvalidOperationException("Saved plan unavailable.");
            if(live.At!=source.At)throw new InvalidOperationException("Saved plan changed. Review it before restarting.");
            var plan=EffectiveGardenPlan(live);var next=GardenAutoPlant.Next(plan);
            if(next==null){gardenAutoPlantPlan=null;return;}
            var port=new GardenCarePort(this,plan);port.Validate();
            port.CheckPlantSupplies(plan.Beds.First(b=>b.Bed==next.Value.Bed));
            gardenCareRun=new(port);gardenCareRun.Start([next.Value.Bed],next.Value.Action,DateTimeOffset.UtcNow);
        }catch(Exception e){StopHousePlant("Stopped: "+e.Message);PrintEquinox("Auto plant: "+e.Message);}
    }
    private void StartHouseHarvest(SharedGardenPlan[] plans)=>StartHouseCare(plans,GardenCareAction.Harvest,gardenPartialHouseSelected);
    private void StartHouseCare(SharedGardenPlan[] plans,GardenCareAction action,bool selected=false)
    {
        if(gardenCareRun?.Running==true||gardenTendRoute!=null)return;
        gardenHousePlant=null;gardenHousePlantStatus="";gardenAutoPlantPlan=null;gardenHouseHarvest=new(plans.Select(p=>(p,action,selected?p.Beds.Where(b=>gardenHarvestSelection.Contains((p.Batch,b.Bed))).Select(b=>b.Bed).ToArray():null)));gardenCareRun=null;
    }
    private void StartGardenCare(SharedGardenPlan plan,GardenCareAction action,bool selected=false,int[]? allowedBeds=null)
    {
        if(gardenCareRun?.Running==true)return;
        try{
            var port=new GardenCarePort(this,plan);port.Validate();
            if(SellerAddonPresentForGarden())throw new InvalidOperationException("Close the current game dialogue or item menu before starting garden care.");
            gardenCareRun=new(port);
            gardenCareRun.Start(plan.Beds.Where(b=>GardenCarePolicy.Eligible(plan,b,action,DateTimeOffset.UtcNow)&&(!selected||gardenHarvestSelection.Contains((plan.Batch,b.Bed)))&&(allowedBeds==null||allowedBeds.Contains(b.Bed))).Select(b=>b.Bed),action,DateTimeOffset.UtcNow);
        }catch(Exception e){gardenHouseHarvest=null;PrintEquinox("Garden care: "+e.Message);}
    }
    private static unsafe bool SellerAddonPresentForGarden()=>new[]{"SelectString","SelectYesno","SelectIconString","Talk","ContextMenu","HousingGardening"}.Any(n=>SellerAddon(n)!=null);
    private void DrawGardenCareProgress()
    {
        if(gardenHousePlantStatus.Length>0)ImGui.TextWrapped(gardenHousePlantStatus);
        if(gardenHousePlant!=null&&ImGui.SmallButton("Stop house planting"))StopHousePlant("Stopped by you · an already-sent action may still finish.");
        if(gardenPartialHouse is {} house){
            ImGui.TextWrapped("Some crops in this house are still growing. Harvest only ready, unprotected beds across its batches now?");
            if(ImGui.Button("Harvest ready beds across batches")){StartHouseHarvest(house);gardenPartialHouse=null;}
            ImGui.SameLine();if(ImGui.Button("Wait / cancel house harvest"))gardenPartialHouse=null;
        }
        if(gardenHouseHarvest!=null)ImGui.TextDisabled($"House care · {gardenHouseHarvest.Count} batches queued");
        if(gardenPartialHarvest is {} partial){
            ImGui.TextWrapped("Some crops are still growing. Harvest only the ready, unprotected beds now?");
            if(ImGui.Button("Harvest ready beds now")){StartGardenCare(partial,GardenCareAction.Harvest,gardenPartialSelected);gardenPartialHarvest=null;gardenPartialSelected=false;}
            ImGui.SameLine();if(ImGui.Button("Wait for whole batch")){gardenPartialHarvest=null;gardenPartialSelected=false;}
        }
        if(gardenCareRun==null||gardenCareRun.Status.Length==0)return;
        if(gardenCareRun.Running){
            if(ImGui.SmallButton("Stop garden care")){gardenHousePlant=null;gardenHousePlantStatus="";gardenAutoPlantPlan=null;gardenHouseHarvest=null;StopTendAdventure("Stopped by you.");gardenCareRun.Stop("Stopped by you · an already-sent action may still finish.");}
            ImGui.SameLine();ImGui.ProgressBar((float)gardenCareRun.Completed/Math.Max(1,gardenCareRun.Total),new Vector2(Math.Max(100,ImGui.GetContentRegionAvail().X),ImGui.GetFrameHeight()),gardenCareRun.Status);
        }else ImGui.TextWrapped(gardenCareRun.Status);
    }
    private void StartScopedGardenCare(SharedGardenPlan plan,GardenCareAction action,DateTimeOffset now)
    {
        if(gardenCareWholeHouse)StartHouseCare(GardenCarePolicy.HouseCare(GardenPlanSources().Where(p=>p.HouseId==plan.HouseId).Select(EffectiveGardenPlan),plan.HouseId,action,now),action);
        else StartGardenCare(plan,action);
    }
    private void DrawGardenCareMenu(SharedGardenPlan plan,string popup)
    {
        ImGui.SetNextWindowSize(new Vector2(360,0),ImGuiCond.Always);
        if(!ImGui.BeginPopup(popup))return;
        var scope=plan.HouseId;
        if(gardenHarvestScope!=scope){gardenHarvestScope=scope;gardenHarvestSelection.Clear();}
        var now=DateTimeOffset.UtcNow;
        ImGui.TextUnformatted($"Batch {plan.Batch} · care actions");
        var walk=config.GardenCareWalk;if(ImGui.Checkbox("Walk to mapped beds (Lifestream)",ref walk)){config.GardenCareWalk=walk;SaveConfiguration();}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Short direct approaches inside this plot, up to 30 yalms. Lifestream movement does not find a route around furniture; blocked approaches stop on timeout.");
        ImGui.BeginDisabled(gardenHousePlant!=null||gardenAutoPlantPlan!=null||gardenHouseHarvest!=null||gardenTendRoute!=null||gardenCareRun?.Running==true||!atOutdoorEstate||SharedGardenLocation.Match(currentAddress,[plan])!=plan.HouseId);
        var multipleBatches=plan.Capacity>1||GardenPlanSources().Any(p=>p.HouseId==plan.HouseId&&p.Batch>1);
        if(multipleBatches){
            ImGui.TextDisabled("Apply actions to");
            if(ImGui.RadioButton("This batch",!gardenCareWholeHouse))gardenCareWholeHouse=false;
            ImGui.SameLine();if(ImGui.RadioButton("Whole house",gardenCareWholeHouse))gardenCareWholeHouse=true;
        }else gardenCareWholeHouse=false;
        if(ImGui.BeginTable("GardenCareActions",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();
            if(ImGui.Button("Auto plant",new Vector2(-1,0))){
                gardenHousePlantStatus="";
                if(gardenCareWholeHouse)StartHousePlant(plan);else {gardenAutoPlantPlan=plan;gardenCareRun=null;ContinueAutoPlant(plan);}
                ImGui.CloseCurrentPopup();}
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Plant each selected batch's own saved plan: exact seeds, soil and step order, including verified starter replacement. Whole house skips batches without an unfinished saved plan. Missing supplies or an unsafe step stop the queue.");
            ImGui.TableNextColumn();
            if(ImGui.Button("Tend due",new Vector2(-1,0))){StartScopedGardenCare(plan,GardenCareAction.Tend,now);ImGui.CloseCurrentPopup();}
            ImGui.TableNextRow();ImGui.TableNextColumn();
            if(ImGui.Button("Fertilize",new Vector2(-1,0))){StartScopedGardenCare(plan,GardenCareAction.Fertilize,now);ImGui.CloseCurrentPopup();}
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Fertilizes eligible growing beds. Fishmeal must be in bags; recorded cooldowns are respected.");
            ImGui.TableNextColumn();
            if(ImGui.Button("Harvest",new Vector2(-1,0))){
                gardenPartialHarvest=null;gardenPartialSelected=false;gardenPartialHouseSelected=false;
                if(gardenCareWholeHouse){var plans=GardenCarePolicy.HouseHarvest(GardenPlanSources().Where(p=>p.HouseId==plan.HouseId).Select(EffectiveGardenPlan),plan.HouseId,now);
                    if(plans.Any(GardenAutoPlant.PartialHarvest))gardenPartialHouse=plans;else StartHouseHarvest(plans);}
                else if(GardenAutoPlant.PartialHarvest(plan))gardenPartialHarvest=plan;else StartGardenCare(plan,GardenCareAction.Harvest);
                ImGui.CloseCurrentPopup();}
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Harvests eligible beds in the chosen scope. Protected crops stay; mixed maturity asks before harvesting ready beds.");
            ImGui.EndTable();
        }
        ImGui.Separator();
        if(ImGui.TreeNode(multipleBatches?"Choose beds to harvest · all batches":"Choose beds to harvest")){
            var housePlans=GardenPlanSources().Where(p=>p.HouseId==plan.HouseId).OrderBy(p=>p.Batch).Select(EffectiveGardenPlan).ToArray();
            if(ImGui.SmallButton("Select eligible")){foreach(var hp in housePlans)foreach(var bed in hp.Beds.Where(b=>GardenCarePolicy.Eligible(hp,b,GardenCareAction.Harvest,now)))gardenHarvestSelection.Add((hp.Batch,bed.Bed));}
            ImGui.SameLine();if(ImGui.SmallButton("Clear selected"))gardenHarvestSelection.Clear();
            ImGui.BeginChild("house-harvest-beds",new Vector2(0,Math.Min(320,ImGui.GetTextLineHeightWithSpacing()*15)),false);
            foreach(var hp in housePlans){ImGui.PushID(hp.Batch);if(multipleBatches)ImGui.TextUnformatted($"Batch {hp.Batch}");
                foreach(var bed in hp.Beds){var eligible=GardenCarePolicy.Eligible(hp,bed,GardenCareAction.Harvest,now);var key=(hp.Batch,bed.Bed);var pick=gardenHarvestSelection.Contains(key);
                    ImGui.BeginDisabled(!eligible);if(ImGui.Checkbox($"Bed {bed.Bed} · {bed.ActualCrop}",ref pick)){if(pick)gardenHarvestSelection.Add(key);else gardenHarvestSelection.Remove(key);}ImGui.EndDisabled();
                    if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(GardenCarePolicy.Protection(hp,bed) is {Length:>0} why?why:GardenCarePolicy.Certainty(bed));}
                ImGui.PopID();}
            ImGui.EndChild();
            if(ImGui.Button("Harvest selected beds")){
                var chosen=housePlans.Where(hp=>hp.Beds.Any(b=>gardenHarvestSelection.Contains((hp.Batch,b.Bed))&&GardenCarePolicy.Eligible(hp,b,GardenCareAction.Harvest,now))).ToArray();
                gardenPartialHouseSelected=true;gardenPartialHarvest=null;
                if(chosen.Any(GardenAutoPlant.PartialHarvest))gardenPartialHouse=chosen;else StartHouseCare(chosen,GardenCareAction.Harvest,true);
                ImGui.CloseCurrentPopup();}
            ImGui.TreePop();
        }
        ImGui.EndDisabled();
        var kept=plan.Beds.Where(b=>GardenCarePolicy.Protection(plan,b).Length>0).ToArray();
        if(kept.Length>0){ImGui.TextDisabled($"{kept.Length} beds protected");if(ImGui.IsItemHovered())ImGui.SetTooltip(string.Join("\n",kept.Select(b=>$"Bed {b.Bed}: {GardenCarePolicy.Protection(plan,b)}")));}
        if(ImGui.TreeNode("When can I harvest a neighbour?")){ImGui.TextWrapped(GardenCarePolicy.KeepGuide);ImGui.TreePop();}
        ImGui.TextWrapped("Stand within reach. Actions verify the numbered bed and game confirmation. No travel. Fishmeal must be in bags. Unknown beds need inspection first.");
        if(ImGui.Button("Supplies / missing items")){PrepareGardenSupplies(plan);ImGui.CloseCurrentPopup();}
        ImGui.EndPopup();
    }
    private sealed unsafe class GardenCarePort : IGardenCarePort
    {
        private readonly Plugin p;private readonly SharedGardenPlan original;private readonly ulong owner;private readonly uint world;private Vector3 origin;private bool walking;private int walkedBed;
        private ulong targetId;private int openedBed;private DateTimeOffset opened,actionAt,next;private bool menuSent,itemOpened,itemSent;private InventoryType fertilizerBag;private int fertilizerSlot;private uint fertilizerId;private bool confirmationSent;private uint plantSeed,plantSoil;private GardenCareAction? issuedAction;
        public GardenCarePort(Plugin plugin,SharedGardenPlan plan){p=plugin;original=plan;owner=Player.ContentId;world=Player.CurrentWorld.RowId;origin=Objects.LocalPlayer?.Position??default;}
        private SharedGardenPlan Current(){var source=p.GardenPlanSources().FirstOrDefault(x=>x.HouseId==original.HouseId&&x.Batch==original.Batch)??throw new InvalidOperationException("Garden plan is no longer available.");if(source.At!=original.At)throw new InvalidOperationException("Garden plan changed during the run.");return p.EffectiveGardenPlan(source);}
        public void Validate()
        {
            if(!Player.IsLoaded||owner==0||Player.ContentId!=owner||Player.CurrentWorld.RowId!=world||!p.ObservingGardens||!p.atOutdoorEstate||SharedGardenLocation.Match(p.currentAddress,[original])!=original.HouseId||Objects.LocalPlayer is not {} player||(!walking&&Vector3.Distance(player.Position,origin)>1.5f)||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.Unconscious]||Conditions[ConditionFlag.TradeOpen]||p.HelperInCutscene()||p.sellerRun.Running||p.TraderRunning||p.retrievalRun.Running||p.organizeRunning||p.chestRunning)
                throw new InvalidOperationException("Character, location, movement or active operation changed.");
            if(!GardenCarePolicy.TargetAllowed(targetId,Targets.Target?.GameObjectId??0,menuSent,issuedAction))throw new InvalidOperationException("Selected garden target changed.");
        }
        public bool Open(int bed)
        {
            var now=DateTimeOffset.UtcNow;
            if(walking){if(p.LifestreamBusy())return false;walking=false;origin=Objects.LocalPlayer!.Position;}
            if(openedBed!=bed){
                if(SellerAddonPresentForGarden())return false;
                var plan=Current();var resolver=p.GardenResolver();var found=Objects.Where(o=>o.IsTargetable&&o.BaseId==2003757&&o.ObjectKind==ObjectKind.EventObj&&o.Address!=0&&Vector3.Distance(o.Position,Objects.LocalPlayer!.Position)<=(p.config.GardenCareWalk?30f:4.5f)).Where(o=>{
                    var arg=((FFXIVClientStructs.FFXIV.Client.Game.Object.EventObject*)o.Address)->Arg;
                    var context=p.snapshot;if(context==null)return false;
                    var candidate=context with{ObservedAt=now,TargetId=o.GameObjectId.ToString("X16"),TargetDetails=new(o.BaseId,o.EntityId,o.ObjectKind.ToString(),o.Position.X,o.Position.Y,o.Position.Z,arg)};
                    return GardenTargetMap.SelectedBatch(candidate,resolver,[plan]) is {} hit&&hit.Bed==bed;
                }).Take(2).ToArray();
                if(found.Length!=1)throw new InvalidOperationException($"Bed {bed} is out of reach or its identity needs syncing.");
                if(Vector3.Distance(found[0].Position,Objects.LocalPlayer!.Position)>3f){
                    if(walkedBed==bed)throw new InvalidOperationException("Could not reach the bed. Clear the approach or move closer, then retry.");
                    if(!p.config.GardenCareWalk)throw new InvalidOperationException("Move closer to the garden or enable Walk to mapped beds.");
                    if(p.LifestreamBusy())throw new InvalidOperationException("Another Lifestream journey is active.");
                    var offset=Objects.LocalPlayer.Position-found[0].Position;offset.Y=0;if(offset.LengthSquared()<.01f)throw new InvalidOperationException("Cannot choose a garden approach point.");
                    var destination=found[0].Position+Vector3.Normalize(offset)*2.2f;
                    Pi.GetIpcSubscriber<List<Vector3>,bool?,float?,float?,object>("Lifestream.MoveEx").InvokeAction([destination],true,.3f,.3f);walking=true;walkedBed=bed;return false;
                }
                openedBed=bed;targetId=found[0].GameObjectId;opened=now;menuSent=itemOpened=itemSent=confirmationSent=false;issuedAction=null;actionAt=default;
                Targets.Target=found[0];TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)found[0].Address,false);return false;
            }
            if(now>=next&&SellerAddon("Talk")!=null){p.ClickVisibleTalk();next=now.AddMilliseconds(800);}
            return Menu(bed)!=null;
        }
        private GardenMenu? Menu(int bed)
        {
            var m=p.activeGardenMenu;var patch=original.PhysicalPatch>0?original.PhysicalPatch:original.Batch;
            return m!=null&&m.OpenedAt>=opened&&m.ClosedAt==null&&m.Target.TargetId==targetId.ToString("X16")&&m.NumberedLocation()==(patch,bed)&&SellerAddon("SelectString")== (AtkUnitBase*)m.AddonAddress?m:null;
        }
        public bool Act(int bed,GardenCareAction action)
        {
            if(action==GardenCareAction.Inspect){actionAt=opened;return Menu(bed)!=null;}
            if(!menuSent){
                var plan=Current();var crop=plan.Beds.First(b=>b.Bed==bed);
                if(action is not (GardenCareAction.Plant or GardenCareAction.RemoveStarter)&&!GardenCarePolicy.Eligible(plan,crop,action,DateTimeOffset.UtcNow))throw new InvalidOperationException($"Bed {bed} changed or is protected. Reopen care actions.");
                var m=Menu(bed);if(m==null)return false;
                if(action==GardenCareAction.Harvest&&m.ReadyLocation()==null)throw new InvalidOperationException($"Bed {bed} is not confirmed harvestable in game.");
                if(action==GardenCareAction.Harvest&&(InventoryManager.Instance()==null||InventoryManager.Instance()->GetEmptySlotsInBag()<2))throw new InvalidOperationException("Free two bag slots for crop and possible seeds.");
                if(action is GardenCareAction.Plant or GardenCareAction.RemoveStarter){var step=GardenAutoPlant.Next(plan);if(step!=(bed,action))throw new InvalidOperationException("The planting step changed.");CheckPlantSupplies(crop);}
                var label=action==GardenCareAction.Plant?"Plant Seeds":action==GardenCareAction.RemoveStarter?"Remove Crop":action==GardenCareAction.Tend?"Tend Crop":action==GardenCareAction.Fertilize?"Fertilize Crop":"Harvest Crop";
                var index=Array.IndexOf(m.Options,label);if(index<0)throw new InvalidOperationException(label+" is not available for this bed.");
                if(action==GardenCareAction.Fertilize&&!FindFishmeal())throw new InvalidOperationException("Put Fishmeal in your bags before fertilizing.");
                issuedAction=action;actionAt=DateTimeOffset.UtcNow;SelectTravelChoice((AtkUnitBase*)m.AddonAddress,index);menuSent=true;
                return action is not (GardenCareAction.Fertilize or GardenCareAction.Plant);
            }
            if(action==GardenCareAction.Plant){
                var plant=AgentHousingPlant.Instance();if(plant==null||plant->PlotType!=14||SellerAddon("HousingGardening")==null)return false;
                if(!FindBagItem(plantSoil,out var soil)||!FindBagItem(plantSeed,out var seed))throw new InvalidOperationException("Required seed or soil is no longer in bags.");
                plant->SelectedItems[0]=soil;plant->SelectedItems[1]=seed;plant->ConfirmSeedAndSoilSelection();return true;
            }
            if(action!=GardenCareAction.Fertilize)return true;
            var housing=AgentHousingPlant.Instance();if(housing==null||housing->PlotType!=15)return false;
            var agent=AgentInventoryContext.Instance();if(agent==null)return false;
            if(!itemOpened){if(SellerAddon("ContextMenu")!=null)throw new InvalidOperationException("Another item menu is open.");var module=AgentModule.Instance();var bagAgent=module==null?null:module->GetAgentByInternalId(AgentId.Inventory);if(bagAgent==null)return false;agent->OpenForItemSlot(fertilizerBag,fertilizerSlot,0,bagAgent->GetAddonId());itemOpened=true;return false;}
            if(itemSent)return true;
            if(agent->TargetInventoryId!=fertilizerBag||agent->TargetInventorySlotId!=fertilizerSlot||agent->TargetInventorySlot==null||agent->TargetInventorySlot->ItemId!=fertilizerId)throw new InvalidOperationException("Fertilizer item selection changed.");
            var menu=SellerAddon("ContextMenu");if(menu==null)return false;var index2=0;var found=-1;
            foreach(var v in agent->EventParams){if(v.Type!=AtkValueType.String)continue;var text=TravelMenuText(v.String);if(text is "Fertilize" or "Fertilize Crop")found=index2;index2++;}
            if(found<0)throw new InvalidOperationException("Could not verify the fertilizer menu action.");
            var values=stackalloc AtkValue[5];for(var i=0;i<5;i++)values[i]=new(){Type=AtkValueType.Int};values[1].Int=found;menu->FireCallback(5,values,true);itemSent=true;return true;
        }
        public void CheckPlantSupplies(SharedGardenBed bed)
        {
            var names=new[]{p.GardenSeedItemName(bed.Crop),GardenPlantRequirement.Soil(bed)};
            var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
            plantSeed=sheet.FirstOrDefault(i=>i.Name.ToString()==names[0]).RowId;plantSoil=sheet.FirstOrDefault(i=>i.Name.ToString()==names[1]).RowId;
            if(plantSeed==0||plantSoil==0||!FindBagItem(plantSeed,out _)||!FindBagItem(plantSoil,out _))throw new InvalidOperationException("Missing exact seed / soil for the next step: "+string.Join(" + ",names));
        }
        private static bool FindBagItem(uint id,out AgentHousingPlant.SelectedItem result)
        {
            result=default;var m=InventoryManager.Instance();if(m==null)return false;
            foreach(var t in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4}){var c=m->GetInventoryContainer(t);if(c==null||!c->IsLoaded||c->Items==null)continue;
                for(var i=0;i<c->Size;i++)if(c->Items[i].ItemId==id&&c->Items[i].Quantity>0){result=new(){InventoryType=t,InventorySlot=(ushort)i,ItemId=id};return true;}}
            return false;
        }
        private bool FindFishmeal()
        {
            var manager=InventoryManager.Instance();if(manager==null)return false;
            foreach(var type in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4}){
                var c=manager->GetInventoryContainer(type);if(c==null||!c->IsLoaded||c->Items==null)continue;
                for(var i=0;i<c->Size;i++){var item=c->Items[i];if(item.ItemId==0||item.Quantity<=0)continue;
                    if(DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(item.ItemId)?.Name.ToString()!="Fishmeal")continue;
                    fertilizerBag=type;fertilizerSlot=i;fertilizerId=item.ItemId;return true;}
            }return false;
        }
        private bool MatchesPrompt(GardenCareAction action,string prompt)
        {
            if(action!=GardenCareAction.Plant)return GardenAutoPlant.MatchesConfirmation(action,prompt);
            var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
            return GardenAutoPlant.MatchesPlantConfirmation(prompt,sheet.GetRowOrDefault(plantSeed)?.Name.ToString()??"",sheet.GetRowOrDefault(plantSoil)?.Name.ToString()??"");
        }
        public bool Confirmed(int bed,GardenCareAction action,DateTimeOffset sent)
        {
            // Opening this exact numbered menu is the inspection receipt. Identical
            // observations may be deduplicated; do not require a rewritten timestamp.
            if(action==GardenCareAction.Inspect)return Menu(bed)!=null&&DateTimeOffset.UtcNow-opened>=TimeSpan.FromSeconds(1);
            var dialog=(AddonSelectYesno*)SellerAddon("SelectYesno");
            if(dialog!=null){
                if(confirmationSent)return false;
                if(dialog->PromptText==null||dialog->YesButton==null||!dialog->YesButton->IsEnabled||!MatchesPrompt(action,dialog->PromptText->NodeText.ToString()))throw new InvalidOperationException("Unrecognized garden confirmation; review it manually.");
                if(action==GardenCareAction.Plant&&GardenAutoPlant.Next(Current())!=(bed,action))throw new InvalidOperationException("Saved planting step changed before confirmation.");
                if(action==GardenCareAction.RemoveStarter&&GardenAutoPlant.Next(Current())!=(bed,action))throw new InvalidOperationException("Starter replacement changed.");
                dialog->FireCallbackInt(0);confirmationSent=true;return false;
            }
            var kind=action==GardenCareAction.Plant?"garden.planted":action==GardenCareAction.Tend?"garden.tended":action==GardenCareAction.Fertilize?"garden.fertilized":"garden.empty";
            return p.LocalGardenActions().Any(e=>e.Kind==kind&&e.At>=actionAt&&e.Actor.ContentId==owner.ToString()&&e.Patch==(original.PhysicalPatch>0?original.PhysicalPatch:original.Batch)&&e.Bed==bed&&SharedGardenLocation.Match(e.Address,[original])==original.HouseId);
        }
        public bool Close(int bed)
        {
            if(DateTimeOffset.UtcNow<next)return false;
            if(SellerAddon("Talk")!=null){p.ClickVisibleTalk();next=DateTimeOffset.UtcNow.AddMilliseconds(800);return false;}
            if(Menu(bed) is {} m){var quit=Array.IndexOf(m.Options,"Quit");if(quit<0)throw new InvalidOperationException("Could not close the garden menu.");SelectTravelChoice((AtkUnitBase*)m.AddonAddress,quit);next=DateTimeOffset.UtcNow.AddMilliseconds(800);return false;}
            if(SellerAddonPresentForGarden())return false;
            targetId=0;openedBed=0;walkedBed=0;return true;
        }
        public void Release(){if(walking){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}walking=false;}p.RequestStorageRefresh();p.nextRosterRead=default;if(p.syncFailures==0)p.nextSync=default;}
    }
}
