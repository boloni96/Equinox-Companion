using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint helperDutyLeaveWindow;
    private DateTimeOffset helperDutyLeaveSubmittedAt;
    private unsafe bool HelperDutyButtonMatches(AtkComponentButton* button,AddonReceiveEventArgs ev)
    {
        if(button==null||button->AtkComponentBase.OwnerNode==null)return false;
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var count=0;
        while(evt!=null&&count++<32){
            if(evt->State.EventType==(AtkEventType)ev.AtkEventType&&evt->Param==ev.EventParam)return true;
            evt=evt->NextEvent;
        }
        return false;
    }
    private unsafe void UpdateHelperSoloDutyLeave(HelperAction action,DateTimeOffset now)
    {
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName(action.Addon).Address;
        if(helperActionSubmitted){
            if(addon==null||!addon->IsVisible||addon->Id!=helperDutyLeaveWindow){
                RecordFollowTravel("Solo quest duty Leave confirmed",new {action.QuestId,action.Addon});
                CompleteHelperAction(now);return;
            }
            if(now-helperDutyLeaveSubmittedAt>TimeSpan.FromSeconds(5))BlockHelper("The duty window did not close after Leave; no retry.");
            return;
        }
        if(!ReadHelperDutyPrompt(action.Addon,out var prompt,out var button,true)){
            if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The matching duty Leave prompt did not appear.");
            return;
        }
        var manager=QuestManager.Instance();
        var name=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(action.QuestId)?.Name.ToString()??"";
        if(manager==null||!HelperDutyPolicy.MatchesLeave(action,prompt,name,manager->IsQuestAccepted(action.QuestId),QuestManager.GetQuestSequence(action.QuestId))){
            BlockHelper("Duty Leave prompt or quest progress differs; nothing selected.");return;
        }
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var count=0;
        while(evt!=null&&count++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||count>32){BlockHelper("Duty Leave button has no verified click event.");return;}
        var click=*evt;var data=new AtkEventData();
        helperDutyLeaveWindow=addon->Id;helperDutyLeaveSubmittedAt=now;helperActionSubmitted=true;helperReplaying=true;
        try{addon->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}finally{helperReplaying=false;}
        helperQuestStatus="Leave submitted; waiting for the duty window to close.";helperNextAction=now.AddMilliseconds(450);
        RecordFollowTravel("Solo quest duty Leave submitted",new {action.QuestId,action.Addon});
    }
}
