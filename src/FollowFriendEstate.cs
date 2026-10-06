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
                transportCaptureAt=DateTimeOffset.UtcNow;lastTransportChoice="";
            }
        }catch(Exception e){Log.Debug(e,"Could not observe friend estate destination");}
        friendEstateHook!.Original(agent,contentId);
    }
    private unsafe void UpdateFriendEstateHook()
    {
        if(SharingTravel&&!friendEstateHookFailed&&friendEstateHook==null){try{friendEstateHook=Interop.HookFromAddress<FriendEstateDelegate>(AgentFriendlist.MemberFunctionPointers.OpenFriendEstateTeleportation,ObserveFriendEstate);}catch(Exception){friendEstateHookFailed=true;TravelDiagnostic("Friend estate observer unavailable; that route remains manual.");}}
        if(friendEstateHook!=null){if(SharingTravel&&!friendEstateHook.IsEnabled)friendEstateHook.Enable();else if(!SharingTravel&&friendEstateHook.IsEnabled)friendEstateHook.Disable();}
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
