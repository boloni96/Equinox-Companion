using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private FollowPortalSignal? helperMeetTravel;
    private (string Session,FollowPortalSignal Travel)? helperBring;
    private void DiscardHelperTravel()
    {
        ResetTravelQueue();receivedPortal=null;pendingAethernet=null;pendingWard=null;pendingTransport=null;pendingDutyLeave=null;
        CancelLifestreamTravel();CancelFollowApproach();relayGeneration++;
    }
    private void ClearHelperLeaderSession()
    {
        helperRecordAudience=[];ResetHelperRecording();helperOutgoing.Clear();helperControls.Clear();helperOpened.Clear();helperFollowers=[];
        helperWindowOpen=false;helperStopConfirm="";helperError="";helperFateObserved=false;helperBring=null;helperMeetTravel=null;
        outgoingTrips.Clear();outgoingTravel=null;outgoingPortal=null;transportCapture=null;boundaryDeparture=null;worldSource=null;
        helperLeaderIdentity="";helperSendIdentity="";helperControlIdentity="";nextHelperLeader=default;
    }
    private void UpdateHelperActor()
    {
        // Keep identity during normal loading. A different loaded character is a new actor.
        if(!Player.IsLoaded||Player.HomeWorld.RowId==0||string.IsNullOrWhiteSpace(Player.CharacterName))return;
        var identity=config.PairingKey+"/"+Player.CharacterName+"/"+Player.HomeWorld.RowId;
        if(identity==helperActorIdentity)return;
        ClearHelperLeaderSession();helperActorIdentity=identity;
        RecordFollowTravel("Helper leader identity reset",new {reason="Loaded character or pairing changed; old recordings and travel discarded."});
    }
    private void SetHelperFollowers(HelperFollower[] incoming)
    {
        var active=incoming.Where(f=>f.Control!="stop").ToArray();
        foreach(var old in helperFollowers)if(!active.Any(f=>f.Id==old.Id))RemoveHelperRecordingAudience(old.Id);
        if(helperFollowers.Length>0&&!helperFollowers.Any(old=>active.Any(f=>f.Id==old.Id))){
            ClearHelperLeaderSession();
            RecordFollowTravel("Helper leader session ended",new {reason="No active followers remain."});
        }
        helperFollowers=active;
    }
    private unsafe bool HelperBringAvailable(FollowPortalSignal travel)
    {
        var map=AgentMap.Instance();
        return Player.IsLoaded&&map!=null&&travel.Name==Player.CharacterName&&travel.HomeWorld==Player.HomeWorld.RowId&&
            travel.ArrivalWorld==Player.CurrentWorld.RowId&&travel.ArrivalTerritory==Client.TerritoryType&&travel.ArrivalMap==map->CurrentMapId;
    }
    private void RequestHelperBring(HelperFollower follower)
    {
        if(helperMeetTravel is not {} travel||!HelperBringAvailable(travel)){
            helperError="No supported recent teleport to your current area. Teleport to the meeting destination first, then use Bring follower back.";return;
        }
        helperBring=(follower.Id,travel);SendHelperControl(follower,"resume");
    }
    private void CompleteHelperBring(bool success)
    {
        if(helperBring is not {} request)return;helperBring=null;
        if(!success||!HelperBringAvailable(request.Travel))return;
        var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fresh=request.Travel with {Id=Guid.NewGuid().ToString("N"),SentAt=now,ExpiresAt=now+120000,Sessions=[request.Session]};
        outgoingTrips.Enqueue((config.PairingKey,fresh,Task.FromResult(true)));
        helperError="Meeting teleport requested for this follower; their travel permissions still apply.";
    }
}
