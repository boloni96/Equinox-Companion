using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset nextFollowInvitation;
    private unsafe void TryFollowInvitations(DateTimeOffset now)
    {
        if(!followSession.Armed||!Player.IsLoaded||now<nextFollowInvitation||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
        if(config.FollowThem.AcceptPartyInvites){
            var invite=InfoProxyPartyInvite.Instance();
            var yes=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
            if(invite!=null&&invite->InviteTime!=0&&FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,invite->InviterName.ToString(),invite->InviterWorldId)&&yes!=null&&yes->IsVisible&&yes->PromptText!=null){
                var prompt=yes->PromptText->NodeText.ToString();
                if(prompt.Contains(config.FollowThem.TargetName,StringComparison.Ordinal)&&prompt.Contains("party",StringComparison.OrdinalIgnoreCase)&&prompt.Contains("join",StringComparison.OrdinalIgnoreCase)){
                    nextFollowInvitation=now.AddSeconds(5);usingSharedTravel=true;
                    try{yes->FireCallbackInt(0);}finally{usingSharedTravel=false;}
                    FollowChatNotice("PARTY — Accepted the selected character's party invitation.");return;
                }
            }
        }
        if(!config.FollowThem.AcceptDutyReady||!FollowParty.Any(x=>FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,x.Name.TextValue,x.World.RowId)))return;
        var duty=(AddonContentsFinderConfirm*)GardenGui.GetAddonByName("ContentsFinderConfirm").Address;
        if(duty==null||!duty->IsVisible)return;
        var button=duty->CommenceButton;
        if(button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null){TravelDiagnostic("Duty entry is not available yet; check the duty-ready window.");return;}
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;
        for(var i=0;evt!=null&&i<16;i++,evt=evt->NextEvent)if(evt->State.EventType==AtkEventType.ButtonClick&&evt->Listener==(AtkEventListener*)duty){
            PauseFollowForTravel();nextFollowInvitation=now.AddSeconds(5);
            duty->ReceiveEvent(evt->State.EventType,(int)evt->Param,evt);
            FollowChatNotice("DUTY — Requested Commence for the duty queued with your selected character.");return;
        }
        TravelDiagnostic("Duty-ready button could not be activated; please select Commence manually.");
    }
}
