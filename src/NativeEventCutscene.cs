using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string nativeEventSceneIdentity="",nativeEventSkipAttempt="",nativeEventSkipConfirmed="",nativeEventEscapeScene="";
    private bool NativeEventEscapeAllowed(int inputId)=>HelperNativeSkipPolicy.Escape(
        nativeEventActive&&nativeEventSceneOwned&&Player.IsLoaded&&Player.ContentId==nativeEventCharacter,
        nativeEventEscapeScene.Length>0,HelperInCutscene(),nativeEventEscapeScene,nativeEventSceneIdentity,
        inputId,DateTimeOffset.UtcNow,helperNativeEscapeUntil);
    private unsafe bool UpdateNativeEventCutscene(DateTimeOffset now){
        if(!nativeEventActive)return false;
        var inScene=HelperInCutscene();
        if(!nativeEventSceneOwned||nativeEventSceneIdentity.Length==0||now-nativeEventInteractionAt>TimeSpan.FromMinutes(3)){
            if(inScene)nativeEventStatus="Cutscene · waiting; its event identity is not verified for skipping";
            return inScene;
        }
        if(TryHelperSkipMenu(out var menu,out _,out _)){
            if(nativeEventSkipConfirmed==nativeEventSceneIdentity)return true;
            var list=((AddonSelectString*)menu)->PopupMenu.PopupMenu.List;
            if(list==null||list->ListLength!=2||!list->IsItemInteractionEnabled||list->GetItemDisabledState(0))return true;
            nativeEventEscapeScene="";EndHelperNativeEscape();nativeEventSkipConfirmed=nativeEventSceneIdentity;
            helperReplaying=true;
            try{list->DispatchItemEvent(0,AtkEventType.ListItemClick);}finally{helperReplaying=false;}
            nativeEventStatus="Skipping verified event cutscene · waiting for the next step";
            TraceKeyboundRun(keyboundAccountRun,"event-cutscene-skip",nativeEventSceneIdentity,"event-quest",false);return true;
        }
        if(!inScene)return false;
        nativeEventStatus="Event cutscene · waiting for its skip prompt";
        if(nativeEventSkipAttempt==nativeEventSceneIdentity||HelperCutsceneReplayPolicy.BlockingAddons.Any(VisibleFollowAddon)||FollowStopTextEntryActive())return true;
        try{
            helperEscapeDownHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdDown,ReadHelperEscapeDown);
            helperEscapePressedHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdPressed,ReadHelperEscapePressed);
            nativeEventSkipAttempt=nativeEventEscapeScene=nativeEventSceneIdentity;
            helperNativeEscapeReads=0;helperNativeEscapeUntil=now.AddMilliseconds(100);
            if(!helperEscapeDownHook.IsEnabled)helperEscapeDownHook.Enable();
            if(!helperEscapePressedHook.IsEnabled)helperEscapePressedHook.Enable();
        }catch(Exception e){nativeEventEscapeScene="";EndHelperNativeEscape();nativeEventStatus="Cutscene skip unavailable · let it finish or skip manually";Log.Debug(e,"Event cutscene input");}
        return true;
    }
}
