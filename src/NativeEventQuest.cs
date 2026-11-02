using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool nativeEventActive,nativeEventFromAccount,nativeEventSceneOwned,nativeEventAcceptSent,nativeEventResultSent,nativeEventYesSent,nativeEventPromptLoaded;
    private ulong nativeEventCharacter;
    private uint nativeEventNpc;
    private NativeEventStep nativeEventStep;
    private DateTimeOffset nativeEventNext,nativeEventStepAt,nativeEventActionAt,nativeEventInteractionAt,nativeEventUnavailableAt;
    private string nativeEventStatus="Keyward Bound · Companion runner",nativeEventPrompt="";
    private unsafe bool StartNativeEventQuest(bool fromAccount=false){
        try{
            if(nativeEventActive)throw new InvalidOperationException("The event quest is already running.");
            if(!Player.IsLoaded||QuestManager.Instance()==null)throw new InvalidOperationException("Log in first.");
            if(questChainActive||independentQuestId!=0||questRecorderActive||QuestToolsOtherOperation()||(!fromAccount&&eventAccountActive))throw new InvalidOperationException("Stop the other helper first.");
            if(Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.InDutyQueue]||Conditions[ConditionFlag.InCombat]||QuestConversationVisible())throw new InvalidOperationException("Finish the current duty, queue, combat or conversation first.");
            if(OptionalQuestRunning()||LifestreamBusy())throw new InvalidOperationException("Another quest or travel task is running.");
            if(QuestManager.IsQuestComplete(NativeEventPolicy.Quest)){nativeEventStatus="Keyward Bound is complete. Missing rewards are checked separately.";return true;}
            StopIndependentWalk(resetFailure:true);nativeEventActive=true;nativeEventFromAccount=fromAccount;nativeEventCharacter=Player.ContentId;
            nativeEventStep=NativeEventStep.Unsupported;nativeEventStepAt=DateTimeOffset.UtcNow;nativeEventNext=nativeEventActionAt=nativeEventUnavailableAt=default;
            nativeEventNpc=0;nativeEventSceneOwned=nativeEventAcceptSent=nativeEventResultSent=nativeEventYesSent=false;
            nativeEventPrompt="";nativeEventPromptLoaded=false;eventSaucerTravelAt=default;
            nativeEventStatus="Starting Keyward Bound with Companion";return true;
        }catch(Exception e){nativeEventStatus="Not started: "+e.Message;return false;}
    }
    private void StopNativeEventQuest(string reason){
        if(!nativeEventActive)return;
        nativeEventActive=false;CancelEventAccountMovement();nativeEventSceneOwned=false;nativeEventNpc=0;
        nativeEventStatus=reason;questToolsStatus=reason;
        PrintEquinox("Event quest · "+reason);
    }
    private void ObserveNativeEventScene(uint eventId,uint source){
        if(!nativeEventActive)return;
        nativeEventSceneOwned=(NativeEventPolicy.Scene(eventId,source,nativeEventNpc)||nativeEventSceneOwned&&eventId==NativeEventPolicy.FullQuest&&source==0)&&
            DateTimeOffset.UtcNow-nativeEventInteractionAt<TimeSpan.FromMinutes(3);
    }
    private unsafe bool NativeEventButton(AtkUnitBase* addon,AtkComponentButton* button){
        if(addon==null||!addon->IsReady||button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null)return false;
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var count=0;
        while(evt!=null&&count++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||count>32)return false;
        var click=*evt;var data=new AtkEventData();
        helperReplaying=true;try{addon->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}finally{helperReplaying=false;}
        return true;
    }
    private unsafe bool NativeEventTitle(AtkUnitBase* addon,uint questId=NativeEventPolicy.Quest){
        if(addon==null||addon->AtkValues==null||addon->AtkValuesCount>1024)return false;
        var title=QuestTitle(questId);
        for(var i=0;i<addon->AtkValuesCount;i++){
            var v=addon->AtkValues[i];if(((int)v.Type&15) is not (8 or 10))continue;
            var text=(TravelMenuText(v.String.Value)??"").Trim();
            if(text==title||text.EndsWith(" "+title,StringComparison.Ordinal))return true;
        }
        return false;
    }
    private unsafe bool NativeEventDialogue(DateTimeOffset now){
        var visible=QuestConversationVisible()||VisibleFollowAddon("SelectYesno");
        if(!visible)return false;
        if(now-nativeEventActionAt<TimeSpan.FromSeconds(1))return true;
        if(!nativeEventSceneOwned||now-nativeEventInteractionAt>TimeSpan.FromMinutes(3)){
            // The NPC's quest picker precedes the native quest scene. Match the
            // exact quest title, never the first menu entry or arbitrary Yes.
            if(nativeEventNpc!=0&&Targets.Target?.BaseId==nativeEventNpc&&now-nativeEventInteractionAt<TimeSpan.FromSeconds(20)){
                foreach(var name in new[]{"SelectString","SelectIconString"}){
                    var addon=KeyboundAddon(name);var choices=HelperChoices(addon,name);
                    var matches=choices.Select((text,index)=>(text,index)).Where(x=>x.text.Trim()==QuestTitle(NativeEventPolicy.Quest)).ToArray();
                    if(matches.Length==1){SelectTravelChoice(addon,matches[0].index);nativeEventActionAt=now;return true;}
                }
            }
            nativeEventStatus="Waiting for a verified Keyward Bound conversation; other windows are left alone.";return true;
        }
        if(VisibleFollowAddon("JournalAccept")){
            var addon=(AddonJournalAccept*)KeyboundAddon("JournalAccept");
            if(nativeEventStep==NativeEventStep.Accept&&!nativeEventAcceptSent&&NativeEventTitle((AtkUnitBase*)addon)&&NativeEventButton((AtkUnitBase*)addon,addon->AcceptButton)){
                nativeEventAcceptSent=true;nativeEventActionAt=now;nativeEventStatus="Accept sent · waiting for the quest journal";
            }return true;
        }
        if(VisibleFollowAddon("JournalResult")){
            var addon=(AddonJournalResult*)KeyboundAddon("JournalResult");
            if(nativeEventStep==NativeEventStep.Complete&&!nativeEventResultSent&&NativeEventTitle((AtkUnitBase*)addon)&&NativeEventButton((AtkUnitBase*)addon,addon->CompleteButton)){
                nativeEventResultSent=true;nativeEventActionAt=now;nativeEventStatus="Complete sent · waiting for confirmed completion";
            }else nativeEventStatus="Quest reward window · waiting for Complete to become available";
            return true;
        }
        if(VisibleFollowAddon("SelectYesno")){
            var addon=(AddonSelectYesno*)KeyboundAddon("SelectYesno");
            var place=DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(1360)?.PlaceName.Value.Name.ToString()??"";
            if(!nativeEventYesSent&&addon!=null&&addon->PromptText!=null&&NativeEventPolicy.EntranceConfirmation(nativeEventStep,nativeEventNpc,Client.TerritoryType,nativeEventSceneOwned,place,TravelMenuText(addon->PromptText->NodeText.StringPtr)??"")){
                if(NativeEventButton((AtkUnitBase*)addon,addon->YesButton)){nativeEventYesSent=true;nativeEventActionAt=now;nativeEventStatus="Entering Keybound Square · waiting for arrival";}
                return true;
            }
            if(nativeEventStep==NativeEventStep.Complete&&nativeEventNpc==1058752&&!nativeEventYesSent&&addon!=null&&addon->PromptText!=null){
                if(!nativeEventPromptLoaded){nativeEventPromptLoaded=true;nativeEventPrompt=ReadNativeEventPrompt();}
                if(NativeEventPolicy.Prompt(nativeEventPrompt,TravelMenuText(addon->PromptText->NodeText.StringPtr)??"")&&NativeEventButton((AtkUnitBase*)addon,addon->YesButton)){
                    nativeEventYesSent=true;nativeEventActionAt=now;return true;
                }
            }
            nativeEventStatus="Unrecognized quest confirmation · choose manually to continue";return true;
        }
        if(VisibleFollowAddon("Talk")){helperReplaying=true;try{ClickVisibleTalk();}finally{helperReplaying=false;}nativeEventActionAt=now;return true;}
        nativeEventStatus="Quest window needs manual input · waiting for the verified next step";return true;
    }
    private unsafe void NativeEventInteract(uint id,Vector3 fallback,DateTimeOffset now){
        var npc=Objects.FirstOrDefault(o=>o.BaseId==id&&o.IsTargetable);
        if(!EventAccountWalk(npc?.Position??fallback,2.5f,now)||npc==null)return;
        if(now-nativeEventActionAt<TimeSpan.FromSeconds(3)||Conditions[ConditionFlag.OccupiedInEvent])return;
        if(nativeEventNpc==id&&now-nativeEventInteractionAt<TimeSpan.FromSeconds(15))return;
        var system=TargetSystem.Instance();if(system==null)return;
        nativeEventNpc=id;nativeEventSceneOwned=false;nativeEventInteractionAt=nativeEventActionAt=now;
        Targets.Target=npc;system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)npc.Address,false);
    }
    private unsafe void UpdateNativeEventQuest(DateTimeOffset now){
        if(!nativeEventActive||now<nativeEventNext)return;nativeEventNext=now.AddMilliseconds(500);
        try{
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
            if(!Player.IsLoaded){
                if(nativeEventUnavailableAt==default)nativeEventUnavailableAt=now;
                if(now-nativeEventUnavailableAt>TimeSpan.FromSeconds(30))throw new InvalidOperationException("Character remained unavailable for 30 seconds.");
                nativeEventStatus="Waiting for the character to load";return;
            }
            nativeEventUnavailableAt=default;
            if(Player.ContentId!=nativeEventCharacter)throw new InvalidOperationException("Character changed.");
            if(FollowMovementKeysHeld())throw new InvalidOperationException("Stopped by your movement. Restart when ready.");
            if(QuestToolsOtherOperation()||questChainActive||questRecorderActive||OptionalQuestRunning())throw new InvalidOperationException("Another helper started; the event quest was stopped.");
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
            if(Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.InDutyQueue])throw new InvalidOperationException("Duty or queue detected; complete it manually.");
            var manager=QuestManager.Instance();if(manager==null)return;
            var step=NativeEventPolicy.Step(QuestManager.IsQuestComplete(NativeEventPolicy.Quest),manager->IsQuestAccepted(NativeEventPolicy.Quest),QuestManager.GetQuestSequence(NativeEventPolicy.Quest));
            if(step!=nativeEventStep){CancelEventAccountMovement();nativeEventStep=step;nativeEventStepAt=now;}
            if(now-nativeEventStepAt>TimeSpan.FromMinutes(5))throw new InvalidOperationException("No quest progress for five minutes. "+nativeEventStatus);
            if(step==NativeEventStep.Complete&&Client.TerritoryType!=1360){if(!EventAccountSquare(now))nativeEventStatus=eventAccountStatus;return;}
            if(NativeEventDialogue(now))return;
            if(Conditions[ConditionFlag.OccupiedInEvent]||Conditions[ConditionFlag.OccupiedInQuestEvent]||Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.WatchingCutscene78]){nativeEventStatus="Waiting for the quest scene to finish";return;}
            switch(step){
                case NativeEventStep.Finished:StopNativeEventQuest("Keyward Bound completed · rewards are checked separately.");return;
                case NativeEventStep.Accept:
                    nativeEventStatus="Keyward Bound · going to the catlike mammet";
                    if(!EventReachSaucer(now)||!EventReachEntrance(now)){nativeEventStatus=eventAccountStatus;return;}
                    NativeEventInteract(1058756,new(-33.7745f,0,45.1991f),now);return;
                case NativeEventStep.EnterSquare:
                    nativeEventStatus="Keyward Bound · entering Keybound Square";
                    if(Client.TerritoryType==1360){nativeEventStatus="Waiting for the quest journal to update after arrival";return;}
                    if(!EventReachSaucer(now)){nativeEventStatus=eventAccountStatus;return;}
                    NativeEventInteract(1058754,new(.804425f,3.98254f,61.2062f),now);return;
                case NativeEventStep.Complete:
                    nativeEventStatus="Keyward Bound · speaking to the magitype master";
                    if(Client.TerritoryType!=1360){if(!EventAccountSquare(now))nativeEventStatus=eventAccountStatus;return;}
                    NativeEventInteract(1058752,new(-13.6017f,.332853f,-28.1275f),now);return;
                case NativeEventStep.Wait:nativeEventStatus="Waiting for quest acceptance to update";return;
                default:throw new InvalidOperationException("Unsupported Keyward Bound quest sequence. Use the recorder to capture this step.");
            }
        }catch(Exception e){StopNativeEventQuest("Stopped: "+e.Message);}
    }
    private void DrawNativeEventQuest(){
        ImGui.TextWrapped("Keyward Bound runs in Companion. Companion uses live collision checks for local walking. Lifestream supplies aethernet/world travel; Questionable and vnavmesh are not required.");
        ImGui.TextWrapped(nativeEventStatus);
        ImGui.TextWrapped(independentWalkStatus);
        if(nativeEventActive){if(ImGui.Button("Stop event quest")){if(nativeEventFromAccount)StopEventAccount("Stopped by you.");else StopNativeEventQuest("Stopped by you.");}}
        else {ImGui.BeginDisabled(eventAccountActive||questChainActive||independentQuestId!=0||questRecorderActive);if(ImGui.Button("Start Keyward Bound"))StartNativeEventQuest();ImGui.EndDisabled();}
    }
}
