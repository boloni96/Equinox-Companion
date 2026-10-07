using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private const string NocturneReplayPrompt="Do you wish to replay the event?";
    private bool NocturneNpc(HelperNpc npc)=>npc.Name=="Kipih Jakkya"&&npc.Territory==130;
    private uint NocturneFirstQuest=>HelperQuestIdForName("The Man in Black");
    private static bool NocturneWarning(string text)=>text.StartsWith("If you proceed, the following quest(s) will be rendered incomplete:",StringComparison.Ordinal)&&new[]{"The Man in Black","In the Dark of Night","Messenger of the Winds","The Ironworks Vendor","The Recompense Officer"}.All(text.Contains);
    private unsafe string HelperReplayPrompt()
    {
        var yes=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        if(yes==null||!yes->IsVisible||yes->PromptText==null)return "";
        var text=yes->PromptText->NodeText.ToString();
        return text==NocturneReplayPrompt||NocturneWarning(text)?text:"";
    }
    private bool HelperReplayPromptVisible()=>HelperReplayPrompt().Length>0;
    private string helperReplayCheckedPrompt="";
    private bool helperReplayChecked;
    private unsafe void ObserveHelperReplayCheck()
    {
        if(!SharingQuest||helperReplaying||!helperRecording||helperCaptureNpc is not {} npc||!NocturneNpc(npc)){helperReplayCheckedPrompt="";return;}
        var prompt=HelperReplayPrompt();var yes=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        if(!NocturneWarning(prompt)||yes==null||yes->ConfirmCheckBox==null){helperReplayCheckedPrompt="";return;}
        var check=yes->ConfirmCheckBox->IsChecked;
        if(helperReplayCheckedPrompt!=prompt){helperReplayCheckedPrompt=prompt;helperReplayChecked=false;}
        if(check==helperReplayChecked)return;
        helperReplayChecked=check;
        EmitHelper("eventReplay",prompt,HelperPolicy.Signature([prompt]),"SelectYesno",scene:check?"checked":"unchecked",questId:NocturneFirstQuest);
    }
    private unsafe bool CaptureHelperEventReplay(AtkUnitBase* addon,int index)
    {
        if(index is not (0 or 1)||helperCaptureNpc is not {} npc||!NocturneNpc(npc)||addon!=(AtkUnitBase*)GardenGui.GetAddonByName("SelectYesno").Address)return false;
        var prompt=HelperReplayPrompt();if(prompt.Length==0)return false;
        var quest=NocturneFirstQuest;if(quest==0)return false;
        ObserveHelperReplayCheck();
        EmitHelper("eventReplay",prompt,HelperPolicy.Signature([prompt]),"SelectYesno",scene:index==0?"yes":"no",questId:quest);
        return true;
    }
    private unsafe void UpdateHelperEventReplay(HelperAction action,DateTimeOffset now)
    {
        if(!NocturneNpc(action.Npc)||(action.Text!=NocturneReplayPrompt&&!NocturneWarning(action.Text))||action.Signature!=HelperPolicy.Signature([action.Text])||action.QuestId==0||action.QuestId!=NocturneFirstQuest){BlockHelper("This event replay has no verified first-time mapping.");return;}
        var prompt=HelperReplayPrompt();
        if(helperActionSubmitted){
            if(prompt!=action.Text){if(action.Scene=="no")FinishHelperConversation("Seasonal replay declined; following resumed.");else CompleteHelperAction(now);return;}
            if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The replay confirmation did not close after the selected response.");return;
        }
        if(prompt==action.Text){
            var dialog=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
            if(!dialog->IsReady)return;
            if(action.Scene is "checked" or "unchecked"){
                var check=dialog->ConfirmCheckBox;
                if(check==null){BlockHelper("The matching replay checkbox is unavailable.");return;}
                var wanted=action.Scene=="checked";
                if(check->IsChecked==wanted){CompleteHelperAction(now);return;}
                var owner=check->AtkComponentBase.OwnerNode;if(owner==null)return;
                var evt=owner->AtkResNode.AtkEventManager.Event;var n=0;
                while(evt!=null&&n++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
                if(evt==null||n>32){BlockHelper("Replay checkbox has no verified UI click event.");return;}
                var click=*evt;var input=new AtkEventData();
                helperReplaying=true;try{check->SetChecked(wanted);dialog->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&input);}finally{helperReplaying=false;}
                helperNextAction=now.AddMilliseconds(450);
                if(now-helperActionStarted>TimeSpan.FromSeconds(10))BlockHelper("Replay checkbox did not reach the selected state.");return;
            }
            var yes=action.Scene!="no";
            if(yes&&NocturneWarning(prompt)&&(dialog->ConfirmCheckBox==null||!dialog->ConfirmCheckBox->IsChecked)){BlockHelper("Replay acknowledgement must match the leader before Yes.");return;}
            var button=yes?dialog->YesButton:dialog->NoButton;
            if(button==null||!button->IsEnabled)return;
            helperReplaying=true;try{dialog->FireCallbackInt(yes?0:1);}finally{helperReplaying=false;}
            helperActionSubmitted=true;helperNextAction=now.AddMilliseconds(450);return;
        }
        if(action.Scene=="no"){
            // A first-time follower can have a quest offer instead of the replay prompt.
            if(VisibleHelperQuest()==action.QuestId){UpdateHelperQuestAccept(action with {Scene="decline"},now);return;}
            if(!QuestConversationVisible()){FinishHelperConversation("Leader declined the event; following resumed.");return;}
        }else if(action.Scene is "checked" or "unchecked"||NocturneWarning(action.Text)){
            // First-time participants have no replay warning/checkbox to acknowledge.
            var manager=FFXIVClientStructs.FFXIV.Client.Game.QuestManager.Instance();
            if(manager!=null&&manager->IsQuestAccepted(action.QuestId)){CompleteHelperAction(now);return;}
        }else if(VisibleHelperQuest()==action.QuestId){UpdateHelperQuestAccept(action with {Scene="offer"},now);return;}
        if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The matching event confirmation has not appeared.");
    }
}
