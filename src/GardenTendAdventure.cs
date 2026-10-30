using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Queue<(string House,int Batch)>? gardenTendRoute;
    private ulong gardenTendOwner;private string gardenTendBoard="",gardenTendGeneration="",gardenTendStatus="";
    private DateTimeOffset gardenTendDeadline;private bool gardenTendTravelSent,gardenTendStarted;
    private void DrawTendAdventure(AdventureBoard board)
    {
        if(ImGui.CollapsingHeader("Automatic tending")){
            var walk=config.GardenCareWalk;if(ImGui.Checkbox("Walk to mapped beds (Lifestream)",ref walk)){config.GardenCareWalk=walk;SaveConfiguration();}
            ImGui.TextWrapped("Uses this adventure's remaining care batches for the current character. Travels to each house, approaches mapped beds and tends only due growing crops. Never harvests, plants or changes character. Blocked paths stop for review.");
            ImGui.BeginDisabled(!adventureFresh||gardenTendRoute!=null||gardenCareRun?.Running==true);
            if(ImGui.Button("Start tend adventure"))StartTendAdventure(board);ImGui.EndDisabled();
        }
        if(gardenTendRoute!=null){if(ImGui.SmallButton("Stop tend adventure"))StopTendAdventure("Stopped by you.");ImGui.SameLine();ImGui.TextWrapped(gardenTendStatus);}
        DrawGardenCareProgress();
    }
    private void StartTendAdventure(AdventureBoard board)
    {
        if(!Player.IsLoaded||gardenCareRun?.Running==true)return;
        var characters=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).ToArray();var id=CurrentSharedCharacter()?.Id??"";
        var targets=board.Stops.Skip(board.Index).SelectMany(stop=>stop.Tasks.Where(t=>!t.Done&&t.Rule?.Kind is "care" or "garden"&&
            (t.Rule.Jobs==null||t.Rule.Jobs.Contains("care"))&&AdventurePolicy.TaskCharacterId(t,characters)==id)
            .Select(t=>(House:t.Rule!.HouseId.Length>0?t.Rule.HouseId:stop.HouseId,Batch:t.Rule.Batch>0?t.Rule.Batch:stop.Batch))).Where(x=>x.House.Length>0&&x.Batch>0).Distinct().ToArray();
        if(targets.Length==0){gardenTendStatus="No remaining care batches for this character in the selected adventure.";PrintEquinox(gardenTendStatus);return;}
        gardenTendRoute=new(targets);gardenTendOwner=Player.ContentId;gardenTendBoard=board.Id;gardenTendGeneration=board.Generation;gardenTendTravelSent=gardenTendStarted=false;gardenTendDeadline=DateTimeOffset.UtcNow.AddMinutes(10);gardenCareRun=null;
        gardenTendStatus=$"{targets.Length} care batches queued.";
    }
    private void StopTendAdventure(string reason)
    {
        if(gardenTendRoute==null)return;gardenTendRoute=null;gardenAutoPlantPlan=null;gardenCareRun?.Stop(reason);gardenTendStatus=reason;
        goSeeHomeTrip=null;try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}
    }
    private void TickTendAdventure(DateTimeOffset now)
    {
        if(gardenTendRoute==null)return;
        try{
            if(now>=gardenTendDeadline)throw new InvalidOperationException("Travel / care timed out. Check the route and garden access.");
            if(!Player.IsLoaded)return;
            if(Player.ContentId!=gardenTendOwner||Conditions[ConditionFlag.InCombat]||!adventureBoards.Any(b=>b.Id==gardenTendBoard&&b.Generation==gardenTendGeneration)||followSession.Armed)throw new InvalidOperationException("Character, adventure or active movement changed.");
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||HelperInCutscene())return;
            if(gardenTendRoute.Count==0){StopTendAdventure("Tend adventure complete · confirmed garden observations will update the Journal.");return;}
            var target=gardenTendRoute.Peek();var source=GardenPlanSources().FirstOrDefault(p=>p.HouseId==target.House&&p.Batch==target.Batch)??throw new InvalidOperationException("Selected batch is not recorded.");
            if(gardenTendStarted){
                if(gardenCareRun?.Running==true)return;
                if(gardenCareRun?.Status.StartsWith("Stopped")==true)throw new InvalidOperationException(gardenCareRun.Status);
                gardenTendRoute.Dequeue();gardenTendTravelSent=gardenTendStarted=false;gardenTendDeadline=now.AddMinutes(10);return;
            }
            if(!atOutdoorEstate||SharedGardenLocation.Match(currentAddress,[source])!=source.HouseId){
                if(!gardenTendTravelSent){if(LifestreamBusy())throw new InvalidOperationException("Another Lifestream journey is active.");EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>w.Name==source.World);if(world.Id==0)throw new InvalidOperationException("Destination world unavailable.");GoSeeBid(world.Id,source.District,source.Ward,source.Plot);gardenTendTravelSent=true;gardenTendStatus="Travelling to "+source.HouseName+" · Batch "+source.Batch;}
                return;
            }
            if(LifestreamBusy()||goSeeHomeTrip!=null)return;
            var plan=EffectiveGardenPlan(source);if(plan.Beds.Any(b=>!b.ObservedAt.HasValue))throw new InvalidOperationException("Inspect / sync this batch before automatic tending.");
            var port=new GardenCarePort(this,plan);port.Validate();gardenCareRun=new(port);gardenCareRun.Start(plan.Beds.Where(b=>GardenCarePolicy.Eligible(plan,b,GardenCareAction.Tend,now)).Select(b=>b.Bed),GardenCareAction.Tend,now);
            gardenTendStarted=true;gardenTendStatus=$"Tending {plan.HouseName} · Batch {plan.Batch} · {gardenTendRoute.Count} batches left.";
        }catch(Exception e){StopTendAdventure("Stopped: "+e.Message);PrintEquinox(gardenTendStatus);}
    }
}
