using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool workshopSceneArmed,workshopEscapeActive,workshopSkipConfirmed;
    private string workshopScene="";
    private int workshopSkipPulses;
    private DateTimeOffset workshopSkipLastPulse;
    private void ResetWorkshopCutscene()
    {
        if(workshopEscapeActive){workshopEscapeActive=false;EndHelperNativeEscape();}
        workshopSceneArmed=workshopSkipConfirmed=false;workshopScene="";workshopSkipPulses=0;workshopSkipLastPulse=default;
    }
    private bool WorkshopEscapeAllowed(int id)=>HelperNativeSkipPolicy.Escape(
        workshopRunning&&workshopQueueRun&&workshopSceneArmed&&workshopActive!=null&&Player.IsLoaded&&Player.ContentId==workshopActive.Character&&Player.CurrentWorld.RowId==workshopActive.World,
        workshopEscapeActive,HelperInCutscene(),workshopScene,HelperScene(),id,DateTimeOffset.UtcNow,helperNativeEscapeUntil);
    private unsafe bool TickWorkshopCutscene(DateTimeOffset now)
    {
        if(!workshopSceneArmed)return false;
        var inScene=HelperInCutscene();
        if(!inScene)return false;
        workshopDeadline=now.AddSeconds(45);
        var scene=HelperScene();if(scene.Length==0)return true;
        if(workshopScene.Length==0)workshopScene=scene;
        if(scene!=workshopScene)return true;
        if(TryHelperSkipMenu(out var menu,out _,out _)){
            if(workshopSkipConfirmed)return true;
            var list=((AddonSelectString*)menu)->PopupMenu.PopupMenu.List;
            if(list==null||list->ListLength!=2||!list->IsItemInteractionEnabled||list->GetItemDisabledState(0))return true;
            if(workshopEscapeActive){workshopEscapeActive=false;EndHelperNativeEscape();}
            workshopSkipConfirmed=true;helperReplaying=true;
            try{list->DispatchItemEvent(0,AtkEventType.ListItemClick);}finally{helperReplaying=false;}
            workshopStatus="Skipping workshop production cutscene";RecordTravelPrompt("Workshop cutscene skipped",new{scene,project=workshopBefore?.ItemId});return true;
        }
        if(workshopSkipConfirmed||!EventCutsceneRetry.Ready(workshopSkipPulses,now,workshopSkipLastPulse)||FollowStopTextEntryActive()||HelperCutsceneReplayPolicy.BlockingAddons.Any(VisibleFollowAddon))return true;
        try{
            helperEscapeDownHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdDown,ReadHelperEscapeDown);
            helperEscapePressedHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdPressed,ReadHelperEscapePressed);
            workshopEscapeActive=true;workshopSkipPulses++;workshopSkipLastPulse=now;helperNativeEscapeReads=0;helperNativeEscapeUntil=now.AddMilliseconds(250);
            if(!helperEscapeDownHook.IsEnabled)helperEscapeDownHook.Enable();
            if(!helperEscapePressedHook.IsEnabled)helperEscapePressedHook.Enable();
            workshopStatus="Opening workshop cutscene skip prompt";
        }catch(Exception e){workshopEscapeActive=false;EndHelperNativeEscape();workshopStatus="Cutscene skip unavailable · waiting for the scene to finish";Log.Debug(e,"Workshop cutscene input");}
        return true;
    }
}
