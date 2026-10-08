using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint helperResultQuest;
    private bool helperResultCompleteIntent;
    private DateTimeOffset helperResultClosedAt;
    private string helperResultConfirmationId="";
    private bool HelperResultPending=>helperResultQuest!=0;
    private unsafe uint VisibleHelperResultQuest()
    {
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("JournalResult").Address;
        if(addon==null||!addon->IsVisible||addon->AtkValues==null||addon->AtkValuesCount>1024)return 0;
        var scene=HelperScene();var colon=scene.IndexOf(':');
        if(colon<1||!uint.TryParse(scene[..colon],out var id)||!HelperQuestScenePolicy.Quest(id))return 0;
        var title=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(id)?.Name.ToString()??"";
        if(title.Length==0)return 0;
        for(var i=0;i<addon->AtkValuesCount;i++){
            var v=addon->AtkValues[i];if(((int)v.Type&15) is not (8 or 10))continue;
            var text=(TravelMenuText(v.String.Value)??"").Trim();
            if(text==title||text.EndsWith(" "+title,StringComparison.Ordinal))return id;
        }
        return 0;
    }
    private unsafe void ObserveHelperQuestResult(DateTimeOffset now)
    {
        if(!helperRecording||!SharingQuest||helperCaptureNpc==null){helperResultQuest=0;helperResultClosedAt=default;return;}
        var manager=QuestManager.Instance();if(manager==null)return;
        var visible=VisibleHelperResultQuest();
        if(helperResultQuest==0){
            if(visible==0||!manager->IsQuestAccepted(visible))return;
            helperResultQuest=visible;helperResultCompleteIntent=false;helperResultClosedAt=default;FlushHelperTalk();
            EmitHelper("completeQuest",addon:"JournalResult",scene:"pending",questId:visible);
            RecordFollowTravel("Helper quest result observed",new {questId=visible});return;
        }
        if(VisibleFollowAddon("JournalResult")){helperResultClosedAt=default;return;}
        var accepted=manager->IsQuestAccepted(helperResultQuest);
        var completed=helperResultCompleteIntent&&!accepted&&QuestManager.IsQuestComplete(helperResultQuest);
        if(!completed){
            if(QuestConversationVisible()||Conditions[ConditionFlag.OccupiedInQuestEvent]||Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.WatchingCutscene78]||Conditions[ConditionFlag.OccupiedInCutSceneEvent]){helperResultClosedAt=default;return;}
            if(helperResultClosedAt==default){helperResultClosedAt=now;return;}
            if(now-helperResultClosedAt<TimeSpan.FromSeconds(helperResultCompleteIntent?20:2))return;
            if(helperResultCompleteIntent){helperRecordingFailed=true;helperError="Quest completion was not confirmed; the conversation was not shared.";helperResultQuest=0;return;}
        }
        var index=helperRecorded.FindLastIndex(a=>a.Kind=="completeQuest"&&a.QuestId==helperResultQuest&&a.Scene=="pending");
        if(index>=0)helperRecorded[index]=helperRecorded[index] with {Scene=completed?"complete":"decline"};
        RecordFollowTravel("Helper quest result recorded",new {questId=helperResultQuest,outcome=completed?"complete":"decline"});
        helperResultQuest=0;helperResultClosedAt=default;
    }
    private unsafe void CaptureHelperResultCallback(AtkUnitBase* addon,uint count,AtkValue* values)
    {
        if(!SharingQuest||helperReplaying||addon==null||addon!=(AtkUnitBase*)GardenGui.GetAddonByName("JournalResult").Address)return;
        ObserveHelperQuestResult(DateTimeOffset.UtcNow);
        // Reported native Complete callback is [0, selected reward]; -2 is only window close.
        if(helperResultQuest!=0&&values!=null&&count==2&&((int)values[0].Type&15) is 3 or 5&&((int)values[1].Type&15) is 3 or 5&&values[0].Int==0){
            helperResultCompleteIntent=true;
            RecordFollowTravel("Helper quest result intent",new {questId=helperResultQuest,rewardSelection=values[1].Int});
        }
    }
    private unsafe void UpdateHelperQuestResult(HelperAction a,DateTimeOffset now)
    {
        if(!HelperQuestResultPolicy.Valid(a)){BlockHelper("Quest result is not verified.");return;}
        var manager=QuestManager.Instance();if(manager==null)return;
        if(a.Id==helperResultConfirmationId){
            if(!VisibleFollowAddon("JournalResult")&&!manager->IsQuestAccepted(a.QuestId)&&QuestManager.IsQuestComplete(a.QuestId)){
                helperResultConfirmationId="";RecordFollowTravel("Helper quest result confirmed",new {a.QuestId,a.Scene,afterDialogue=true});CompleteHelperAction(now);return;
            }
            helperQuestStatus="Follow-up dialogue finished; waiting for quest completion confirmation.";
            if(now-helperActionStarted>TimeSpan.FromSeconds(20))BlockHelper("Follow-up dialogue finished but the game has not confirmed quest completion.");return;
        }
        if(helperActionSubmitted){
            if(!VisibleFollowAddon("JournalResult")&&(a.Scene=="decline"||!manager->IsQuestAccepted(a.QuestId)&&QuestManager.IsQuestComplete(a.QuestId))){RecordFollowTravel("Helper quest result confirmed",new {a.QuestId,a.Scene});CompleteHelperAction(now);return;}
            if(!VisibleFollowAddon("JournalResult")&&a.Scene=="complete"){
                var remaining=helperIncoming.Skip(1).ToArray();
                var count=HelperQuestResultPolicy.FollowupTalkCount(a,remaining);
                // Some quests commit completion only after their informational Talk pages close.
                // Keep verification before the next non-Talk action; never assume button submission is completion.
                if(count>0){
                    var verify=a with {Id=Guid.NewGuid().ToString("N")};helperResultConfirmationId=verify.Id;
                    helperIncoming.Clear();helperIncoming.Enqueue(a);
                    foreach(var talk in remaining.Take(count))helperIncoming.Enqueue(talk);
                    helperIncoming.Enqueue(verify);helperStepDelays[verify.Id]=0;
                    foreach(var later in remaining.Skip(count))helperIncoming.Enqueue(later);
                    RecordFollowTravel("Helper quest result awaiting follow-up dialogue",new {a.QuestId,lines=count});
                    CompleteHelperAction(now);return;
                }
            }
            if(now-helperActionStarted>TimeSpan.FromSeconds(20))BlockHelper("Quest completion was requested but the game has not confirmed it.");return;
        }
        if(VisibleHelperResultQuest()!=a.QuestId){if(now-helperActionStarted>TimeSpan.FromSeconds(10))BlockHelper("The matching quest completion window did not appear.");return;}
        var addon=(AddonJournalResult*)GardenGui.GetAddonByName("JournalResult").Address;
        if(addon==null||!addon->AtkUnitBase.IsReady)return;
        var button=a.Scene=="decline"?addon->DeclineButton:addon->CompleteButton;
        if(button==null||!button->IsEnabled){BlockHelper("Complete is unavailable. A reward choice or another quest requirement needs your attention; no reward was chosen automatically.");return;}
        if(button->AtkComponentBase.OwnerNode==null)return;
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var count=0;
        while(evt!=null&&count++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||count>32){BlockHelper("The quest result button has no verified click event.");return;}
        var click=*evt;var input=new AtkEventData();
        helperReplaying=true;try{addon->AtkUnitBase.ReceiveEvent(click.State.EventType,(int)click.Param,&click,&input);}finally{helperReplaying=false;}
        helperActionSubmitted=true;helperActionStarted=now;helperNextAction=now.AddMilliseconds(450);
        RecordFollowTravel("Helper quest result submitted",new {a.QuestId,a.Scene});
    }
}

