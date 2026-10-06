using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private long portalReadCursor;
    private bool routeArrivalConfirmed,routeSawLoading,routeExecutionStarted;
    private Vector3 routeStartPosition;
    private readonly Queue<FollowPortalSignal> travelQueue=new();
    private readonly HashSet<string> queuedTravelIds=new();
    private FollowPortalSignal? travelAwaitingArrival;
    private DateTimeOffset travelDispatchedAt;
    private void ResetTravelQueue(){travelQueue.Clear();queuedTravelIds.Clear();travelAwaitingArrival=null;portalReadCursor=0;routeArrivalConfirmed=false;routeSawLoading=false;routeExecutionStarted=false;}
    private void EnqueueTravel(FollowPortalSignal signal)
    {
        if(signal.SentAt<followArmedAt||signal.Id==lastPortalSignalId||queuedTravelIds.Contains(signal.Id))return;
        if(travelQueue.Count>=16){TravelDiagnostic("Travel queue is full; wait for the follower before the next trip.");return;}
        queuedTravelIds.Add(signal.Id);travelQueue.Enqueue(signal);
    }
    private unsafe FollowPortalSignal CaptureTravelArrival(FollowPortalSignal s)
    {
        var map=AgentMap.Instance();var self=Objects.LocalPlayer;
        return Player.IsLoaded&&map!=null&&self!=null?s with {Arrival=FollowTravelPosition.From(self.Position),ArrivalWorld=Player.CurrentWorld.RowId,ArrivalTerritory=Client.TerritoryType,ArrivalMap=map->CurrentMapId}:s;
    }
    private unsafe void UpdateTravelQueue(DateTimeOffset now,bool loading)
    {
        if(!followSession.Armed){ResetTravelQueue();return;}
        if(travelAwaitingArrival is {} active){
            routeSawLoading|=loading;
            var map=AgentMap.Instance();
            var departed=map!=null&&Objects.LocalPlayer is {} moved&&FollowArrivalPolicy.HasDeparted(active,!routeExecutionStarted||followApproach!=null,routeSawLoading,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,routeStartPosition,moved.Position);
            var arrived=followApproach==null&&departed&&!loading&&Player.IsLoaded&&Objects.LocalPlayer is {} self&&map!=null&&active.Arrival is {Valid:true} point&&Player.CurrentWorld.RowId==active.ArrivalWorld&&Client.TerritoryType==active.ArrivalTerritory&&map->CurrentMapId==active.ArrivalMap&&Vector3.DistanceSquared(self.Position,point.Point)<225;
            if(arrived&&now-travelDispatchedAt>TimeSpan.FromSeconds(1)){
                travelAwaitingArrival=null;routeArrivalConfirmed=true;CancelFollowApproach();pendingTransport=null;pendingWard=null;pendingAethernet=null;receivedPortal=null;
                TravelDiagnostic("Arrival confirmed; checking the next queued trip.");
            }else if(active.ExpiresAt<=now.ToUnixTimeMilliseconds()){
                travelAwaitingArrival=null;travelQueue.Clear();CancelFollowApproach();pendingTransport=null;pendingWard=null;pendingAethernet=null;receivedPortal=null;
                TravelDiagnostic("Travel expired before arrival. Remaining trips cancelled; return to your follower before trying again.");
            }else return;
        }
        if(loading||!Player.IsLoaded||followApproach!=null||pendingTransport!=null||pendingWard!=null||pendingAethernet!=null||receivedPortal!=null||pendingDutyLeave!=null||lifestreamTravelOwned)return;
        while(travelQueue.TryPeek(out var next)){
            if(next.ExpiresAt<=now.ToUnixTimeMilliseconds()||next.SentAt<followArmedAt){travelQueue.Dequeue();continue;}
            travelQueue.Dequeue();
            if(routeArrivalConfirmed&&FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,next.Name,next.HomeWorld)&&next.CurrentWorld==Player.CurrentWorld.RowId&&next.Territory==Client.TerritoryType&&AgentMap.Instance()!=null&&next.MapId==AgentMap.Instance()->CurrentMapId){lastLeaderEntity=next.EntityId;lastLeaderSeen=now;}
            QueueFollowApproach(next,now);
            if((followApproach!=null||pendingDutyLeave!=null)&&next.Arrival!=null){travelAwaitingArrival=next;travelDispatchedAt=now;routeExecutionStarted=pendingDutyLeave!=null;routeSawLoading=false;routeStartPosition=Objects.LocalPlayer?.Position??default;}
            if(followApproach==null&&pendingDutyLeave==null){travelQueue.Clear();TravelDiagnostic("Queued trip could not start; remaining trips cancelled. Return to your follower.");}
            return;
        }
    }
    private readonly Queue<(string Key,FollowPortalSignal Signal,Task<bool> Audience)> outgoingTrips=new();
    private void EnqueueOutgoingTravel(string key,FollowPortalSignal signal,Task<bool> audience)
    {
        if(outgoingTrips.Count>=16){TravelDiagnostic("Outgoing travel queue full; wait for your follower.");return;}
        outgoingTrips.Enqueue((key,CaptureTravelArrival(signal),audience));FlushOutgoingTravel();
    }
    private void FlushOutgoingTravel()
    {
        if(portalSendTask!=null)return;
        while(outgoingTrips.TryDequeue(out var item)){
            if(item.Key!=config.PairingKey||!SharingTravel||DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-item.Signal.SentAt>10000)continue;
            portalSendTask=SendPortalToAudience(item.Key,item.Signal,item.Audience);return;
        }
    }
}
