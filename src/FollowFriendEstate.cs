using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe delegate void FriendEstateDelegate(AgentFriendlist* agent,ulong contentId);
    private Hook<FriendEstateDelegate>? friendEstateHook;
    private bool friendEstateHookFailed;
    private unsafe void ObserveFriendEstate(AgentFriendlist* agent,ulong contentId)
    {
        try{
            if(SharingTravel&&!usingSharedTravel&&Objects.LocalPlayer is {} self&&TravelSignal("friendestate",0,"",0,self.Position) is {} source){
                transportCapture=source with {SourceKind="FriendEstate",FriendContentId=contentId.ToString(),Steps=[]};
                transportSawLoading=false;transportCaptureAt=DateTimeOffset.UtcNow;lastTransportChoice="";
            }
        }catch(Exception e){Log.Debug(e,"Could not observe friend estate destination");}
        friendEstateHook!.Original(agent,contentId);
    }
    private unsafe void UpdateFriendEstateHook()
    {
        if(SharingTravel&&!friendEstateHookFailed&&friendEstateHook==null){try{friendEstateHook=Interop.HookFromAddress<FriendEstateDelegate>(AgentFriendlist.MemberFunctionPointers.OpenFriendEstateTeleportation,ObserveFriendEstate);}catch(Exception){friendEstateHookFailed=true;TravelDiagnostic("Friend estate observer unavailable; that route remains manual.");}}
        if(friendEstateHook!=null){if(SharingTravel&&!friendEstateHook.IsEnabled)friendEstateHook.Enable();else if(!SharingTravel&&friendEstateHook.IsEnabled)friendEstateHook.Disable();}
    }
    private unsafe bool TryOwnSharedEstate(FollowPortalSignal signal,DateTimeOffset now)
    {
        if(!FollowTravelRecovery.OwnEstate(signal,Player.ContentId))return false;
        var type=signal.Destination=="Private Estate"?FFXIVClientStructs.FFXIV.Client.Game.EstateType.PersonalEstate:FFXIVClientStructs.FFXIV.Client.Game.EstateType.FreeCompanyEstate;
        var house=FFXIVClientStructs.FFXIV.Client.Game.HousingManager.GetOwnedHouseId(type);
        var telepo=FFXIVClientStructs.FFXIV.Client.Game.UI.Telepo.Instance();
        if(telepo==null){FailFollowTrip("Your own estate teleport list is unavailable.");return true;}
        telepo->UpdateAetheryteList();
        foreach(var entry in telepo->TeleportList){
            if(house.Id==0||house.Id==ulong.MaxValue||entry.HouseId.Id!=house.Id||entry.TerritoryId!=signal.ArrivalTerritory)continue;
            var own=signal with {TravelKind="estate",EstateId=house.Id.ToString("X16"),FriendContentId="",AetheryteId=entry.AetheryteId};
            if(travelAwaitingArrival?.Id==signal.Id)travelAwaitingArrival=own;
            RecordFollowTravel("Follower own estate resolved",new {signal.Id,estate=signal.Destination,own.EstateId});
            TryUseSharedTravel(own,now);return true;
        }
        FailFollowTrip("The selected estate belongs to you, but its exact teleport is unavailable.");return true;
    }
    private unsafe bool OpenSharedFriendEstate(FollowPortalSignal signal)
    {
        if(!ulong.TryParse(signal.FriendContentId,out var id)||id==0)return false;
        var friends=InfoProxyFriendList.Instance();if(friends==null||friends->CharData==null||friends->EntryCount>200)return false;
        foreach(var friend in friends->CharDataSpan)if(friend.ContentId==id){
            var agent=AgentFriendlist.Instance();if(agent==null)return false;
            usingSharedTravel=true;try{agent->OpenFriendEstateTeleportation(id);}finally{usingSharedTravel=false;}return true;
        }
        return false;
    }
}

