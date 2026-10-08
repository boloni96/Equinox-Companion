using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private long portalReadCursor;
    private bool travelNeedsRecovery;
    private bool routeArrivalConfirmed,routeSawLoading,routeExecutionStarted;
    private Vector3 routeStartPosition;
    private bool routeTeleportAccepted;
    private DateTimeOffset nextArrivalDiagnostic,routeSettledAt;
    private readonly Queue<FollowPortalSignal> travelQueue=new();
    private readonly HashSet<string> queuedTravelIds=new();
    private FollowPortalSignal? travelAwaitingArrival;
    private DateTimeOffset travelDispatchedAt,nextQueueAttempt;
    private void ResumeAfterConfirmedTravel(){followStuck.Reset();followRecovery.Reset();followStuckStopRequested=false;followReady.Reset();followSession.Pause();}
    private void ResetTravelQueue(){aethernetArrivalTrip="";aethernetArrivalId=0;aethernetArrivalLoading=false;routeTeleportAccepted=false;routeSettledAt=default;nextArrivalDiagnostic=default;travelQueue.Clear();queuedTravelIds.Clear();travelNeedsRecovery=false;travelAwaitingArrival=null;portalReadCursor=0;routeArrivalConfirmed=false;routeSawLoading=false;routeExecutionStarted=false;nextQueueAttempt=default;acceptedPartyTeleportAt=default;}
    private void EnqueueTravel(FollowPortalSignal signal)
    {
        if(!HelperSessionPolicy.AcceptTravel(HelperPaused,signal.SentAt,followArmedAt,helperTravelCutoff)){RecordFollowTravel("Travel discarded by session",new {signal.Id,signal.SentAt,cutoff=helperTravelCutoff,paused=HelperPaused,started=followArmedAt});return;}
        if(signal.Id==lastPortalSignalId||queuedTravelIds.Contains(signal.Id))return;
        if(travelQueue.Count>=16){TravelDiagnostic("Travel queue is full; wait for the follower before the next trip.");return;}
        queuedTravelIds.Add(signal.Id);travelQueue.Enqueue(signal);
        if(helperPendingFate is {} pendingFate&&pendingFate.SentAt<=signal.SentAt){helperPendingFate=null;CancelHelperFateApproach();}
        if(helperPermission.Active&&!HoldHelperTravel){
            helperTravelAfter=Math.Max(helperTravelAfter,signal.SentAt);
            var later=helperIncoming.Where(a=>a.SentAt>helperTravelAfter).ToArray();
            ClearHelperActions();foreach(var action in later)helperIncoming.Enqueue(action);
        }
        RecordFollowTravel("Travel queued",new {signal.Id,signal.TravelKind,signal.Territory,signal.MapId,signal.CurrentWorld,signal.Approach,signal.Arrival,signal.ArrivalTerritory,signal.ArrivalMap,signal.ArrivalInstance,signal.SentAt,signal.ExpiresAt});
    }
    private void FailFollowTrip(string reason)
    {
        travelNeedsRecovery=true;routeTeleportAccepted=false;routeSettledAt=default;
        var failed=travelAwaitingArrival??followApproach??pendingTransport??pendingAethernet??pendingWard;
        if(failed!=null&&Targets.Target is {} selected&&selected.BaseId==failed.BaseId&&
            selected.ObjectKind.ToString()==failed.SourceKind&&Vector3.DistanceSquared(selected.Position,new(failed.X,failed.Y,failed.Z))<1)Targets.Target=null;
        ResumeAfterConfirmedTravel();
        CancelFollowApproach();travelAwaitingArrival=null;pendingTransport=null;pendingWard=null;pendingAethernet=null;receivedPortal=null;
        routeExecutionStarted=false;routeArrivalConfirmed=false;nextQueueAttempt=default;
        var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var retained=FollowArrivalPolicy.RecoveryTail(travelQueue,now,config.FollowThem.MeetAtTeleports);
        travelQueue.Clear();foreach(var trip in retained)travelQueue.Enqueue(trip);
        RecordFollowTravel("Travel recovery",new {failed=failed?.Id,reason,remaining=travelQueue.Count});
        TravelDiagnostic(reason+(travelQueue.Count>0?" Checking the next independent travel request.":" Waiting for a new travel request or your selected character."));
    }
    private unsafe FollowPortalSignal CaptureTravelArrival(FollowPortalSignal s)
    {
        var map=AgentMap.Instance();var self=Objects.LocalPlayer;
        return Player.IsLoaded&&map!=null&&self!=null?s with {Arrival=FollowTravelPosition.From(self.Position),ArrivalWorld=Player.CurrentWorld.RowId,ArrivalTerritory=Client.TerritoryType,ArrivalMap=map->CurrentMapId,ArrivalInstance=CurrentFollowInstance()}:s;
    }
    private unsafe void UpdateTravelQueue(DateTimeOffset now,bool loading)
    {
        if(!followSession.Armed){ResetTravelQueue();return;}
        if(HoldHelperTravel&&travelAwaitingArrival==null)return;
        ObservePartyTeleport(now,loading);
        if(travelAwaitingArrival is {} active){
            routeSawLoading|=loading;
            if(RecoverReturnedDoorLeader(active,now,loading))return;
            if(TrySelectFollowInstance(active,now))return;
            var map=AgentMap.Instance();
            var departed=map!=null&&Objects.LocalPlayer is {} moved&&FollowArrivalPolicy.HasDeparted(active,!routeExecutionStarted||followApproach!=null,routeSawLoading,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,routeStartPosition,moved.Position);
            var reconciled=MatchesAcceptedPartyTrip(active,now)&&partyTripArrived||map!=null&&Objects.LocalPlayer is {} arrivedPlayer&&FollowArrivalPolicy.AlreadyAtTravelArrival(active,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,arrivedPlayer.Position,acceptedPartyTeleportAt.ToUnixTimeMilliseconds()>=followArmedAt&&now-acceptedPartyTeleportAt<TimeSpan.FromSeconds(90));
            var nativeArrived=Objects.LocalPlayer!=null&&map!=null&&FollowArrivalPolicy.CompletedNativeTeleport(active,routeTeleportAccepted,routeSawLoading,loading,Player.IsLoaded,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,CurrentFollowInstance());
            var aethernetArrived=ConfirmAethernetArrival(active,loading);
            var arrived=aethernetArrived||nativeArrived||MatchesAcceptedPartyTrip(active,now)&&partyTripArrived&&!loading|| (followApproach==null&&departed||reconciled)&&!loading&&Player.IsLoaded&&Objects.LocalPlayer is {} self&&map!=null&&active.Arrival is {Valid:true} point&&Player.CurrentWorld.RowId==active.ArrivalWorld&&Client.TerritoryType==active.ArrivalTerritory&&map->CurrentMapId==active.ArrivalMap&&FollowInstancePolicy.Arrived(active.ArrivalInstance,CurrentFollowInstance())&&Vector3.DistanceSquared(self.Position,point.Point)<225;
            if(arrived&&now-travelDispatchedAt>TimeSpan.FromSeconds(1)){
                RecordFollowTravel("Travel arrival confirmed",new {active.Id,active.TravelKind,nativeTeleport=nativeArrived,aethernetDestination=aethernetArrived,routeSawLoading});
                travelAwaitingArrival=null;routeArrivalConfirmed=true;routeTeleportAccepted=false;routeSettledAt=default;CancelFollowApproach();pendingTransport=null;pendingWard=null;pendingAethernet=null;receivedPortal=null;
                ResumeAfterConfirmedTravel();
                TravelDiagnostic("Arrival confirmed; checking the next queued trip.");
            }else if(active.ExpiresAt<=now.ToUnixTimeMilliseconds()||(!loading&&!routeSawLoading&&followApproach==null&&now-travelDispatchedAt>TimeSpan.FromSeconds(35)||!loading&&Player.IsLoaded&&routeSettledAt!=default&&now-routeSettledAt>TimeSpan.FromSeconds(35))){
                FailFollowTrip("Travel did not reach its destination before the timeout.");
            }else {
                if(loading||!Player.IsLoaded)routeSettledAt=default;
                else if(routeSawLoading&&routeSettledAt==default)routeSettledAt=now;
                if(now>=nextArrivalDiagnostic){
                    nextArrivalDiagnostic=now.AddSeconds(5);
                    RecordFollowTravel("Arrival pending",new {active.Id,active.TravelKind,routeTeleportAccepted,routeSawLoading,loading,loaded=Player.IsLoaded,preparing=followApproach!=null,expected=new {active.ArrivalWorld,active.ArrivalTerritory,active.ArrivalMap,active.ArrivalInstance,active.Arrival},actual=new {world=Player.CurrentWorld.RowId,territory=Client.TerritoryType,map=map==null?0:map->CurrentMapId,instance=CurrentFollowInstance(),position=Objects.LocalPlayer is {} actor?FollowTravelPosition.From(actor.Position):null}});
                }
                return;
            }
        }
        if(HoldHelperTravel)return;
        if(travelAwaitingArrival==null&&travelQueue.TryPeek(out var instanceTrip)&&TrySelectFollowInstance(instanceTrip,now)){
            travelQueue.Dequeue();travelAwaitingArrival=instanceTrip;routeTeleportAccepted=false;routeSettledAt=default;nextArrivalDiagnostic=now.AddSeconds(5);travelDispatchedAt=now;routeExecutionStarted=true;routeSawLoading=loading;routeStartPosition=Objects.LocalPlayer?.Position??default;return;
        }
        if(loading||!Player.IsLoaded||HoldAcceptedPartyTeleport(now)||followApproach!=null||pendingTransport!=null||pendingWard!=null||pendingAethernet!=null||receivedPortal!=null||pendingDutyLeave!=null||lifestreamTravelOwned)return;
        if(now<nextQueueAttempt)return;
        while(travelQueue.TryPeek(out var next)){
            if(next.ExpiresAt<=now.ToUnixTimeMilliseconds()||next.SentAt<followArmedAt){travelQueue.Dequeue();FailFollowTrip("Queued trip expired.");continue;}
            if(FollowWorldReplayPolicy.AlreadyArrived(next,config.FollowThem.TargetName,config.FollowThem.HomeWorld,Player.CurrentWorld.RowId,followArmedAt,now.ToUnixTimeMilliseconds())){
                travelQueue.Dequeue();routeArrivalConfirmed=true;ResumeAfterConfirmedTravel();
                RecordFollowTravel("Completed World instruction skipped",new {next.Id,next.DestinationWorld});
                TravelDiagnostic("Already at the selected World; completed instruction cleared.");continue;
            }
            if(MatchesAcceptedPartyTrip(next,now)&&partyTripArrived){travelQueue.Dequeue();routeArrivalConfirmed=true;ResumeAfterConfirmedTravel();RecordFollowTravel("Party relay duplicate skipped",new {next.Id});continue;}
            if(Objects.LocalPlayer is {} located&&AgentMap.Instance()!=null&&
                FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,next.Name,next.HomeWorld)&&FollowInstancePolicy.Arrived(next.ArrivalInstance,CurrentFollowInstance())&&
                FollowArrivalPolicy.AlreadyAtTravelArrival(next,Player.CurrentWorld.RowId,Client.TerritoryType,AgentMap.Instance()->CurrentMapId,located.Position,acceptedPartyTeleportAt.ToUnixTimeMilliseconds()>=followArmedAt&&now-acceptedPartyTeleportAt<TimeSpan.FromSeconds(90))){
                travelQueue.Dequeue();routeArrivalConfirmed=true;ResumeAfterConfirmedTravel();
                RecordFollowTravel("Queued arrival reconciled",new {next.Id,next.TravelKind});
                TravelDiagnostic("Already at the queued destination; no second teleport needed.");continue;
            }
            if(AgentMap.Instance()!=null&&(next.Territory!=Client.TerritoryType||next.MapId!=AgentMap.Instance()->CurrentMapId)&&
                !FollowArrivalPolicy.IndependentRecovery(next,config.FollowThem.MeetAtTeleports)&&(travelNeedsRecovery||travelQueue.Skip(1).Any(s=>s.ExpiresAt>now.ToUnixTimeMilliseconds()&&FollowArrivalPolicy.IndependentRecovery(s,config.FollowThem.MeetAtTeleports)))){
                travelQueue.Dequeue();FailFollowTrip("Earlier trip is unavailable from this location.");continue;
            }
            if(routeArrivalConfirmed&&FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,next.Name,next.HomeWorld)&&next.CurrentWorld==Player.CurrentWorld.RowId&&next.Territory==Client.TerritoryType&&AgentMap.Instance()!=null&&next.MapId==AgentMap.Instance()->CurrentMapId){lastLeaderEntity=next.EntityId;lastLeaderSeen=now;}
            QueueFollowApproach(next,now);
            if(followApproach!=null||pendingDutyLeave!=null){travelQueue.Dequeue();travelNeedsRecovery=false;}
            if((followApproach!=null||pendingDutyLeave!=null)&&next.Arrival!=null){travelAwaitingArrival=next;routeTeleportAccepted=false;routeSettledAt=default;nextArrivalDiagnostic=now.AddSeconds(5);travelDispatchedAt=now;routeExecutionStarted=pendingDutyLeave!=null;routeSawLoading=false;routeStartPosition=Objects.LocalPlayer?.Position??default;}
            if(followApproach==null&&pendingDutyLeave==null){nextQueueAttempt=now.AddSeconds(1);}
            return;
        }
    }
    private readonly Queue<(string Key,FollowPortalSignal Signal,Task<bool> Audience)> outgoingTrips=new();
    private DateTimeOffset nextOutgoingTripAt,lastBoundarySentAt;
    private FollowPortalSignal? lastBoundarySent;
    private string lastBoundaryKey="";
    private void EnqueueOutgoingTravel(string key,FollowPortalSignal signal,Task<bool> audience)
    {
        var now=DateTimeOffset.UtcNow;
        var captured=signal.TravelKind=="world"?signal:CaptureTravelArrival(signal);
        captured=captured with {ExpiresAt=FollowTravelRecovery.Deadline(captured)};
        if(captured.TravelKind is "teleport" or "estate" or "friendestate")helperMeetTravel=captured;
        if(captured.TravelKind=="boundary"){
            if(lastBoundaryKey==key&&lastBoundarySent is {} previous&&now-lastBoundarySentAt<TimeSpan.FromSeconds(3)&&previous.Territory==captured.Territory&&previous.MapId==captured.MapId&&previous.ArrivalTerritory==captured.ArrivalTerritory&&previous.ArrivalInstance==captured.ArrivalInstance&&previous.Name==captured.Name&&previous.HomeWorld==captured.HomeWorld){RecordFollowTravel("Duplicate boundary capture skipped",new {captured.Id});return;}
            lastBoundarySent=captured;lastBoundarySentAt=now;lastBoundaryKey=key;
        }
        if(outgoingTrips.Count>=16){TravelDiagnostic("Outgoing travel queue full; wait for your follower.");return;}
        outgoingTrips.Enqueue((key,captured,audience));FlushOutgoingTravel();
    }
    private void FlushOutgoingTravel()
    {
        if(portalSendTask!=null||DateTimeOffset.UtcNow<nextOutgoingTripAt)return;
        while(outgoingTrips.TryPeek(out var item)){
            var now=DateTimeOffset.UtcNow;
            if(item.Key!=config.PairingKey||!SharingTravel||Player.IsLoaded&&(item.Signal.Name!=Player.CharacterName||item.Signal.HomeWorld!=Player.HomeWorld.RowId)||!FollowTravelRecovery.PendingFresh(item.Signal,now.ToUnixTimeMilliseconds())){
                outgoingTrips.Dequeue();RecordFollowTravel("Outgoing travel discarded",new {item.Signal.Id,reason="expired or sharing changed"});continue;
            }
            if(!item.Audience.IsCompleted)return;
            if(!item.Audience.IsCompletedSuccessfully||!item.Audience.Result){
                // Retain original timestamps and FIFO order; never extend a trip's lifetime.
                nextOutgoingTripAt=now.AddSeconds(5);
                var remaining=outgoingTrips.Skip(1).ToArray();outgoingTrips.Clear();
                outgoingTrips.Enqueue((item.Key,item.Signal,portalRelay.HasFollowers(item.Key,item.Signal.Name,item.Signal.HomeWorld)));
                foreach(var later in remaining)outgoingTrips.Enqueue(later);
                RecordFollowTravel("Outgoing travel awaiting active follower",new {item.Signal.Id,item.Signal.ExpiresAt});return;
            }
            outgoingTrips.Dequeue();nextOutgoingTripAt=now.AddMilliseconds(2200);
            RecordFollowTravel("Outgoing travel submitting",new {item.Signal.Id,item.Signal.SentAt,item.Signal.ExpiresAt});
            portalSendingId=item.Signal.Id;portalSendingMeeting=item.Signal.ResumeAfter>0;portalSendTask=SendPortalToAudience(item.Key,item.Signal,item.Audience);return;
        }
    }
}

