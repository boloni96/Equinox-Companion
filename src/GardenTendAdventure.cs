using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record GardenAdventureWork(string TaskId,string Character,string House,int Batch,AdventureTaskRule Rule);
    private Queue<GardenAdventureWork>? gardenTendRoute;
    private ulong gardenTendOwner;
    private string gardenTendBoard="",gardenTendGeneration="",gardenTendStatus="";
    private HashSet<string> gardenAdventureCharacters=[];
    private DateTimeOffset gardenTendDeadline,gardenAdventureNext;
    private bool gardenPlotApproaching,gardenPlotApproachAttempted;
    private DateTimeOffset gardenPlotApproachAt;
    private readonly GardenWalkProgress gardenPlotProgress=new();
    private readonly GardenWalkRecovery gardenPlotRecovery=new();
    private readonly GardenArrivalGate gardenArrival=new();
    private bool gardenTendTravelSent,gardenTendStarted,gardenAdventureSwitchSent,gardenTendPreparing;
    private bool gardenAdventureTend=true,gardenAdventurePlant,gardenAdventureHarvest,gardenAdventureSwitch;
    private readonly Dictionary<string,DateTimeOffset> gardenAdventureInspected=[];
    private void DrawTendAdventure(AdventureBoard board)
    {
        if(!board.Stops.Any(s=>s.Tasks.Any(AdventurePolicy.IsGardenTask)))return;
        if(ImGui.CollapsingHeader("Automatic gardening")){
            ImGui.BeginDisabled(gardenTendRoute!=null);
            if(ImGui.BeginTable("AutomaticGardenOptions",2,ImGuiTableFlags.SizingStretchSame)){
                ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.Checkbox("Tend due",ref gardenAdventureTend);ImGui.TableNextColumn();ImGui.Checkbox("Plant saved plans",ref gardenAdventurePlant);
                ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.Checkbox("Harvest full batches",ref gardenAdventureHarvest);ImGui.EndTable();
            }
            ImGui.Checkbox("Switch assigned characters on this account",ref gardenAdventureSwitch);
            var walk=config.GardenCareWalk;if(ImGui.Checkbox("Walk to mapped beds (Lifestream)",ref walk)){config.GardenCareWalk=walk;SaveConfiguration();}
            ImGui.TextWrapped("Uses the assigned gardeners on this character's Journal account. The growing adventure pauses when no work is ready; resume it later. Planting uses its exact saved seed, soil and bed order; with Harvest full batches enabled, ready crops are harvested before the next route layout is applied. Mixed maturity and protected neighbours wait. After teleporting, Walk to mapped beds also makes a short approach through the plot entrance. Stop remains available during active work.");
            ImGui.BeginDisabled(!adventureFresh||gardenCareRun?.Running==true||!Player.IsLoaded||!(gardenAdventureTend||gardenAdventurePlant||gardenAdventureHarvest));
            if(ImGui.Button("Resume automatic gardening"))StartTendAdventure(board);ImGui.EndDisabled();ImGui.EndDisabled();
        }
        if(gardenTendRoute!=null){if(ImGui.SmallButton("Stop automatic gardening"))StopTendAdventure("Stopped by you.");ImGui.SameLine();}
        if(gardenTendStatus.Length>0)ImGui.TextWrapped(gardenTendStatus);
        DrawGardenCareProgress();
    }
    private void StartTendAdventure(AdventureBoard board)
    {
        var current=CurrentSharedCharacter();if(current==null||gardenCareRun?.Running==true)return;
        var person=config.SharedRoster?.People.FirstOrDefault(p=>p.Characters.Any(c=>c.Id==current.Id));
        gardenAdventureCharacters=(person?.Characters??[]).Where(c=>c.AccountId==current.AccountId&&(gardenAdventureSwitch||c.Id==current.Id)).Select(c=>c.Id).ToHashSet();
        gardenTendRoute=new();gardenTendOwner=Player.ContentId;gardenTendBoard=board.Id;gardenTendGeneration=board.Generation;
        gardenTendPreparing=false;gardenTendTravelSent=gardenTendStarted=gardenAdventureSwitchSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;gardenCareRun=null;gardenAdventureNext=default;
        gardenTendStatus="Checking this account's assigned garden work…";
    }
    private void StopTendAdventure(string reason)
    {
        if(gardenTendRoute==null)return;
        CancelAssignedLogin(reason);gardenPlotApproaching=false;gardenTendRoute=null;gardenAutoPlantPlan=null;gardenCareRun?.Stop(reason);gardenTendStatus=reason;
        goSeeHomeTrip=null;
        if(gardenAdventureSwitchSent){characterSwitchUntil=default;characterSwitchTarget=null;gardenAdventureSwitchSent=false;}
        try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}
    }
    private unsafe void TryApproachGardenPlot(SharedGardenPlan plan,DateTimeOffset now)
    {
        var manager=HousingManager.Instance();var player=Objects.LocalPlayer;
        static string Norm(string? text)=>(text??"").Trim().Replace("The ","",StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        if(manager==null||manager->GetCurrentHousingTerritoryType()!=HousingTerritoryType.Outdoor||player==null||
            Norm(WorldName(Player.CurrentWorld.RowId))!=Norm(plan.World)||Norm(DistrictName(Client.TerritoryType))!=Norm(plan.District)||manager->GetCurrentWard()!=plan.Ward-1||manager->GetCurrentDivision()!=(plan.Plot>30?2:1)||atOutdoorEstate||Conditions[ConditionFlag.Casting])return;
        if(!config.GardenCareWalk)throw new InvalidOperationException("Enable Walk to mapped beds to approach the plot after teleporting, or move onto the plot first.");
        if(gardenPlotApproachAttempted)throw new InvalidOperationException("Plot entrance approach already attempted. Move inside the destination plot and restart.");
        if(Conditions[ConditionFlag.Unconscious]||Conditions[ConditionFlag.TradeOpen]||SellerAddonPresentForGarden())return;
        var entrance=Pi.GetIpcSubscriber<uint,int,Vector3?>("Lifestream.GetPlotEntrance").InvokeFunc(Client.TerritoryType,plan.Plot-1)
            ??throw new InvalidOperationException("Lifestream has no entrance position for this plot. Move onto the plot first.");
        // The game supplies the arrival facing. Read it only after travel and settling finish.
        var path=GardenPlotApproachPolicy.Path(player.Position,entrance,null,player.Rotation);
        Pi.GetIpcSubscriber<List<Vector3>,bool?,float?,float?,object>("Lifestream.MoveEx").InvokeAction(path.ToList(),true,.3f,.3f);
        gardenPlotApproaching=gardenPlotApproachAttempted=true;gardenPlotApproachAt=now;gardenPlotProgress.Reset(player.Position,now);gardenPlotRecovery.Reset(player.Position,path[^1]);
        gardenTendStatus="Walking through the plot entrance · "+plan.HouseName;
    }
    private bool TryRecoverGardenPlot(Vector3 position,DateTimeOffset now)
    {
        var path=gardenPlotRecovery.Next(position,now);if(path==null)return false;
        Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();
        Pi.GetIpcSubscriber<List<Vector3>,bool?,float?,float?,object>("Lifestream.MoveEx").InvokeAction(path.ToList(),true,.3f,.3f);
        gardenPlotProgress.Reset(position,now);gardenTendStatus="Plot approach blocked · trying a short sidestep";return true;
    }
    private static bool SameGardenDefinition(GardenPlanDefinition? a,GardenPlanDefinition? b)=>a!=null&&b!=null&&a.Draft.Target==b.Draft.Target&&
        a.Steps.OrderBy(s=>s.Bed).Select(s=>(s.Bed,s.Order,s.Crop,s.Soil,s.ReplantOrder,s.StarterSoil,s.Neighbour)).SequenceEqual(b.Steps.OrderBy(s=>s.Bed).Select(s=>(s.Bed,s.Order,s.Crop,s.Soil,s.ReplantOrder,s.StarterSoil,s.Neighbour)));
    private GardenCareAction? GardenAdventureAction(SharedGardenPlan plan,AdventureTaskRule rule,DateTimeOffset now)
    {
        var scope=rule.Jobs??new[]{rule.Kind};var cycle=rule.Kind=="growingcycle";
        if(cycle&&rule.Phase is "waiting" or "review")return null;
        if(gardenAdventurePlant&&(cycle||scope.Contains("planplant")||scope.Contains("plant")||rule.Kind=="garden"&&rule.Jobs==null)&&GardenGuidance.Next(plan)!=null)return GardenCareAction.Plant;
        var active=plan.Beds.Where(EquinoxCompanion.GardenVisualState.HasActualCrop).ToArray();
        if(gardenAdventureHarvest&&(cycle||scope.Contains("harvest")||rule.Kind=="garden"&&rule.Jobs==null)&&active.Length>0){
            if(active.All(b=>b.Ready)&&active.Any(b=>GardenCarePolicy.Eligible(plan,b,GardenCareAction.Harvest,now)))return GardenCareAction.Harvest;
            var key=plan.HouseId+":"+plan.Batch;
            if(active.Any(b=>!b.Ready&&GardenTiming.MaturityEstimateDue(b.HarvestAt,b.GrowingObservedAt,now))&&(!gardenAdventureInspected.TryGetValue(key,out var checkedAt)||now-checkedAt>TimeSpan.FromMinutes(10)))return GardenCareAction.Inspect;
        }
        if(gardenAdventureTend&&(cycle||scope.Contains("care")||rule.Kind=="garden"&&rule.Jobs==null)&&active.Any(b=>GardenCarePolicy.Eligible(plan,b,GardenCareAction.Tend,now)))return GardenCareAction.Tend;
        return null;
    }
    private void TickTendAdventure(DateTimeOffset now)
    {
        if(gardenTendRoute==null||now<gardenAdventureNext)return;
        gardenAdventureNext=now.AddSeconds(1);
        try{
            var board=adventureBoards.FirstOrDefault(b=>b.Id==gardenTendBoard&&b.Generation==gardenTendGeneration)??throw new InvalidOperationException("The saved adventure changed or was removed.");
            if(!adventureFresh||now-adventureFetched>TimeSpan.FromSeconds(30))throw new InvalidOperationException("Adventure connection is stale. Reconnect before resuming automatic work.");
            if(followSession.Armed||Conditions[ConditionFlag.InCombat])throw new InvalidOperationException("Another movement operation or combat started.");
            if(gardenTendRoute.Count==0){
                var chars=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).ToArray();
                var tasks=board.Stops.SelectMany(s=>s.Tasks).Where(t=>!t.Done&&AdventurePolicy.IsGardenTask(t)&&t.Rule!=null).ToArray();
                if(tasks.Length==0){StopTendAdventure("Adventure complete · all requested work confirmed.");return;}
                foreach(var task in tasks){
                    var rule=task.Rule!;var cid=AdventurePolicy.TaskCharacterId(task,chars);if(!gardenAdventureCharacters.Contains(cid)||rule.Batch is <1 or >3||rule.HouseId.Length==0)continue;
                    if(rule.Kind=="growingcycle"&&rule.Phase is "waiting" or "review")continue;
                    var source=GardenPlanSources().FirstOrDefault(p=>p.HouseId==rule.HouseId&&(rule.Kind!="growingcycle"||p.Batch==rule.Batch)&&p.Batch==rule.Batch);if(source==null)continue;
                    var live=EffectiveGardenPlan(source);
                    if(rule.Kind=="growingcycle"&&rule.Phase=="plan"&&gardenAdventurePlant||GardenAdventureAction(live,rule,now)!=null)
                        gardenTendRoute.Enqueue(new(task.Id,cid,rule.HouseId,rule.Batch,rule));
                }
                if(gardenTendRoute.Count==0){StopTendAdventure("Paused · no garden work is ready for this account. Resume automatic gardening later; saved progress is kept.");return;}
                gardenTendDeadline=now.AddMinutes(10);gardenTendPreparing=false;gardenTendTravelSent=gardenTendStarted=gardenAdventureSwitchSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;
            }
            if(now>=gardenTendDeadline)throw new InvalidOperationException("Travel or garden action timed out. Review before restarting.");
            var work=gardenTendRoute.Peek();
            if(!gardenTendStarted){
                var liveTask=board.Stops.SelectMany(s=>s.Tasks).FirstOrDefault(t=>t.Id==work.TaskId)??throw new InvalidOperationException("The active garden task was removed.");
                if(liveTask.Done){gardenTendRoute.Dequeue();gardenTendTravelSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;return;}
                if(liveTask.Rule==null)throw new InvalidOperationException("The active garden task has no rule.");
                if(liveTask.Rule.HouseId!=work.House||liveTask.Rule.Batch!=work.Batch||liveTask.Rule.Cid!=work.Character)throw new InvalidOperationException("Garden batch assignments changed. Resume automatic gardening to use the updated route.");
                work=work with{Rule=liveTask.Rule};
            }
            if(!Player.IsLoaded){gardenArrival.Reset();return;}
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.Casting]||HelperInCutscene()){gardenArrival.Reset();return;}
            if(CurrentSharedCharacter()?.Id!=work.Character){
                if(gardenAdventureSwitchSent){if(assignedLogin==null&&assignedLoginError.Length>0)throw new InvalidOperationException(assignedLoginError);return;}
                if(Player.ContentId!=gardenTendOwner)throw new InvalidOperationException("An unexpected character logged in.");
                if(!gardenAdventureSwitch)throw new InvalidOperationException("Switch to the assigned gardener first.");
                var target=config.SharedRoster!.People.SelectMany(p=>p.Characters).Single(c=>c.Id==work.Character);
                if(!StartAssignedCharacterSwitch(target))throw new InvalidOperationException(characterTravelStatus);
                if(characterSwitchTarget?.Id!=target.Id)throw new InvalidOperationException(characterTravelStatus);
                gardenAdventureSwitchSent=true;gardenTendStatus="Switching to "+target.Name;return;
            }
            gardenTendOwner=Player.ContentId;gardenAdventureSwitchSent=false;
            var sourcePlan=GardenPlanSources().FirstOrDefault(p=>p.HouseId==work.House&&p.Batch==work.Batch)??throw new InvalidOperationException("Selected batch is unavailable.");
            if(gardenTendStarted){
                if(gardenCareRun?.Running==true||gardenAutoPlantPlan!=null)return;
                if(gardenCareRun?.Status.StartsWith("Stopped")==true)throw new InvalidOperationException(gardenCareRun.Status);
                if(gardenTendPreparing){gardenTendPreparing=false;gardenTendStarted=false;gardenTendStatus="Preparation confirmed · continuing this batch";gardenAdventureNext=now.AddSeconds(1);return;}
                gardenTendRoute.Dequeue();gardenPlotApproaching=gardenPlotApproachAttempted=false;gardenTendStarted=gardenTendTravelSent=false;gardenTendDeadline=now.AddMinutes(10);gardenAdventureNext=now.AddSeconds(3);return;
            }
            if(gardenTendTravelSent&&!gardenPlotApproaching){
                var pending=now<goSeeNativeUntil||LifestreamBusy()||goSeeHomeTrip!=null||goSeeFriendTrip!=null||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("TeleportHousingFriend");
                if(!gardenArrival.Ready(pending,Objects.LocalPlayer!.Position,now)){gardenTendStatus="Waiting for teleport and travel to finish · "+sourcePlan.HouseName;return;}
            }
            var atDestination=atOutdoorEstate&&SharedGardenLocation.Match(currentAddress,[sourcePlan])==sourcePlan.HouseId;
            if(gardenPlotApproaching){
                if(atDestination){
                    // Finish the short forward leg; crossing the boundary alone can leave us beside the fence.
                    if(LifestreamBusy()){
                        if(now>=(gardenPlotRecovery.Deadline??gardenPlotApproachAt.AddSeconds(20)))
                            throw new InvalidOperationException("Forward plot approach timed out.");
                        if(Objects.LocalPlayer is {} inside&&gardenPlotProgress.Blocked(inside.Position,now)&&!TryRecoverGardenPlot(inside.Position,now))
                            throw new InvalidOperationException("Forward plot approach is blocked. Clear the path or move further inside, then restart.");
                        gardenTendStatus="Inside the plot · finishing forward approach";return;
                    }
                    gardenPlotApproaching=false;gardenTendStatus="On the destination plot · approaching garden";
                }
                else{
                    if(atOutdoorEstate)throw new InvalidOperationException("Plot approach reached a different estate. Review the entrance before restarting.");
                    if(now>=(gardenPlotRecovery.Deadline??gardenPlotApproachAt.AddSeconds(20)))throw new InvalidOperationException("Plot entrance approach timed out.");
                    if(Objects.LocalPlayer is {} stalled&&gardenPlotProgress.Blocked(stalled.Position,now)&&!TryRecoverGardenPlot(stalled.Position,now))
                        throw new InvalidOperationException("Plot entrance approach is blocked. Move inside the destination plot and restart.");
                    if(now-gardenPlotApproachAt>TimeSpan.FromSeconds(3)&&!LifestreamBusy())throw new InvalidOperationException("The short approach did not enter the destination plot. Move inside and restart.");
                    return;
                }
            }
            if(!atDestination){
                if(!gardenTendTravelSent){if(LifestreamBusy())throw new InvalidOperationException("Another Lifestream journey is active.");EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>w.Name==sourcePlan.World);if(world.Id==0)throw new InvalidOperationException("Destination world unavailable.");gardenArrival.Reset();GoSeeBid(world.Id,sourcePlan.District,sourcePlan.Ward,sourcePlan.Plot);gardenTendTravelSent=true;gardenTendStatus="Travelling to "+sourcePlan.HouseName;}
                else if(!LifestreamBusy()&&goSeeHomeTrip==null&&goSeeFriendTrip==null)TryApproachGardenPlot(sourcePlan,now);
                return;
            }
            if(LifestreamBusy()||goSeeHomeTrip!=null)return;
            var plan=EffectiveGardenPlan(sourcePlan);
            if(work.Rule.PhysicalPatch>0&&(plan.PhysicalPatch>0?plan.PhysicalPatch:plan.Batch)!=work.Rule.PhysicalPatch)throw new InvalidOperationException("Batch labels changed. Refresh the route before restarting.");
            if(work.Rule.Kind=="growingcycle"){
                if(work.Rule.PlanTemplate==null)throw new InvalidOperationException("Route planting layout is missing.");
                if(work.Rule.PlanAt.Length>0&&(!DateTimeOffset.TryParse(work.Rule.PlanAt,out var at)||plan.At!=at||!SameGardenDefinition(plan.Definition,work.Rule.PlanTemplate)))throw new InvalidOperationException("The route's saved batch plan changed.");
                if(work.Rule.PlanAt.Length==0){
                    if(!gardenAdventurePlant){gardenTendRoute.Dequeue();return;}
                    if(plan.CompletedAt!=null||!SameGardenDefinition(plan.Definition,work.Rule.PlanTemplate)||DateTimeOffset.TryParse(work.Rule.ReadyAfter,out var readyAfter)&&plan.At<readyAfter){
                        var preparation=GardenAutoPlant.PrepareRouteBeds(plan,work.Rule.PlanTemplate,now,gardenAdventureHarvest,gardenAdventureTend);
                        if(!preparation.Empty){
                            gardenTendStatus=preparation.Reason+" · "+plan.HouseName+" · Batch "+plan.Batch;
                            var key=plan.HouseId+":"+plan.Batch;
                            if(preparation.Action is {} prepareAction&&!(prepareAction==GardenCareAction.Inspect&&gardenAdventureInspected.TryGetValue(key,out var checkedAt)&&now-checkedAt<TimeSpan.FromMinutes(10))){
                                var port=new GardenCarePort(this,plan);port.Validate();gardenCareRun=new(port);
                                gardenCareRun.Start(preparation.Beds,prepareAction,now);gardenTendStarted=true;gardenTendPreparing=true;
                                if(prepareAction==GardenCareAction.Inspect)gardenAdventureInspected[key]=now;
                            }else{gardenTendRoute.Dequeue();gardenTendTravelSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;gardenAdventureNext=now.AddSeconds(10);}
                            return;
                        }
                        ChangeGardenPlan(plan,work.Rule.PlanTemplate);
                    }
                    gardenTendStatus="Waiting here for the stage plan to sync.";gardenAdventureNext=now.AddSeconds(5);return;
                }
            }
            if(plan.Beds.Any(b=>!b.ObservedAt.HasValue))throw new InvalidOperationException("Inspect / sync the batch before automatic gardening.");
            var action=GardenAdventureAction(plan,work.Rule,now);
            if(action==null){gardenTendRoute.Dequeue();return;}
            if(action==GardenCareAction.Plant){gardenAutoPlantPlan=plan;gardenCareRun=null;ContinueAutoPlant(plan);if(gardenCareRun==null)throw new InvalidOperationException("Planting needs its exact seeds and soil in bags.");}
            else{
                var port=new GardenCarePort(this,plan);port.Validate();gardenCareRun=new(port);
                var beds=plan.Beds.Where(b=>action==GardenCareAction.Inspect||GardenCarePolicy.Eligible(plan,b,action.Value,now));
                if(work.Rule.PlanTemplate!=null&&action==GardenCareAction.Harvest)beds=beds.Where(b=>work.Rule.PlanTemplate.Steps.Any(s=>s.Bed==b.Bed));
                gardenCareRun.Start(beds.Select(b=>b.Bed),action.Value,now);
                if(action==GardenCareAction.Inspect)gardenAdventureInspected[plan.HouseId+":"+plan.Batch]=now;
            }
            gardenTendStarted=true;gardenTendStatus=$"{action} · {plan.HouseName} · Batch {plan.Batch}";
        }catch(Exception e){StopTendAdventure("Stopped: "+e.Message);PrintEquinox(gardenTendStatus);}
    }
}
