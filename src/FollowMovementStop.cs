using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool followStopPending,nativeFollowRequested,followStopUnconfirmed;
    private DateTimeOffset followStopNext;
    private ulong followStopCharacter;
    private bool followStopHadText;
    private readonly FollowStationaryGate followStopStationary=new();
    private static readonly InputId[] FollowMovementBindings=[InputId.MOVE_FORE,InputId.MOVE_BACK,InputId.MOVE_LEFT,InputId.MOVE_RIGHT,InputId.MOVE_STRIFE_L,InputId.MOVE_STRIFE_R];

    // Read the stored, UI-filtered input data. Never call InputManager.GetInputStatus:
    // the .64 stop hook received no reads and that path produced unwanted actions.
    private unsafe bool FollowMovementKeysHeld()
    {
        var input=UIInputData.Instance();if(input==null)return false;
        if(Math.Abs(input->GamepadInputs.LeftStickX)>20||Math.Abs(input->GamepadInputs.LeftStickY)>20)return true;
        foreach(var id in FollowMovementBindings){
            if(input->Keybinds==null||(int)id>=input->NumKeybinds)continue;
            foreach(var binding in input->Keybinds[(int)id].KeySettings){
                var key=(int)binding.Key;
                if(key<=0||key>=input->KeyboardInputs.KeyState.Length)continue;
                if(binding.KeyModifier==input->CurrentKeyModifier&&(input->GetKeyState(key)&KeyStateFlags.Down)!=0)return true;
            }
        }
        return false;
    }
    private unsafe bool FollowStopTextEntryActive()
    {
        var stage=AtkStage.Instance();
        return stage==null||stage->AtkInputManager==null||stage->AtkInputManager->IsTextInputActive||Dalamud.Bindings.ImGui.ImGui.GetIO().WantTextInput;
    }
    private void RequestFollowMovementStop()
    {
        if(followStopPending)return;
        followNativePulseAttempts=0;followNativeRetryAt=default;
        followStopUnconfirmed=false;followStopPending=true;followStopCharacter=Player.IsLoaded?Player.ContentId:followLogin;followStopNext=DateTimeOffset.UtcNow.AddSeconds(5);followStopStationary.Reset();
        UpdateFollowMovementStop(DateTimeOffset.UtcNow);
    }
    private unsafe void UpdateFollowMovementStop(DateTimeOffset now)
    {
        if(followStopPending&&Player.IsLoaded&&Player.ContentId!=followStopCharacter){followStopPending=false;nativeFollowRequested=false;followStopUnconfirmed=false;return;}
        // A real movement input cancels native follow even if its chat notice was missed.
        // Require release plus stationary confirmation before reporting a completed stop.
        if((followStopPending||followStopUnconfirmed)&&CanIssueFollowMovement()&&!FollowStopTextEntryActive()&&FollowMovementKeysHeld()){
            nativeFollowRequested=false;followStopUnconfirmed=false;followStopPending=true;followStopStationary.Reset();
        }
        if(!followStopPending){EndNativeFollowStop();return;}
        var textActive=FollowStopTextEntryActive();
        if(followStopHadText&&!textActive){followNativePulseAttempts=0;followNativeRetryAt=default;RecordFollowTravel("Text input released with pending stop",new {nativeFollowRequested});}
        followStopHadText=textActive;
        if(nativeFollowRequested&&TryNativeFollowStop(now))return;
        if(now>=followNativePulseUntil)EndNativeFollowStop();
        if(nativeFollowRequested&&FollowStopTextEntryActive()){
            
            if(now>=followStopNext){followStopNext=now.AddSeconds(30);TravelDiagnostic("Stop queued; close text input to finish stopping.");}
            return;
        }
        if(!nativeFollowRequested){
            EndNativeFollowStop();
            if(followStopStationary.Observe(now,Objects.LocalPlayer?.Position??default,Player.IsLoaded&&!FollowMovementKeysHeld())){
                EndNativeFollowStop();followStopPending=false;followStopUnconfirmed=false;if(!followSession.Armed){followStatus="STOPPED — Game follow cancelled; movement stopped.";RefreshFollowBar();}RecordFollowTravel("Movement stop confirmed",new {stationary=true,nativeFollowCancelled=true});
            }
            return;
        }
        // Do not post keyboard messages as a fallback: focus or bindings can change
        // before delivery. Keep travel held if the bounded native stop is unconfirmed.
        if(CanIssueFollowMovement() && (followNativeInputUnavailable||followNativePulseAttempts>=3)){
            EndNativeFollowStop();followStopPending=false;followStopUnconfirmed=true;
            RecordFollowTravel("Movement stop unconfirmed",new {nativeFollowCancelled=false,keyboardFallback=false});
            TravelDiagnostic("Game follow cancellation was not confirmed. Tap a movement key; travel is held.");
        }
    }
}
