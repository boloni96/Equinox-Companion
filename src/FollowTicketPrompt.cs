using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string followTicketRequest="";
    private long followTicketSession,followTicketUntil;
    private uint followTicketWorld,followTicketTerritory;
    private bool followTicketUse;
    private void BeginFollowTicketPrompt(FollowPortalSignal signal,DateTimeOffset now)
    {
        followTicketRequest=signal.Id;followTicketSession=followArmedAt;
        followTicketUntil=now.AddSeconds(15).ToUnixTimeMilliseconds();
        followTicketWorld=Player.CurrentWorld.RowId;followTicketTerritory=Client.TerritoryType;
        followTicketUse=config.FollowThem.UseAetheryteTickets;
    }
    private unsafe void UpdateFollowTicketPrompt(DateTimeOffset now)
    {
        if(followTicketRequest.Length==0)return;
        if(!FollowTicketPolicy.Pending(followTicketRequest,travelAwaitingArrival?.Id??"",followTicketSession,followArmedAt,
            now.ToUnixTimeMilliseconds(),followTicketUntil,followSession.Armed,
            config.EnableFollowThem&&config.FollowThem.UseSharedTeleports,HelperPaused,
            Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51],
            Player.IsLoaded&&Player.CurrentWorld.RowId==followTicketWorld&&Client.TerritoryType==followTicketTerritory)){
            followTicketRequest="";return;
        }
        var dialog=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        if(dialog==null||!dialog->IsVisible||!dialog->IsReady||dialog->PromptText==null)return;
        var cut=AgentCutscene.Instance();
        if(cut!=null&&cut->SkipDialogAddonId==dialog->Id)return;
        var prompt=TravelMenuText(dialog->PromptText->NodeText.StringPtr)??"";
        if(!FollowTicketPolicy.Prompt(prompt))return;
        var button=followTicketUse?dialog->YesButton:dialog->NoButton;
        if(button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null)return;
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var count=0;
        while(evt!=null&&count++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||count>32)return;
        var request=followTicketRequest;followTicketRequest="";
        var click=*evt;var data=new AtkEventData();usingSharedTravel=true;
        try{((AtkUnitBase*)dialog)->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}
        finally{usingSharedTravel=false;}
        RecordFollowTravel("Aetheryte ticket decision submitted",new {id=request,useTicket=followTicketUse});
    }
}
