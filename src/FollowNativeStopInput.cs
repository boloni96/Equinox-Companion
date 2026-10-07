using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Hook<FollowInputRead>? followBackDownHook,followBackPressedHook;
    private bool followNativeInputUnavailable;
    private DateTimeOffset followNativePulseUntil,followNativeRetryAt;
    private int followNativePulseAttempts,followNativePulseReads;
    private unsafe bool OverrideFollowBack(nint input,int id)=>
        id==(int)InputId.MOVE_BACK&&input==(nint)UIInputData.Instance()&&followStopPending&&nativeFollowRequested&&
        DateTimeOffset.UtcNow<followNativePulseUntil&&Player.IsLoaded&&Player.ContentId==followStopCharacter&&CanIssueFollowMovement();
    private byte ReadFollowBackDown(nint input,int id)
    {
        var original=followBackDownHook!.Original(input,id);
        if(!OverrideFollowBack(input,id))return original;
        followNativePulseReads++;return 1;
    }
    private byte ReadFollowBackPressed(nint input,int id)
    {
        var original=followBackPressedHook!.Original(input,id);
        if(!OverrideFollowBack(input,id))return original;
        followNativePulseReads++;return 1;
    }
    private unsafe bool TryNativeFollowStop(DateTimeOffset now)
    {
        if(followNativeInputUnavailable)return false;
        if(now<followNativePulseUntil)return true;
        if(followNativePulseUntil!=default)EndNativeFollowStop();
        if(followNativePulseAttempts>=3)return false;
        if(now<followNativeRetryAt)return true;
        if(!CanIssueFollowMovement())return false;
        try{
            followBackDownHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdDown,ReadFollowBackDown);
            followBackPressedHook??=Interop.HookFromAddress<FollowInputRead>(InputData.MemberFunctionPointers.IsInputIdPressed,ReadFollowBackPressed);
            if(!followBackDownHook.IsEnabled)followBackDownHook.Enable();
            if(!followBackPressedHook.IsEnabled)followBackPressedHook.Enable();
            followNativePulseReads=0;followNativePulseAttempts++;followNativePulseUntil=now.AddMilliseconds(100);followNativeRetryAt=now.AddSeconds(1);
            RecordFollowTravel("Native stop input requested",new {input="MOVE_BACK",durationMs=100,attempt=followNativePulseAttempts,textEntry=FollowStopTextEntryActive()});
            return true;
        }catch(Exception ex){
            followNativeInputUnavailable=true;followBackDownHook?.Disable();followBackPressedHook?.Disable();
            RecordFollowTravel("Native stop input unavailable",new {exception=ex.GetType().Name});return false;
        }
    }
    private void EndNativeFollowStop()
    {
        if(followNativePulseUntil!=default)RecordFollowTravel("Native stop input observed",new {reads=followNativePulseReads,attempt=followNativePulseAttempts});
        followNativePulseUntil=default;followBackDownHook?.Disable();followBackPressedHook?.Disable();
    }
}
