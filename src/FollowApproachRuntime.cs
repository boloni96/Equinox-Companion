using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private FollowPortalSignal? followApproach;
    private readonly FollowStationaryGate travelStationary=new();
    private bool approachOwnsMovement,approachStopRequested;
    private DateTimeOffset nextApproachAttempt;
    private string approachKey="",lastApproachRejection="";
    private ulong approachCharacter;
    private long approachSession;
    private int approachDispatchAttempts;
    private readonly FollowApproachProgress approachProgress=new();
    private void CancelFollowApproach()
    {
        followApproach=null;travelStationary.Reset();approachStopRequested=false;
        if(!approachOwnsMovement)return;approachOwnsMovement=false;
        try{if(LifestreamBusy())Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}
        catch(Exception){TravelDiagnostic("Could not stop the approach through Lifestream. Use a movement key to stop.");}
    }
    private unsafe void QueueFollowApproach(FollowPortalSignal signal,DateTimeOffset now)
    {
        if(signal.TravelKind=="leaveDuty"){QueueDutyLeave(signal,now);return;}
        if(followApproach!=null||lifestreamTravelOwned||pendingWard!=null||pendingAethernet!=null||pendingTransport!=null||receivedPortal!=null)return;
        if(!followSession.Armed||!Player.IsLoaded||Objects.LocalPlayer is not {} self)return;
        var map=AgentMap.Instance();if(map==null)return;
        // Validate authenticity/freshness/source first. Range to the clicked source is
        // checked at the captured leader position; actual follower range is rechecked
        // by the original handler after the approach, with the original expiry intact.
        var point=signal.Approach?.Point??self.Position;
        var valid=signal.TravelKind=="world"?
            FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,signal.Name,signal.HomeWorld)&&signal.CurrentWorld==Player.CurrentWorld.RowId&&signal.EntityId==lastLeaderEntity&&signal.SentAt>=followArmedAt&&signal.ExpiresAt>now.ToUnixTimeMilliseconds():
            signal.TravelKind=="portal"?config.FollowThem.UseSharedPortals&&FollowPortalPolicy.CanUse(signal,now.ToUnixTimeMilliseconds(),followArmedAt,config.FollowThem.TargetName,config.FollowThem.HomeWorld,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,lastLeaderEntity,(now-lastLeaderSeen).TotalSeconds,point):
            config.FollowThem.UseSharedTeleports&&FollowTravelPolicy.CanUse(signal,now.ToUnixTimeMilliseconds(),followArmedAt,config.FollowThem.TargetName,config.FollowThem.HomeWorld,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,lastLeaderEntity,(now-lastLeaderSeen).TotalSeconds,point,config.FollowThem.MeetAtTeleports);
        if(!valid){
            var reason=signal.SentAt<followArmedAt?"instruction belongs to an earlier follow session":signal.ExpiresAt<=now.ToUnixTimeMilliseconds()?"instruction expired":signal.CurrentWorld!=Player.CurrentWorld.RowId?"source world differs":signal.Territory!=Client.TerritoryType?"source territory differs":signal.MapId!=map->CurrentMapId?"source map differs":signal.EntityId!=lastLeaderEntity?"leader instance identity differs":(now-lastLeaderSeen).TotalSeconds>120?"leader observation expired":"source range, settings or instruction validation failed";
            if(lastApproachRejection!=signal.Id+reason){
                lastApproachRejection=signal.Id+reason;
                RecordFollowTravel("Travel source mismatch",new {signal.Id,reason,expected=new {signal.CurrentWorld,signal.Territory,signal.MapId,signal.Approach},actual=new {world=Player.CurrentWorld.RowId,territory=Client.TerritoryType,map=map->CurrentMapId,position=FollowTravelPosition.From(self.Position)}});
            }
            TravelDiagnostic("Travel waiting ("+signal.TravelKind+"): "+reason+"; original expiry retained.");return;
        }
        if(signal.TravelKind is not ("teleport" or "estate" or "friendestate" or "world")&&signal.Approach!=null&&!FollowApproachPolicy.CanApproach(signal,self.Position)){TravelDiagnostic("Travel position is too far away, on another level or invalid; waiting.");return;}
        followApproach=signal;approachKey=config.PairingKey;approachCharacter=Player.ContentId;approachSession=followArmedAt;
        lastPortalSignalId=signal.Id;approachDispatchAttempts=0;travelStationary.Reset();approachStopRequested=false;nextApproachAttempt=default;
        PauseFollowForTravel();TravelDiagnostic("Preparing the selected travel action; checking the captured position.");
    }
    private unsafe void UpdateFollowApproach(DateTimeOffset now)
    {
        if(followApproach is not {} signal)return;
        if(TrySelectFollowInstance(signal,now))return;
        if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||!Player.IsLoaded){
            travelStationary.Reset();
            if(!followSession.Armed||followArmedAt!=approachSession||config.PairingKey!=approachKey||signal.ExpiresAt<=now.ToUnixTimeMilliseconds())CancelFollowApproach();
            return;
        }
        var map=AgentMap.Instance();
        var remote=signal.TravelKind=="world"||config.FollowThem.MeetAtTeleports&&signal.TravelKind is "teleport" or "estate" or "friendestate";
        if(!config.EnableFollowThem||!followSession.Armed||followArmedAt!=approachSession||config.PairingKey!=approachKey||Player.IsLoaded&&Player.ContentId!=approachCharacter||Player.CurrentWorld.RowId!=signal.CurrentWorld||map==null||!remote&&(Client.TerritoryType!=signal.Territory||map->CurrentMapId!=signal.MapId)||!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,signal.Name,signal.HomeWorld)||signal.ExpiresAt<=now.ToUnixTimeMilliseconds()||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.Unconscious]){
            CancelFollowApproach();TravelDiagnostic("Travel preparation cancelled or expired; no action performed.");return;
        }
        if(signal.TravelKind=="portal"?!config.FollowThem.UseSharedPortals:signal.TravelKind!="world"&&!config.FollowThem.UseSharedTeleports){CancelFollowApproach();return;}
        if(approachOwnsMovement&&!config.FollowThem.UseLifestream){CancelFollowApproach();return;}
        if(FollowMovementKeysHeld()||followManualInputSeen){
            followManualInputSeen=false;
            if(config.FollowThem.StopOnMovement){StopFollowThem("Your movement input.");return;}
            if(approachOwnsMovement){try{if(LifestreamBusy())Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch(Exception){return;}approachOwnsMovement=false;}
            approachStopRequested=false;travelStationary.Reset();nextApproachAttempt=now.AddMilliseconds(750);
            TravelDiagnostic("Your movement paused travel; the queued trip is kept until its original expiry.");return;
        }
        if(followStopPending||followStopUnconfirmed)return;
        if(signal.TravelKind=="boundary"&&TrySelectFollowInstance(signal,now))return;
        if(now<nextApproachAttempt)return;
        if(signal.TravelKind=="teleport"&&now-acceptedPartyTeleportAt<TimeSpan.FromSeconds(15))return;
        if(Objects.LocalPlayer is not {} self)return;
        var position=self.Position;var atPoint=signal.TravelKind is "teleport" or "estate" or "friendestate" or "world"||MatchingTravelMenu(signal)||WithinTravelInteractionRange(signal)||signal.Approach==null||Vector3.DistanceSquared(position,signal.Approach.Point)<=.5625f;
        if(!atPoint){
            travelStationary.Reset();
            if(approachStopRequested){approachStopRequested=false;travelStationary.Reset();nextApproachAttempt=now.AddMilliseconds(750);return;}
            if(approachOwnsMovement){
                if(approachProgress.Stuck(now,position,signal.Approach!.Point,config.FollowThem.StuckSeconds)){
                    RequestFollowMovementStop();FailFollowTrip("No approach progress; movement stop requested.");return;
                }
                try{if(!LifestreamBusy()){CancelFollowApproach();TravelDiagnostic("Approach ended before reaching the travel position; waiting.");}}catch(Exception){CancelFollowApproach();}
                return;
            }
            if(!config.FollowThem.UseLifestream){TravelDiagnostic("Enable Lifestream integration to approach the captured position, or move there manually.");return;}
            if(FollowTransitionBusy()){TravelDiagnostic("Waiting until movement is available before approaching.");return;}
            if(now<nextApproachAttempt)return;nextApproachAttempt=now.AddSeconds(3);
            try{
                if(LifestreamBusy()){TravelDiagnostic("Lifestream is busy; waiting without replacing its task.");return;}
                Pi.GetIpcSubscriber<List<Vector3>,bool?,float?,float?,object>("Lifestream.MoveEx").InvokeAction([signal.Approach!.Point],true,.5f,.25f);
                approachOwnsMovement=true;approachProgress.Reset(now,position,signal.Approach!.Point);if(signal.TravelKind=="boundary")routeExecutionStarted=true;TravelDiagnostic("Approaching the leader's captured travel position.");
            }catch(Exception e){errorJournal.Record("follow-approach-ipc",e.Message,exceptionType:e.GetType().Name);TravelDiagnostic("Direct approach unavailable ("+e.GetType().Name+"); move to the captured position manually.");}
            return;
        }
        if(!approachStopRequested){
            if(approachOwnsMovement){
                try{if(LifestreamBusy())Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}
                catch(Exception){TravelDiagnostic("Waiting: cannot confirm that approach movement stopped.");return;}
                approachOwnsMovement=false;
            }
            if(!CanIssueFollowMovement()&&!MatchingTravelMenu(signal))return;
            RequestFollowMovementStop();approachStopRequested=true;travelStationary.Reset();
            TravelDiagnostic("At the travel position; stopping and confirming stationary.");return;
        }
        if(followStopUnconfirmed||followStopPending&&!MatchingTravelMenu(signal))return;
        if(!travelStationary.Observe(now,position,(!FollowTransitionBusy()||MatchingTravelMenu(signal))&&travelStepReady))return;
        if(signal.TravelKind=="boundary"){
            routeExecutionStarted=true;TravelDiagnostic("At the gate; waiting for instance selection or area loading.");return;
        }
        followApproach=null;travelStationary.Reset();
        TravelDiagnostic("Stationary confirmed; performing the selected travel action.");
        if(travelAwaitingArrival?.Id==signal.Id){routeStartPosition=position;routeExecutionStarted=true;routeSawLoading=false;}
        TryUseSharedPortal(signal,now);
        if(signal.TravelKind=="world"&&!lifestreamTravelOwned){followApproach=signal;nextApproachAttempt=now.AddSeconds(2);return;}
        // A failed interaction dispatch is not a departure. Keep the exact trip
        // and its original expiry so a transient range/menu failure can recover.
        if(signal.TravelKind is "portal" or "door" or "transport" or "aethernet" or "ward"&&pendingTransport==null&&pendingWard==null&&pendingAethernet==null&&receivedPortal==null){
            routeExecutionStarted=false;
            if(++approachDispatchAttempts>=3){
                FailFollowTrip("Travel could not start after three attempts.");
            }else{followApproach=signal;nextApproachAttempt=now.AddSeconds(2);}
            RecordFollowTravel("Travel dispatch deferred",new {signal.Id,signal.TravelKind,attempt=approachDispatchAttempts,position=FollowTravelPosition.From(position),source=new FollowTravelPosition(signal.X,signal.Y,signal.Z)});
        }
    }
}
