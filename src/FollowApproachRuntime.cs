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
    private string approachKey="";
    private ulong approachCharacter;
    private long approachSession;
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
            config.FollowThem.UseSharedTeleports&&FollowTravelPolicy.CanUse(signal,now.ToUnixTimeMilliseconds(),followArmedAt,config.FollowThem.TargetName,config.FollowThem.HomeWorld,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,lastLeaderEntity,(now-lastLeaderSeen).TotalSeconds,point);
        if(!valid){TravelDiagnostic("Travel position rejected: stale instruction, different session or source location.");return;}
        if(signal.Approach!=null&&!FollowApproachPolicy.CanApproach(signal,self.Position)){TravelDiagnostic("Travel position is too far away, on another level or invalid; waiting.");return;}
        followApproach=signal;approachKey=config.PairingKey;approachCharacter=Player.ContentId;approachSession=followArmedAt;
        lastPortalSignalId=signal.Id;travelStationary.Reset();approachStopRequested=false;nextApproachAttempt=default;
        PauseFollowForTravel();TravelDiagnostic("Preparing the selected travel action; checking the captured position.");
    }
    private unsafe void UpdateFollowApproach(DateTimeOffset now)
    {
        if(followApproach is not {} signal)return;
        if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||!Player.IsLoaded){
            travelStationary.Reset();
            if(!followSession.Armed||followArmedAt!=approachSession||config.PairingKey!=approachKey||signal.ExpiresAt<=now.ToUnixTimeMilliseconds())CancelFollowApproach();
            return;
        }
        var map=AgentMap.Instance();
        if(!config.EnableFollowThem||!followSession.Armed||followArmedAt!=approachSession||config.PairingKey!=approachKey||Player.IsLoaded&&Player.ContentId!=approachCharacter||Player.CurrentWorld.RowId!=signal.CurrentWorld||Client.TerritoryType!=signal.Territory||map==null||map->CurrentMapId!=signal.MapId||!FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,signal.Name,signal.HomeWorld)||signal.ExpiresAt<=now.ToUnixTimeMilliseconds()||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.Unconscious]){
            CancelFollowApproach();TravelDiagnostic("Travel preparation cancelled or expired; no action performed.");return;
        }
        if(signal.TravelKind=="portal"?!config.FollowThem.UseSharedPortals:signal.TravelKind!="world"&&!config.FollowThem.UseSharedTeleports){CancelFollowApproach();return;}
        if(approachOwnsMovement&&!config.FollowThem.UseLifestream){CancelFollowApproach();return;}
        if(followManualInputSeen){followManualInputSeen=false;CancelFollowApproach();TravelDiagnostic("Your movement cancelled the pending travel action; FollowThem stays armed.");return;}
        if(Objects.LocalPlayer is not {} self)return;
        var position=self.Position;var atPoint=signal.Approach==null||Vector3.DistanceSquared(position,signal.Approach.Point)<=.5625f;
        if(!atPoint){
            travelStationary.Reset();
            if(approachStopRequested){CancelFollowApproach();TravelDiagnostic("Moved away from the captured travel position; waiting without interacting.");return;}
            if(approachOwnsMovement){
                try{if(!LifestreamBusy()){CancelFollowApproach();TravelDiagnostic("Approach ended before reaching the travel position; waiting.");}}catch(Exception){CancelFollowApproach();}
                return;
            }
            if(!config.FollowThem.UseLifestream){TravelDiagnostic("Enable Lifestream integration to approach the captured position, or move there manually.");return;}
            if(FollowTransitionBusy()){TravelDiagnostic("Waiting until movement is available before approaching.");return;}
            if(now<nextApproachAttempt)return;nextApproachAttempt=now.AddSeconds(3);
            try{
                if(LifestreamBusy()){TravelDiagnostic("Lifestream is busy; waiting without replacing its task.");return;}
                Pi.GetIpcSubscriber<List<Vector3>,bool?,float?,float?,object>("Lifestream.MoveEx").InvokeAction([signal.Approach!.Point],false,.5f,.25f);
                approachOwnsMovement=true;TravelDiagnostic("Approaching the leader's captured travel position.");
            }catch(Exception){TravelDiagnostic("Direct approach unavailable; move to the captured position manually.");}
            return;
        }
        if(!approachStopRequested){
            if(approachOwnsMovement){
                try{if(LifestreamBusy())Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}
                catch(Exception){TravelDiagnostic("Waiting: cannot confirm that approach movement stopped.");return;}
                approachOwnsMovement=false;
            }
            if(!CanIssueFollowMovement()&&!BoundaryWardOpen(signal))return;
            if(CanIssueFollowMovement())FollowCommand("/automove off");approachStopRequested=true;travelStationary.Reset();
            TravelDiagnostic("At the travel position; stopping and confirming stationary.");return;
        }
        if(!travelStationary.Observe(now,position,(!FollowTransitionBusy()||BoundaryWardOpen(signal))&&travelStepReady))return;
        followApproach=null;travelStationary.Reset();
        TravelDiagnostic("Stationary confirmed; performing the selected travel action.");
        TryUseSharedPortal(signal,now);
    }
}
