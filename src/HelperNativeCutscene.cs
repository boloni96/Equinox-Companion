using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Hook<FollowInputRead>? helperEscapeDownHook,helperEscapePressedHook;
    private DateTimeOffset helperNativeEscapeUntil;
    private string helperNativeSkipAction="";
    private int helperNativeEscapeReads;
    private unsafe bool CanReadHelperEscape(nint input,int id)
    {
        if(input!=(nint)UIInputData.Instance()||!helperIncoming.TryPeek(out var a))return false;
        var permitted=config.EnableFollowThem&&helperPermission.Active&&helperPermission.Quest&&helperPermission.Skip&&!helperSessionTextAdvance&&!HelperPaused&&!helperPermission.QuestPaused&&helperBlocked.Length==0&&Player.IsLoaded;
        if(!HelperNativeSkipPolicy.Escape(permitted,a.Kind=="skip"&&a.Id==helperNativeSkipAction,HelperInCutscene(),a.Scene,HelperScene(),id,DateTimeOffset.UtcNow,helperNativeEscapeUntil))return false;
        return !FollowStopTextEntryActive()&&!HelperCutsceneReplayPolicy.BlockingAddons.Any(VisibleFollowAddon);
    }
    private byte ReadHelperEscapeDown(nint input,int id)
    {
        var original=helperEscapeDownHook!.Original(input,id);
        if(!CanReadHelperEscape(input,id))return original;
        helperNativeEscapeReads++;return 1;
    }
    private byte ReadHelperEscapePressed(nint input,int id)
    {
        var original=helperEscapePressedHook!.Original(input,id);
        if(!CanReadHelperEscape(input,id))return original;
        helperNativeEscapeReads++;return 1;
    }
    private unsafe bool BeginHelperNativeEscape(HelperAction action,DateTimeOffset now)
    {
        try{
            helperEscapeDownHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdDown,ReadHelperEscapeDown);
            helperEscapePressedHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdPressed,ReadHelperEscapePressed);
            helperNativeSkipAction=action.Id;helperNativeEscapeReads=0;helperNativeEscapeUntil=now.AddMilliseconds(100);
            if(!helperEscapeDownHook.IsEnabled)helperEscapeDownHook.Enable();
            if(!helperEscapePressedHook.IsEnabled)helperEscapePressedHook.Enable();
            RecordFollowTravel("Helper built-in Escape requested",new {action.Scene,action.QuestId});
            return true;
        }catch(Exception ex){EndHelperNativeEscape();BlockHelper("Companion cutscene input unavailable: "+ex.GetType().Name);return false;}
    }
    private void EndHelperNativeEscape()
    {
        helperEscapeDownHook?.Disable();helperEscapePressedHook?.Disable();
        if(helperNativeEscapeUntil!=default)RecordFollowTravel("Helper built-in Escape observed",new {reads=helperNativeEscapeReads});
        helperNativeEscapeUntil=default;
    }
    private void ObserveHelperNativeEscape(DateTimeOffset now)
    {
        if(helperNativeEscapeUntil==default)return;
        if(now>=helperNativeEscapeUntil||!helperPermission.Active||!helperPermission.Skip||HelperPaused||helperPermission.QuestPaused||!HelperInCutscene())
            EndHelperNativeEscape();
    }
    private unsafe void UpdateHelperNativeCutsceneSkip(HelperAction a,DateTimeOffset now)
    {
        if(!helperPermission.Skip){BlockHelper("Cutscene skipping is not enabled for this session.");return;}
        if(helperActionSubmitted){
            if(!HelperInCutscene()||HelperScene()!=a.Scene){EndHelperNativeEscape();CompleteHelperAction(now);return;}
            if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The cutscene did not finish after Companion's one Yes selection; no retry.");
            return;
        }
        if(now-helperActionStarted>TimeSpan.FromSeconds(15)){BlockHelper("Companion could not open or verify the matching cutscene skip prompt; no retry.");return;}
        if(a.Scene.Length==0||HelperScene()!=a.Scene||!HelperInCutscene())return;
        if(TryHelperSkipMenu(out var menu,out var prompt,out var choices)){
            EndHelperNativeEscape();
            if(prompt!=a.Text||HelperPolicy.Signature(choices)!=a.Signature){BlockHelper("Cutscene skip prompt differs; nothing selected.");return;}
            var list=((AddonSelectString*)menu)->PopupMenu.PopupMenu.List;
            if(list==null||list->ListLength!=2||!list->IsItemInteractionEnabled||list->GetItemDisabledState(0))return;
            helperActionSubmitted=true;helperReplaying=true;
            // Use the live list's own event dispatch. No FireCallbackInt, constructed AtkValue
            // callback, OpenSkipDialog, or direct AgentCutscene.ReceiveEvent.
            try{list->DispatchItemEvent(0,AtkEventType.ListItemClick);}finally{helperReplaying=false;}
            RecordFollowTravel("Helper built-in Yes selected",new {a.Scene,a.QuestId});
            helperNextAction=now.AddMilliseconds(450);return;
        }
        if(helperNativeSkipAction==a.Id)return;
        if(HelperCutsceneReplayPolicy.BlockingAddons.Any(VisibleFollowAddon)||FollowStopTextEntryActive())return;
        if(BeginHelperNativeEscape(a,now))helperQuestStatus="Companion is waiting for the game's cutscene skip prompt.";
    }
}
