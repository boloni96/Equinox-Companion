using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record GardenAdventureWork(string Character,string House,int Batch,AdventureTaskRule Rule);
    private Queue<GardenAdventureWork>? gardenTendRoute;
    private ulong gardenTendOwner;
    private string gardenTendBoard="",gardenTendGeneration="",gardenTendStatus="";
    private HashSet<string> gardenAdventureCharacters=[];
    private DateTimeOffset gardenTendDeadline,gardenAdventureNext;
    private bool gardenTendTravelSent,gardenTendStarted,gardenAdventureSwitchSent;
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
            ImGui.TextWrapped("Uses the assigned gardeners on this character's Journal account. The growing adventure waits between stages. Planting uses its exact saved seed, soil and bed order; harvesting waits for observed maturity of the whole batch. Stop remains available while waiting.");
            ImGui.BeginDisabled(!adventureFresh||gardenCareRun?.Running==true||!Player.IsLoaded||!(gardenAdventureTend||gardenAdventurePlant||gardenAdventureHarvest));
            if(ImGui.Button("Start automatic gardening"))StartTendAdventure(board);ImGui.EndDisabled();ImGui.EndDisabled();
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
        gardenTendTravelSent=gardenTendStarted=gardenAdventureSwitchSent=false;gardenCareRun=null;gardenAdventureNext=default;
        gardenTendStatus="Checking this account's assigned garden work…";
    }
    private void StopTendAdventure(string reason)
    {
        if(gardenTendRoute==null)return;
        CancelAssignedLogin(reason);gardenTendRoute=null;gardenAutoPlantPlan=null;gardenCareRun?.Stop(reason);gardenTendStatus=reason;
        goSeeHomeTrip=null;
        if(gardenAdventureSwitchSent){characterSwitchUntil=default;characterSwitchTarget=null;gardenAdventureSwitchSent=false;}
        try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}
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
                        gardenTendRoute.Enqueue(new(cid,rule.HouseId,rule.Batch,rule));
                }
                if(gardenTendRoute.Count==0){gardenTendStatus="Waiting for growth, supplies, another assigned account, or a garden inspection · route stays active.";gardenAdventureNext=now.AddSeconds(10);return;}
                gardenTendDeadline=now.AddMinutes(10);gardenTendTravelSent=gardenTendStarted=gardenAdventureSwitchSent=false;
            }
            if(now>=gardenTendDeadline)throw new InvalidOperationException("Travel or garden action timed out. Review before restarting.");
            var work=gardenTendRoute.Peek();
            if(!Player.IsLoaded)return;
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||HelperInCutscene())return;
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
                gardenTendRoute.Dequeue();gardenTendStarted=gardenTendTravelSent=false;gardenTendDeadline=now.AddMinutes(10);gardenAdventureNext=now.AddSeconds(3);return;
            }
            if(!atOutdoorEstate||SharedGardenLocation.Match(currentAddress,[sourcePlan])!=sourcePlan.HouseId){
                if(!gardenTendTravelSent){if(LifestreamBusy())throw new InvalidOperationException("Another Lifestream journey is active.");EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>w.Name==sourcePlan.World);if(world.Id==0)throw new InvalidOperationException("Destination world unavailable.");GoSeeBid(world.Id,sourcePlan.District,sourcePlan.Ward,sourcePlan.Plot);gardenTendTravelSent=true;gardenTendStatus="Travelling to "+sourcePlan.HouseName;}
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
                    if(!SameGardenDefinition(plan.Definition,work.Rule.PlanTemplate)||DateTimeOffset.TryParse(work.Rule.ReadyAfter,out var readyAfter)&&plan.At<readyAfter){
                        if(work.Rule.PlanTemplate.Steps.Any(s=>plan.Beds.FirstOrDefault(b=>b.Bed==s.Bed) is not {ActualCrop:"Empty",ObservedAt:not null}))throw new InvalidOperationException("Inspect and empty the required beds before applying the next route stage. Existing crops are protected.");
                        ChangeGardenPlan(plan,work.Rule.PlanTemplate);
                    }
                    gardenTendRoute.Clear();gardenTendStatus="Waiting for the stage plan to sync.";gardenAdventureNext=now.AddSeconds(5);return;
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
