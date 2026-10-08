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
        helperLocalNpc=null;helperSceneRecovery.NewInteraction();
        helperRecordAudience=[];ResetHelperRecording();helperOutgoing.Clear();helperControls.Clear();helperOpened.Clear();helperFollowers=[];
        helperWindowOpen=false;helperStopConfirm="";helperError="";helperFateObserved=false;helperBring=null;helperMeetTravel=null;
        ClearPurchaseMirroring();helperVendorPurchases=false;purchaseQuote=null;purchaseVendor=null;purchaseSent.Clear();
        helperEventExchanges=false;helperExchangeSent.Clear();helperExchangeReport="";
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
            helperError="Quest sharing stopped: the previous follower session ended or disappeared from the relay. The follower must check their session and press Start if stopped; then interact with the NPC again.";
            helperWindowOpen=true;
            RecordFollowTravel("Helper leader session ended",new {reason="No active followers remain."});
        }
        foreach(var id in purchaseMirrorFollowers.ToArray())if(!active.Any(f=>f.Id==id))DisablePurchaseMirror(id);
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
        if(!SharingTravel){helperError="Enable Share my travel before requesting a meeting.";return;}
        if(helperMeetTravel is not {} travel||!HelperBringAvailable(travel)){
            travel=CreateCurrentMapMeeting();
            if(travel==null){helperError="No public Teleport destination was found on your current map. This area needs manual travel or a supported recorded route.";return;}
        }
        helperBring=(follower.Id,travel);SendHelperControl(follower,"resume");
    }
    private void CompleteHelperBring(HelperReply? reply,string error)
    {
        if(helperBring is not {} request)return;helperBring=null;
        if(reply==null||error.Length>0||!HelperBringAvailable(request.Travel))return;
        if(reply.TravelAfter<=0){helperError="Bring follower back requires Journal V7.11.86. No meeting request sent.";return;}
        var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fresh=request.Travel with {Id=Guid.NewGuid().ToString("N"),SentAt=now,ExpiresAt=now+120000,Sessions=[request.Session],ResumeAfter=reply.TravelAfter};
        outgoingTrips.Enqueue((config.PairingKey,fresh,Task.FromResult(true)));
        RecordFollowTravel("Meeting request created",new {fresh.Id,cutoff=reply.TravelAfter,localSentAt=now});
        helperError="Meeting request prepared; waiting for the relay response.";
    }
}

