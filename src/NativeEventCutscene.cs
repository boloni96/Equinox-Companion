using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string nativeEventSceneIdentity="",nativeEventSkipAttempt="",nativeEventSkipConfirmed="",nativeEventEscapeScene="";
    private int nativeEventSkipPulses;
    private DateTimeOffset nativeEventSkipLastPulse;
    private bool questCutsceneIsEvent;
    private string CurrentQuestScene=>questCutsceneIsEvent?nativeEventSceneIdentity:independentQuestSceneIdentity;
    private bool CurrentQuestSceneOwned=>Player.IsLoaded&&(questCutsceneIsEvent
        ?nativeEventActive&&nativeEventSceneOwned&&Player.ContentId==nativeEventCharacter
        :independentQuestId!=0&&independentScene&&Player.ContentId==independentQuestCharacter);
    private void ResetQuestCutscene(){
        nativeEventEscapeScene="";EndHelperNativeEscape();
        nativeEventSkipAttempt=nativeEventSkipConfirmed="";nativeEventSkipPulses=0;nativeEventSkipLastPulse=default;
    }
    private void QuestSceneStatus(string text){if(questCutsceneIsEvent)nativeEventStatus=text;else questToolsStatus=text;}
    private void TraceQuestScene(string kind,string text)=>TraceKeyboundRun(questCutsceneIsEvent?keyboundAccountRun:independentQuestRun,kind,text,questCutsceneIsEvent?"event-quest":"companion-quest",false);
    private bool NativeEventEscapeAllowed(int inputId)=>HelperNativeSkipPolicy.Escape(
        CurrentQuestSceneOwned,
        nativeEventEscapeScene.Length>0,HelperInCutscene(),nativeEventEscapeScene,CurrentQuestScene,
        inputId,DateTimeOffset.UtcNow,helperNativeEscapeUntil);
    private bool UpdateNativeEventCutscene(DateTimeOffset now)=>UpdateOwnedQuestCutscene(now,true,()=>NativeEventDialogue(now));
    private unsafe bool UpdateOwnedQuestCutscene(DateTimeOffset now,bool eventRunner,Func<bool> dialogue){
        if(questCutsceneIsEvent!=eventRunner){ResetQuestCutscene();questCutsceneIsEvent=eventRunner;}
        var inScene=HelperInCutscene();var scene=CurrentQuestScene;
        if(!CurrentQuestSceneOwned||scene.Length==0){
            if(inScene)QuestSceneStatus("Cutscene · waiting for verified quest scene identity");
            return inScene;
        }
        if(TryHelperSkipMenu(out var menu,out _,out _)){
            if(nativeEventSkipConfirmed==scene)return true;
            var list=((AddonSelectString*)menu)->PopupMenu.PopupMenu.List;
            if(list==null||list->ListLength!=2||!list->IsItemInteractionEnabled||list->GetItemDisabledState(0))return true;
            nativeEventEscapeScene="";EndHelperNativeEscape();nativeEventSkipConfirmed=scene;
            helperReplaying=true;
            try{list->DispatchItemEvent(0,AtkEventType.ListItemClick);}finally{helperReplaying=false;}
            QuestSceneStatus("Skipping verified quest cutscene · waiting for the next step");
            TraceQuestScene("quest-cutscene-skip",scene);return true;
        }
        if(!inScene)return false;
        if(nativeEventSkipAttempt!=scene){nativeEventSkipPulses=0;nativeEventSkipLastPulse=default;}
        if(HelperCutsceneReplayPolicy.BlockingAddons.Any(VisibleFollowAddon)){
            QuestSceneStatus("Quest scene · waiting for the open choice or confirmation");
            return dialogue();
        }
        if(FollowStopTextEntryActive()){QuestSceneStatus("Quest cutscene · waiting for text entry to close");return true;}
        QuestSceneStatus($"Quest cutscene · waiting for skip prompt ({nativeEventSkipPulses}/3 attempts)");
        if(nativeEventSkipConfirmed==scene||!EventCutsceneRetry.Ready(nativeEventSkipPulses,now,nativeEventSkipLastPulse)){
            // Some event scenes are dialogue-only or unskippable. Keep their owned Talk advancing.
            if(VisibleFollowAddon("Talk"))dialogue();
            return true;
        }
        try{
            helperEscapeDownHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdDown,ReadHelperEscapeDown);
            helperEscapePressedHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdPressed,ReadHelperEscapePressed);
            nativeEventSkipAttempt=nativeEventEscapeScene=scene;
            nativeEventSkipPulses++;nativeEventSkipLastPulse=now;
            helperNativeEscapeReads=0;helperNativeEscapeUntil=now.AddMilliseconds(250);
            TraceQuestScene("quest-cutscene-escape",$"{scene}; attempt {nativeEventSkipPulses}");
            if(!helperEscapeDownHook.IsEnabled)helperEscapeDownHook.Enable();
            if(!helperEscapePressedHook.IsEnabled)helperEscapePressedHook.Enable();
        }catch(Exception e){nativeEventEscapeScene="";EndHelperNativeEscape();QuestSceneStatus("Cutscene skip unavailable · let it finish or skip manually");Log.Debug(e,"Quest cutscene input");}
        return true;
    }
}
