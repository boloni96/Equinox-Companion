using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record GardenAdventureWork(string TaskId,string Character,string House,int Batch,AdventureTaskRule Rule,bool TendOnly=false,bool InspectOnly=false,bool HarvestOnly=false);
    private Queue<GardenAdventureWork>? gardenTendRoute;
    private ulong gardenTendOwner,gardenVisitedCharacter,gardenTravelCharacter;
    private string gardenVisitedHouse="",gardenTravelHouse="";
    private readonly AdventureConnectionRecovery gardenConnection=new();
    private string gardenConnectionKey="";
    private string gardenTendBoard="",gardenTendGeneration="",gardenTendStatus="";
    private HashSet<string> gardenAdventureCharacters=[];
    private string gardenAdventureMain="";
    private DateTimeOffset gardenTendDeadline,gardenAdventureNext,gardenSupplyRefreshUntil;
    private bool gardenPlotApproaching,gardenPlotApproachAttempted;
    private DateTimeOffset gardenPlotApproachAt;
    private readonly GardenWalkProgress gardenPlotProgress=new();
    private readonly GardenWalkRecovery gardenPlotRecovery=new();
    private readonly GardenArrivalGate gardenArrival=new();
    private bool gardenTendTravelSent,gardenTendStarted,gardenAdventureSwitchSent,gardenTendPreparing;
    private bool gardenAdventureTend => config.GardenAdventureTend;
    private bool gardenAdventurePlant => config.GardenAdventurePlant;
    private bool gardenAdventureHarvest => config.GardenAdventureHarvest;
    private bool gardenAdventureSwitch => config.GardenAdventureSwitch;
    private readonly Dictionary<string,DateTimeOffset> gardenAdventureInspected=[];
    private readonly Dictionary<(string House,int Batch),string> gardenDeferredPlanting=[];
    private string[] gardenAdventureBlockedReasons=[];
    private void DrawTendAdventure(AdventureBoard board)=>MeasureWork("Adventure: gardening controls",()=>DrawTendAdventureMeasured(board));
 private void DrawTendAdventureMeasured(AdventureBoard board)
 {
        if(!board.Stops.Any(s=>s.Tasks.Any(AdventurePolicy.IsGardenTask)))return;
        if(ImGui.CollapsingHeader("Automatic gardening")){
            ImGui.BeginDisabled(gardenTendRoute!=null);
            var tend=gardenAdventureTend;var plant=gardenAdventurePlant;var harvest=gardenAdventureHarvest;var switchCharacters=gardenAdventureSwitch;
            var inspect=config.GardenAdventureInspect;var optionsChanged=false;
            if(ImGui.BeginTable("AutomaticGardenOptions",2,ImGuiTableFlags.SizingStretchSame)){
                ImGui.TableNextRow();ImGui.TableNextColumn();optionsChanged|=ImGui.Checkbox("Tend due",ref tend);ImGui.TableNextColumn();optionsChanged|=ImGui.Checkbox("Plant saved plans",ref plant);
                ImGui.TableNextRow();ImGui.TableNextColumn();optionsChanged|=ImGui.Checkbox("Harvest full batches",ref harvest);ImGui.TableNextColumn();optionsChanged|=ImGui.Checkbox("Inspect / sync beds",ref inspect);ImGui.EndTable();
            }
            optionsChanged|=ImGui.Checkbox("Switch assigned characters on this login",ref switchCharacters);
            if(optionsChanged){config.GardenAdventureInspect=inspect;config.GardenAdventureTend=tend;config.GardenAdventurePlant=plant;config.GardenAdventureHarvest=harvest;config.GardenAdventureSwitch=switchCharacters;SaveConfiguration();}
            var walk=config.GardenCareWalk;if(ImGui.Checkbox("Walk to mapped beds (Lifestream)",ref walk)){config.GardenCareWalk=walk;SaveConfiguration();}
            ImGui.TextWrapped("Work is grouped by plot and gardener. When planting or harvesting is selected, that plot’s assigned gardener also handles eligible inspection and tending where travel permits. Care-only work prefers the starting character. Switching stays within the starting person and verified Square Enix login. The growing adventure pauses when no work is ready; resume it later. Planting uses its exact saved seed, soil and bed order; with Harvest full batches enabled, ready crops are harvested before the next route layout is applied. Mixed maturity and protected neighbours wait. After reaching a plot, the routine rechecks all selected batches and finishes eligible work there before leaving. Confirmed arrival is reused between actions and batches on that plot. New plot visits use house travel and the short entrance approach. Stop remains available during active work.");
            ImGui.BeginDisabled(!adventureFresh||gardenCareRun?.Running==true||!Player.IsLoaded||!(gardenAdventureTend||gardenAdventurePlant||gardenAdventureHarvest||config.GardenAdventureInspect));
            if(ImGui.Button("Resume automatic gardening"))StartTendAdventure(board);ImGui.EndDisabled();ImGui.EndDisabled();
        }
        if(gardenTendRoute!=null){if(ImGui.SmallButton("Stop automatic gardening"))StopTendAdventure("Stopped by you.");ImGui.SameLine();}
        if(gardenTendStatus.Length>0)ImGui.TextWrapped(gardenTendStatus);
        if(gardenAdventureBlockedReasons.Length>0&&ImGui.TreeNode("Batch waiting reasons")){foreach(var reason in gardenAdventureBlockedReasons)ImGui.TextWrapped(reason);ImGui.TreePop();}
        DrawGardenCareProgress();
    }
    private void StartTendAdventure(AdventureBoard board)
    {
        var current=CurrentSharedCharacter();if(current==null||gardenCareRun?.Running==true)return;
        var person=config.SharedRoster?.People.FirstOrDefault(p=>p.Characters.Any(c=>c.Id==current.Id));
        gardenAdventureMain=current.Id;gardenVisitedHouse="";gardenVisitedCharacter=0;gardenDeferredPlanting.Clear();gardenAdventureBlockedReasons=[];
        RememberLoadedLoginAccount();
        gardenAdventureCharacters=(person?.Characters??[]).Where(c=>c.Id==current.Id||gardenAdventureSwitch&&GardenServiceAccount(c)>=0).Select(c=>c.Id).ToHashSet();
        gardenConnection.Reset();gardenConnectionKey=config.PairingKey;
        gardenTendRoute=new();gardenTendOwner=Player.ContentId;gardenTendBoard=board.Id;gardenTendGeneration=board.Generation;
        gardenTendPreparing=false;gardenTendTravelSent=gardenTendStarted=gardenAdventureSwitchSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;gardenCareRun=null;gardenAdventureNext=default;
        RequestStorageRefresh();gardenSupplyRefreshUntil=DateTimeOffset.UtcNow.AddSeconds(20);adventureNext=default;
        gardenTendStatus="Checking this login's assigned garden work…";
    }
    private void StopTendAdventure(string reason)
    {
        if(gardenTendRoute==null)return;
        gardenConnection.Reset();gardenConnectionKey="";gardenVisitedHouse="";gardenVisitedCharacter=0;
        CancelAssignedLogin(reason);gardenPlotApproaching=false;gardenTendRoute=null;gardenAutoPlantPlan=null;gardenCareRun?.Stop(reason);gardenTendStatus=reason;
        goSeeHomeTrip=null;goSeeFriendTrip=null;StopNativeHousing("Garden adventure stopped.");gardenArrival.Reset();
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
    private string GardenAdventureTender(SharedGardenPlan plan,string assigned,SharedCharacter[] characters,AdventureTaskRule rule)
    {
        EnsureBidWorlds();
        int Region(string world)=>bidWorlds!.FirstOrDefault(w=>string.Equals(w.Name,world,StringComparison.OrdinalIgnoreCase)).Region;
        var candidates=characters.Where(c=>gardenAdventureCharacters.Contains(c.Id)).Select(c=>new GardenTender(c.Id,Region(c.Id==CurrentSharedCharacter()?.Id?WorldName(Player.HomeWorld.RowId):c.World))).ToArray();
        return GardenAdventureJobs.Tender(gardenAdventureMain,assigned,Region(plan.World),candidates,GardenAdventureJobs.NeedsAssignedWork(rule,gardenAdventurePlant,gardenAdventureHarvest));
    }
    private bool GardenInspectionDue(SharedGardenPlan plan,AdventureTaskRule rule,DateTimeOffset now)
    {
        gardenAdventureInspected.TryGetValue(plan.HouseId+":"+plan.Batch,out var at);
        return GardenAdventureJobs.InspectionDue(rule,config.GardenAdventureInspect,plan.Beds.Any(b=>!b.ObservedAt.HasValue),at,now);
    }
    private GardenCareAction? GardenAdventureAction(SharedGardenPlan plan,AdventureTaskRule rule,DateTimeOffset now)
    {
        if(GardenInspectionDue(plan,rule,now))return GardenCareAction.Inspect;
        return GardenAdventureJobs.Action(plan,rule,gardenAdventurePlant&&!gardenDeferredPlanting.ContainsKey((plan.HouseId,plan.Batch)),
            gardenAdventureHarvest,gardenAdventureTend,now);
    }

    private (bool Empty,GardenCareAction? Action,int[] Beds,string Reason) GardenRoutePreparation(SharedGardenPlan plan,GardenPlanDefinition next,DateTimeOffset now)
    {
        var key=plan.HouseId+":"+plan.Batch;
        var inspect=!gardenAdventureInspected.TryGetValue(key,out var at)||now-at>=TimeSpan.FromMinutes(10);
        return GardenAutoPlant.PrepareRouteBeds(plan,next,now,gardenAdventureHarvest,gardenAdventureTend,inspect);
    }
    private void TickTendAdventure(DateTimeOffset now)
    {
        if(gardenTendRoute==null||now<gardenAdventureNext)return;
        gardenAdventureNext=now.AddSeconds(1);
        try{
            if(!config.SyncEnabled||config.PairingKey!=gardenConnectionKey)throw new InvalidOperationException("Adventure sync was disabled or its pairing changed.");
            var connection=gardenConnection.Check(adventureFresh,adventureFetched,now);
            if(connection==AdventureConnectionState.Expired)throw new InvalidOperationException("Adventure connection did not recover within 90 seconds. Progress is kept; resume when connected.");
            if(connection==AdventureConnectionState.Waiting){
                if(adventureRequest==null&&adventureNext>now.AddSeconds(5))adventureNext=now;
                gardenTendStatus=$"Waiting for Adventure connection · retrying automatically · {(int)(now-gardenConnection.WaitingSince!.Value).TotalSeconds}/90s";
                return;
            }
            if(gardenConnection.ResumedAfter>TimeSpan.Zero){
                if(gardenTendDeadline!=default)gardenTendDeadline+=gardenConnection.ResumedAfter;
                gardenTendStatus="Adventure connection restored · checking saved work…";
            }
            var board=adventureBoards.FirstOrDefault(b=>b.Id==gardenTendBoard&&b.Generation==gardenTendGeneration)??throw new InvalidOperationException("The saved adventure changed or was removed.");
            if(followSession.Armed||Conditions[ConditionFlag.InCombat])throw new InvalidOperationException("Another movement operation or combat started.");
            if(gardenTendRoute.Count==0){
                if(now<gardenSupplyRefreshUntil){gardenTendStatus="Refreshing harvested supplies and selected batches…";return;}
                var chars=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).ToArray();
                var tasks=board.Stops.SelectMany(s=>s.Tasks).Where(t=>!t.Done&&AdventurePolicy.IsGardenTask(t)&&t.Rule!=null).ToArray();
                if(tasks.Length==0){StopTendAdventure("Adventure complete · all requested work confirmed.");return;}
                var blocked=new List<string>(gardenDeferredPlanting.Values);
                foreach(var task in tasks){
                    var rule=task.Rule!;var cid=AdventurePolicy.TaskCharacterId(task,chars);if(rule.Batch is <1 or >3||rule.HouseId.Length==0)continue;
                    var source=GardenPlanSources().FirstOrDefault(p=>p.HouseId==rule.HouseId&&p.Batch==rule.Batch);if(source==null){blocked.Add(rule.HouseId+" · Batch "+rule.Batch+": batch is missing from the shared garden data");continue;}
                    var live=EffectiveGardenPlan(source);
                    if(GardenInspectionDue(live,rule,now)){
                        var inspector=GardenAdventureTender(live,cid,chars,rule);
                        if(inspector.Length==0){blocked.Add("Inspection needs a reachable character on this login for "+live.World);continue;}
                        if(!gardenTendRoute.Any(w=>w.House==rule.HouseId&&w.Batch==rule.Batch))
                            gardenTendRoute.Enqueue(new(task.Id,inspector,rule.HouseId,rule.Batch,rule,InspectOnly:true));
                        continue;
                    }
                    // Harvest existing crops independently of the next stage's planting gate.
                    if(gardenAdventureCharacters.Contains(cid)&&GardenAdventureJobs.FullBatchHarvest(live,rule,gardenAdventureHarvest,now)){
                        if(!gardenTendRoute.Any(w=>w.House==rule.HouseId&&w.Batch==rule.Batch))
                            gardenTendRoute.Enqueue(new(task.Id,cid,rule.HouseId,rule.Batch,rule,HarvestOnly:true));
                        continue;
                    }
                    // Route dependencies gate planting, not care of crops already in this selected batch.
                    if(GardenAdventureJobs.AllowsTending(rule)&&gardenAdventureTend&&live.Beds.Any(b=>GardenCarePolicy.Eligible(live,b,GardenCareAction.Tend,now))){
                        var tender=GardenAdventureTender(live,cid,chars,rule);
                        if(tender.Length==0){blocked.Add("Tending needs an available character on this login for "+live.World);continue;}
                        if(!gardenTendRoute.Any(w=>w.House==rule.HouseId&&w.Batch==rule.Batch))
                            gardenTendRoute.Enqueue(new(task.Id,tender,rule.HouseId,rule.Batch,rule,TendOnly:true));
                        continue;
                    }
                    if(!gardenAdventureCharacters.Contains(cid)){blocked.Add("Assigned gardener is outside this run's verified login · "+live.HouseName);continue;}
                    if(rule.Kind=="growingcycle"&&rule.Phase is "waiting" or "review"){blocked.Add(GardenAdventureJobs.NoActionReason(live,rule,gardenAdventurePlant,gardenAdventureHarvest,gardenAdventureTend,now));continue;}
                    if(rule.Kind=="growingcycle"&&rule.Phase=="plan"&&gardenAdventurePlant&&!gardenDeferredPlanting.ContainsKey((live.HouseId,live.Batch))){
                        if(rule.PlanTemplate==null){blocked.Add(live.HouseName+" · Batch "+live.Batch+": route planting layout is missing");continue;}
                        var preparation=GardenRoutePreparation(live,rule.PlanTemplate,now);
                        if(!preparation.Empty&&preparation.Action==null){blocked.Add(preparation.Reason+" · "+live.HouseName+" · Batch "+live.Batch);continue;}
                        gardenTendRoute.Enqueue(new(task.Id,cid,rule.HouseId,rule.Batch,rule));
                    }else if(GardenAdventureAction(live,rule,now)!=null)
                        gardenTendRoute.Enqueue(new(task.Id,cid,rule.HouseId,rule.Batch,rule));
                    else blocked.Add(GardenAdventureJobs.NoActionReason(live,rule,gardenAdventurePlant,gardenAdventureHarvest,gardenAdventureTend,now));
                }
                if(gardenTendRoute.Count==0){
                    var assigned=tasks.Where(t=>gardenAdventureCharacters.Contains(AdventurePolicy.TaskCharacterId(t,chars))).ToArray();
                    var reasons=blocked.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
                    gardenAdventureBlockedReasons=reasons;
                    var detail=!config.SyncCollections?"Inventory tracking is disabled; enable collection/inventory tracking for supplies.":reasons.Length>0?string.Join(" · ",reasons.Take(2))+(reasons.Length>2?" · See Batch waiting reasons":""):assigned.Length==0?"No eligible visitor work or assigned garden work is available on this login.":"Assigned tasks have no eligible recorded garden action; inspect the selected batches.";
                    StopTendAdventure("Paused · "+detail+" Saved progress is kept.");return;
                }
                gardenTendRoute=new Queue<GardenAdventureWork>(GardenAdventureJobs.OrderForVisit(gardenTendRoute,w=>w.House,w=>w.Character,gardenVisitedHouse,CurrentSharedCharacter()?.Id??""));
                gardenTendDeadline=now.AddMinutes(10);gardenTendPreparing=false;gardenTendTravelSent=gardenTendStarted=gardenAdventureSwitchSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;
            }
            if(now>=gardenTendDeadline)throw new InvalidOperationException("Travel or garden action timed out. Review before restarting.");
            var work=gardenTendRoute.Peek();
            if(!gardenTendStarted){
                var liveTask=board.Stops.SelectMany(s=>s.Tasks).FirstOrDefault(t=>t.Id==work.TaskId);
                if(liveTask==null){gardenTendRoute.Clear();gardenTendTravelSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;return;}
                if(liveTask.Done){gardenTendRoute.Dequeue();gardenTendTravelSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;return;}
                if(liveTask.Rule==null)throw new InvalidOperationException("The active garden task has no rule.");
                if(liveTask.Rule.HouseId!=work.House||liveTask.Rule.Batch!=work.Batch||!work.TendOnly&&!work.InspectOnly&&liveTask.Rule.Cid.Length>0&&liveTask.Rule.Cid!=work.Character){
                    if(LifestreamBusy()||nativeHousingTrip!=null||goSeeHomeTrip!=null||goSeeFriendTrip!=null||Conditions[ConditionFlag.Casting]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
                    gardenTendRoute.Clear();gardenTendTravelSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;gardenTendStatus="Using the updated selected batch assignment…";return;
                }
                if(!work.TendOnly&&!work.InspectOnly&&!work.HarvestOnly&&liveTask.Rule.Kind=="growingcycle"&&liveTask.Rule.Phase is "waiting" or "review"){gardenTendRoute.Dequeue();gardenTendTravelSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;return;}
                work=work with{Rule=liveTask.Rule};
            }
            if(!Player.IsLoaded){gardenArrival.Reset();return;}
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.Casting]||HelperInCutscene()){gardenArrival.Reset();return;}
            if(CurrentSharedCharacter()?.Id!=work.Character){
                if(gardenAdventureSwitchSent){if(assignedLogin==null&&assignedLoginError.Length>0)throw new InvalidOperationException(assignedLoginError);return;}
                if(Player.ContentId!=gardenTendOwner)throw new InvalidOperationException("An unexpected character logged in.");
                if(!gardenAdventureSwitch)throw new InvalidOperationException("Switch to the assigned gardener first.");
                var target=config.SharedRoster!.People.SelectMany(p=>p.Characters).Single(c=>c.Id==work.Character);
                if(!StartAssignedCharacterSwitch(target,allowLinkedAccounts:true))throw new InvalidOperationException(characterTravelStatus);
                if(characterSwitchTarget?.Id!=target.Id)throw new InvalidOperationException(characterTravelStatus);
                gardenAdventureSwitchSent=true;gardenTendStatus="Switching to "+target.Name;return;
            }
            gardenTendOwner=Player.ContentId;gardenAdventureSwitchSent=false;
            var sourcePlan=GardenPlanSources().FirstOrDefault(p=>p.HouseId==work.House&&p.Batch==work.Batch)??throw new InvalidOperationException("Selected batch is unavailable.");
            if(gardenTendStarted){
                if(gardenCareRun?.Running==true||gardenAutoPlantPlan!=null)return;
                if(gardenCareRun?.Status.StartsWith("Stopped")==true)throw new InvalidOperationException(gardenCareRun.Status);
                if(gardenTendPreparing){gardenTendPreparing=false;gardenTendStarted=false;gardenTendStatus="Preparation confirmed · continuing this batch";gardenAdventureNext=now.AddSeconds(1);return;}
                if(gardenCareRun?.Action==GardenCareAction.Harvest&&gardenCareRun.Completed>0)gardenDeferredPlanting.Clear();
                RequestStorageRefresh();nextRosterRead=default;if(syncFailures==0)nextSync=default;adventureNext=default;
                gardenTendStatus="Checking remaining work on this plot · "+sourcePlan.HouseName;
                gardenTendRoute.Clear();gardenSupplyRefreshUntil=now.AddSeconds(20);gardenPlotApproaching=gardenPlotApproachAttempted=false;gardenTendStarted=gardenTendTravelSent=false;gardenTendDeadline=now.AddMinutes(10);gardenAdventureNext=now.AddSeconds(3);return;
            }
            if(gardenTendTravelSent&&(gardenTravelHouse!=work.House||gardenTravelCharacter!=Player.ContentId)){
                if(LifestreamBusy()||nativeHousingTrip!=null||goSeeHomeTrip!=null||goSeeFriendTrip!=null||now<goSeeNativeUntil)return;
                gardenTendTravelSent=false;gardenPlotApproaching=gardenPlotApproachAttempted=false;gardenArrival.Reset();
            }
            // Only a plot this run has actually reached can reuse arrival. A
            // different character/address still takes the full estate approach.
            var reuseArrival=GardenAdventureJobs.ReuseArrival(gardenVisitedHouse,gardenVisitedCharacter,work.House,Player.ContentId,
                atOutdoorEstate&&SharedGardenLocation.Match(currentAddress,[sourcePlan])==sourcePlan.HouseId);
            if(!gardenTendTravelSent&&reuseArrival&&!LifestreamBusy()&&nativeHousingTrip==null&&goSeeHomeTrip==null&&goSeeFriendTrip==null){
                gardenTendTravelSent=true;gardenTravelHouse=work.House;gardenTravelCharacter=Player.ContentId;gardenArrival.Reset();
                gardenTendStatus="Continuing on this plot · "+sourcePlan.HouseName+" · Batch "+work.Batch;
            }
            if(!gardenTendTravelSent){
                if(LifestreamBusy())throw new InvalidOperationException("Another Lifestream journey is active.");EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>w.Name==sourcePlan.World);if(world.Id==0)throw new InvalidOperationException("Destination world unavailable.");gardenArrival.Reset();GoSeeBid(world.Id,sourcePlan.District,sourcePlan.Ward,sourcePlan.Plot);if(now>=goSeeNativeUntil&&nativeHousingTrip==null&&goSeeHomeTrip==null&&goSeeFriendTrip==null&&!LifestreamBusy())
                    throw new InvalidOperationException("House travel did not start: "+bidTravelMessage);
                gardenTendTravelSent=true;gardenTravelHouse=work.House;gardenTravelCharacter=Player.ContentId;gardenTendStatus="Travelling to "+sourcePlan.HouseName;
                return;
            }
            if(gardenTendTravelSent&&!gardenPlotApproaching){
                var pending=now<goSeeNativeUntil||LifestreamBusy()||nativeHousingTrip!=null||goSeeHomeTrip!=null||goSeeFriendTrip!=null||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("TeleportHousingFriend");
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
                if(!LifestreamBusy()&&nativeHousingTrip==null&&goSeeHomeTrip==null&&goSeeFriendTrip==null)TryApproachGardenPlot(sourcePlan,now);
                return;
            }
            if(LifestreamBusy()||nativeHousingTrip!=null||goSeeHomeTrip!=null)return;
            gardenVisitedHouse=work.House;gardenVisitedCharacter=Player.ContentId;
            DiscoverGardenBedMappings(sourcePlan,now);
            var plan=EffectiveGardenPlan(sourcePlan);
            if(work.Rule.PhysicalPatch>0&&(plan.PhysicalPatch>0?plan.PhysicalPatch:plan.Batch)!=work.Rule.PhysicalPatch)throw new InvalidOperationException("Batch labels changed. Refresh the route before restarting.");
            if(!work.TendOnly&&!work.InspectOnly&&!work.HarvestOnly&&work.Rule.Kind=="growingcycle"){
                if(work.Rule.PlanTemplate==null)throw new InvalidOperationException("Route planting layout is missing.");
                if(work.Rule.PlanAt.Length>0&&(!DateTimeOffset.TryParse(work.Rule.PlanAt,out var at)||plan.At!=at||!SameGardenDefinition(plan.Definition,work.Rule.PlanTemplate)))throw new InvalidOperationException("The route's saved batch plan changed.");
                if(work.Rule.PlanAt.Length==0){
                    if(!gardenAdventurePlant){gardenTendRoute.Dequeue();return;}
                    if(plan.CompletedAt!=null||!SameGardenDefinition(plan.Definition,work.Rule.PlanTemplate)||DateTimeOffset.TryParse(work.Rule.ReadyAfter,out var readyAfter)&&plan.At<readyAfter){
                        var preparation=GardenRoutePreparation(plan,work.Rule.PlanTemplate,now);
                        if(!preparation.Empty){
                            gardenTendStatus=preparation.Reason+" · "+plan.HouseName+" · Batch "+plan.Batch;
                            var key=plan.HouseId+":"+plan.Batch;
                            if(preparation.Action is {} prepareAction&&!(prepareAction==GardenCareAction.Inspect&&gardenAdventureInspected.TryGetValue(key,out var checkedAt)&&now-checkedAt<TimeSpan.FromMinutes(10))){
                                var port=new GardenCarePort(this,plan,mappedInspection:prepareAction==GardenCareAction.Inspect);port.Validate();gardenCareRun=new(port);
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
            var action=work.HarvestOnly?(GardenAdventureJobs.FullBatchHarvest(plan,work.Rule,gardenAdventureHarvest,now)?(GardenCareAction?)GardenCareAction.Harvest:null):work.InspectOnly?(GardenInspectionDue(plan,work.Rule,now)?(GardenCareAction?)GardenCareAction.Inspect:null):work.TendOnly&&config.GardenAdventureInspect&&plan.Beds.Any(b=>!b.ObservedAt.HasValue)?GardenCareAction.Inspect:work.TendOnly?(gardenAdventureTend&&GardenAdventureJobs.AllowsTending(work.Rule)&&plan.Beds.Any(b=>GardenCarePolicy.Eligible(plan,b,GardenCareAction.Tend,now))?(GardenCareAction?)GardenCareAction.Tend:null):GardenAdventureAction(plan,work.Rule,now);
            if(action==null){gardenTendRoute.Dequeue();return;}
            if(action!=GardenCareAction.Inspect&&plan.Beds.Any(b=>!b.ObservedAt.HasValue))throw new InvalidOperationException("Inspect / sync the batch before automatic gardening.");
            if(action==GardenCareAction.Plant){gardenAutoPlantPlan=plan;gardenCareRun=null;ContinueAutoPlant(plan);if(gardenCareRun==null&&!gardenDeferredPlanting.ContainsKey((plan.HouseId,plan.Batch)))throw new InvalidOperationException("Planting needs its exact seeds and soil in bags.");}
            else{
                var port=new GardenCarePort(this,plan,visitorTend:work.TendOnly&&action==GardenCareAction.Tend,mappedInspection:action==GardenCareAction.Inspect);port.Validate();gardenCareRun=new(port);
                var beds=plan.Beds.Where(b=>action==GardenCareAction.Inspect||GardenCarePolicy.Eligible(plan,b,action.Value,now));
                if(!work.HarvestOnly&&work.Rule.PlanTemplate!=null&&action==GardenCareAction.Harvest)beds=beds.Where(b=>work.Rule.PlanTemplate.Steps.Any(s=>s.Bed==b.Bed));
                gardenCareRun.Start(beds.Select(b=>b.Bed),action.Value,now);
                if(action==GardenCareAction.Inspect)gardenAdventureInspected[plan.HouseId+":"+plan.Batch]=now;
            }
            gardenTendStarted=true;gardenTendStatus=$"{action} · {plan.HouseName} · Batch {plan.Batch}";
        }catch(Exception e){StopTendAdventure(e.Message.StartsWith("Stopped:")?e.Message:"Stopped: "+e.Message);PrintEquinox(gardenTendStatus);}
    }
    private object ReadGardenAdventureDiagnostics()
    {
        var now=DateTimeOffset.UtcNow;
        var board=adventureBoards.FirstOrDefault(b=>b.Id==(gardenTendBoard.Length>0?gardenTendBoard:adventureSelected));
        var tasks=(board?.Stops??[]).SelectMany(s=>s.Tasks).Where(t=>AdventurePolicy.IsGardenTask(t)&&t.Rule!=null).ToArray();
        var keys=tasks.Select(t=>(t.Rule!.HouseId,t.Rule.Batch)).ToHashSet();
        var plans=GardenPlanSources().Where(p=>keys.Contains((p.HouseId,p.Batch))).Select(EffectiveGardenPlan).ToArray();
        return new {
            capturedAt=now, running=gardenTendRoute!=null,
            options=new{plant=gardenAdventurePlant,harvest=gardenAdventureHarvest,tend=gardenAdventureTend,inspect=config.GardenAdventureInspect,switchCharacters=gardenAdventureSwitch},
            blockedReasons=gardenAdventureBlockedReasons,
            deferredPlanting=gardenDeferredPlanting.Select(x=>new{x.Key.House,x.Key.Batch,reason=x.Value}).ToArray(),
            batches=plans.Select(p=>new{plan=p,beds=p.Beds.Select(b=>new{b.Bed,state=EquinoxCompanion.GardenVisualState.For(b,false,now),protection=GardenCarePolicy.Protection(p,b),harvestEligible=GardenCarePolicy.Eligible(p,b,GardenCareAction.Harvest,now)}).ToArray()}).ToArray(),
            decisions=tasks.Select(t=>{
                var p=plans.FirstOrDefault(p=>p.HouseId==t.Rule!.HouseId&&p.Batch==t.Rule.Batch);
                return new{t.Id,t.Done,rule=t.Rule,action=p==null?null:GardenAdventureAction(p,t.Rule!,now)?.ToString(),reason=p==null?"Batch missing":GardenAdventureJobs.NoActionReason(p,t.Rule!,gardenAdventurePlant,gardenAdventureHarvest,gardenAdventureTend,now)};
            }).ToArray()
        };
    }

}

